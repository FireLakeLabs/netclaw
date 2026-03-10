using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NetClaw.Application.Execution;
using NetClaw.Application.Formatting;
using NetClaw.Application.Ipc;
using NetClaw.Application.Routing;
using NetClaw.Application.Scheduling;
using NetClaw.Domain.Contracts.Agents;
using NetClaw.Domain.Contracts.Channels;
using NetClaw.Domain.Contracts.Containers;
using NetClaw.Domain.Contracts.Persistence;
using NetClaw.Domain.Contracts.Services;
using NetClaw.Domain.Entities;
using NetClaw.Domain.Enums;
using NetClaw.Domain.ValueObjects;
using NetClaw.Host.Configuration;
using NetClaw.Host.Services;
using NetClaw.Infrastructure.Runtime.Agents;
using NetClaw.Infrastructure.Configuration;
using NetClaw.Infrastructure.FileSystem;
using NetClaw.Infrastructure.Paths;
using NetClaw.Infrastructure.Persistence.Sqlite;
using NetClaw.Infrastructure.Runtime;
using NetClaw.Infrastructure.Security;

namespace NetClaw.Host;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddNetClawHostServices(this IServiceCollection services, IConfiguration configuration, IHostEnvironment environment)
    {
        HostPathOptions hostPathOptions = HostPathOptions.Create(configuration, environment);
        StorageOptions storageOptions = StorageOptions.Create(hostPathOptions.ProjectRoot);
        storageOptions.Validate();

        AssistantIdentityOptions assistantIdentityOptions = CreateAssistantIdentityOptions(configuration);
        assistantIdentityOptions.Validate();

        CredentialProxyOptions credentialProxyOptions = CreateCredentialProxyOptions(configuration);
        credentialProxyOptions.Validate();

        ContainerRuntimeOptions containerRuntimeOptions = CreateContainerRuntimeOptions(configuration);
        containerRuntimeOptions.Validate();

        AgentRuntimeOptions agentRuntimeOptions = CreateAgentRuntimeOptions(configuration);
        agentRuntimeOptions.Validate();

        SchedulerOptions schedulerOptions = CreateSchedulerOptions(configuration);
        schedulerOptions.Validate();

        services.AddSingleton(hostPathOptions);
        services.AddSingleton(storageOptions);
        services.AddSingleton(assistantIdentityOptions);
        services.AddSingleton(credentialProxyOptions);
        services.AddSingleton(containerRuntimeOptions);
        services.AddSingleton(agentRuntimeOptions);
        services.AddSingleton(schedulerOptions);

        services.AddSingleton<IFileSystem, PhysicalFileSystem>();
        services.AddSingleton<GroupPathResolver>();
        services.AddSingleton<MountAllowlistLoader>();
        services.AddSingleton<MountSecurityValidator>();
        services.AddSingleton<ICommandRunner, ProcessCommandRunner>();
        services.AddSingleton<PlatformDetectionService>();
        services.AddSingleton(static serviceProvider => serviceProvider.GetRequiredService<PlatformDetectionService>().DetectCurrent());
        services.AddSingleton(serviceProvider => new SqliteConnectionFactory($"Data Source={serviceProvider.GetRequiredService<HostPathOptions>().DatabasePath}"));
        services.AddSingleton<SqliteSchemaInitializer>();

        services.AddSingleton<IMessageRepository, SqliteMessageRepository>();
        services.AddSingleton<IGroupRepository, SqliteGroupRepository>();
        services.AddSingleton<ISessionRepository, SqliteSessionRepository>();
        services.AddSingleton<ITaskRepository, SqliteTaskRepository>();
        services.AddSingleton<IRouterStateRepository, SqliteRouterStateRepository>();
        services.AddSingleton<IContainerRuntime, DockerContainerRuntime>();
        services.AddSingleton<IAgentWorkspaceBuilder, NetClawAgentWorkspaceBuilder>();
        services.AddSingleton<IAgentToolRegistry, NetClawAgentToolRegistry>();
        services.AddSingleton<ICodingAgentEngine, CopilotCodingAgentEngine>();
        services.AddSingleton<ICodingAgentEngine, ClaudeCodePlaceholderEngine>();
        services.AddSingleton<ICodingAgentEngine, CodexPlaceholderEngine>();
        services.AddSingleton<ICodingAgentEngine, OpenCodePlaceholderEngine>();
        services.AddSingleton<IAgentRuntime, NetClawAgentRuntime>();

        services.AddSingleton<IReadOnlyList<IChannel>>(_ => Array.Empty<IChannel>());
        services.AddSingleton<IMessageFormatter, XmlMessageFormatter>();
        services.AddSingleton<IOutboundRouter, ChannelOutboundRouter>();
        services.AddSingleton(serviceProvider =>
        {
            GroupExecutionQueue queue = new(maxConcurrentExecutions: 1);
            queue.SetMessageProcessor(static (_, _) => Task.FromResult(true));
            queue.SetInputHandlers(static (_, _) => false, static _ => { });
            return queue;
        });
        services.AddSingleton<IGroupExecutionQueue>(static serviceProvider => serviceProvider.GetRequiredService<GroupExecutionQueue>());
        services.AddSingleton<Func<ChatJid, string, CancellationToken, Task>>(serviceProvider =>
            (chatJid, text, cancellationToken) =>
            {
                IOutboundRouter router = serviceProvider.GetRequiredService<IOutboundRouter>();
                IReadOnlyList<IChannel> channels = serviceProvider.GetRequiredService<IReadOnlyList<IChannel>>();
                return router.RouteAsync(channels, chatJid, text, cancellationToken);
            });
        services.AddSingleton<Func<ScheduledTask, SessionId?, CancellationToken, Task<(string? Result, string? Error)>>>(
            serviceProvider =>
                async (task, sessionId, cancellationToken) =>
                {
                    IAgentRuntime runtime = serviceProvider.GetRequiredService<IAgentRuntime>();
                    AssistantIdentityOptions identity = serviceProvider.GetRequiredService<AssistantIdentityOptions>();
                    ContainerExecutionResult result = await runtime.ExecuteAsync(
                        new ContainerInput(task.Prompt, sessionId, task.GroupFolder, task.ChatJid, false, true, identity.Name),
                        cancellationToken: cancellationToken);
                    return (result.Result, result.Error);
                });
        services.AddSingleton<ITaskSchedulerService>(serviceProvider => new TaskSchedulerService(
            serviceProvider.GetRequiredService<ITaskRepository>(),
            serviceProvider.GetRequiredService<IGroupRepository>(),
            serviceProvider.GetRequiredService<ISessionRepository>(),
            serviceProvider.GetRequiredService<Func<ScheduledTask, SessionId?, CancellationToken, Task<(string? Result, string? Error)>>>(),
            serviceProvider.GetRequiredService<Func<ChatJid, string, CancellationToken, Task>>()));
        services.AddSingleton<IIpcCommandProcessor>(serviceProvider => new IpcCommandProcessor(
            serviceProvider.GetRequiredService<IGroupRepository>(),
            serviceProvider.GetRequiredService<ITaskRepository>(),
            serviceProvider.GetRequiredService<Func<ChatJid, string, CancellationToken, Task>>()));

        services.AddHostedService<HostInitializationService>();
        services.AddHostedService<SchedulerWorker>();

        return services;
    }

    private static AssistantIdentityOptions CreateAssistantIdentityOptions(IConfiguration configuration)
    {
        return new AssistantIdentityOptions
        {
            Name = configuration["NetClaw:Assistant:Name"] ?? "Andy",
            HasOwnNumber = bool.TryParse(configuration["NetClaw:Assistant:HasOwnNumber"], out bool hasOwnNumber) && hasOwnNumber
        };
    }

    private static CredentialProxyOptions CreateCredentialProxyOptions(IConfiguration configuration)
    {
        int port = 3001;
        if (int.TryParse(configuration["NetClaw:CredentialProxy:Port"], out int configuredPort))
        {
            port = configuredPort;
        }

        return new CredentialProxyOptions
        {
            Host = configuration["NetClaw:CredentialProxy:Host"] ?? "127.0.0.1",
            Port = port
        };
    }

    private static ContainerRuntimeOptions CreateContainerRuntimeOptions(IConfiguration configuration)
    {
        return new ContainerRuntimeOptions
        {
            RuntimeBinary = configuration["NetClaw:ContainerRuntime:RuntimeBinary"] ?? "docker",
            HostGatewayName = configuration["NetClaw:ContainerRuntime:HostGatewayName"] ?? "host.docker.internal",
            ProxyBindHostOverride = configuration["NetClaw:ContainerRuntime:ProxyBindHostOverride"] ?? string.Empty
        };
    }

    private static AgentRuntimeOptions CreateAgentRuntimeOptions(IConfiguration configuration)
    {
        bool keepContainerBoundary = true;
        if (bool.TryParse(configuration["NetClaw:AgentRuntime:KeepContainerBoundary"], out bool configuredKeepContainerBoundary))
        {
            keepContainerBoundary = configuredKeepContainerBoundary;
        }

        return new AgentRuntimeOptions
        {
            DefaultProvider = configuration["NetClaw:AgentRuntime:DefaultProvider"] ?? "copilot",
            KeepContainerBoundary = keepContainerBoundary,
            CopilotCliPath = configuration["NetClaw:AgentRuntime:CopilotCliPath"] ?? "copilot"
        };
    }

    private static SchedulerOptions CreateSchedulerOptions(IConfiguration configuration)
    {
        TimeSpan pollInterval = TimeSpan.FromMinutes(1);
        if (TimeSpan.TryParse(configuration["NetClaw:Scheduler:PollInterval"], out TimeSpan configuredPollInterval))
        {
            pollInterval = configuredPollInterval;
        }

        return new SchedulerOptions
        {
            PollInterval = pollInterval
        };
    }
}