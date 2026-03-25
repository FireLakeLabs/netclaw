using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using FireLakeLabs.NetClaw.Application.Channels;
using FireLakeLabs.NetClaw.Application.Execution;
using FireLakeLabs.NetClaw.Application.Formatting;
using FireLakeLabs.NetClaw.Application.Ipc;
using FireLakeLabs.NetClaw.Application.Observability;
using FireLakeLabs.NetClaw.Application.Routing;
using FireLakeLabs.NetClaw.Application.Scheduling;
using FireLakeLabs.NetClaw.Domain.Contracts.Agents;
using FireLakeLabs.NetClaw.Domain.Contracts.Channels;
using FireLakeLabs.NetClaw.Domain.Contracts.Containers;
using FireLakeLabs.NetClaw.Domain.Contracts.Persistence;
using FireLakeLabs.NetClaw.Domain.Contracts.Services;
using FireLakeLabs.NetClaw.Domain.Entities;
using FireLakeLabs.NetClaw.Domain.Enums;
using FireLakeLabs.NetClaw.Domain.ValueObjects;
using FireLakeLabs.NetClaw.Host.Configuration;
using FireLakeLabs.NetClaw.Host.Services;
using FireLakeLabs.NetClaw.Infrastructure.Channels;
using FireLakeLabs.NetClaw.Infrastructure.Configuration;
using FireLakeLabs.NetClaw.Infrastructure.FileSystem;
using FireLakeLabs.NetClaw.Infrastructure.Ipc;
using FireLakeLabs.NetClaw.Infrastructure.Paths;
using FireLakeLabs.NetClaw.Infrastructure.Persistence.Sqlite;
using FireLakeLabs.NetClaw.Infrastructure.Runtime;
using FireLakeLabs.NetClaw.Infrastructure.Runtime.Agents;
using FireLakeLabs.NetClaw.Infrastructure.Security;

namespace FireLakeLabs.NetClaw.Host;

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

        AgentRuntimeOptions agentRuntimeOptions = CreateAgentRuntimeOptions(configuration, hostPathOptions);
        agentRuntimeOptions.Validate();

        IpcWatcherOptions ipcWatcherOptions = CreateIpcWatcherOptions(configuration);
        ipcWatcherOptions.Validate();

        MessageLoopOptions messageLoopOptions = CreateMessageLoopOptions(configuration);
        messageLoopOptions.Validate();

        SchedulerOptions schedulerOptions = CreateSchedulerOptions(configuration);
        schedulerOptions.Validate();

        ChannelWorkerOptions channelWorkerOptions = CreateChannelWorkerOptions(configuration);
        channelWorkerOptions.Validate();

        ReferenceFileChannelOptions referenceFileChannelOptions = CreateReferenceFileChannelOptions(configuration, hostPathOptions);
        referenceFileChannelOptions.Validate();

        TerminalChannelOptions terminalChannelOptions = CreateTerminalChannelOptions(configuration);
        terminalChannelOptions.Validate();

        SlackChannelOptions slackChannelOptions = CreateSlackChannelOptions(configuration, assistantIdentityOptions);
        slackChannelOptions.Validate();

        services.AddSingleton(hostPathOptions);
        services.AddSingleton(storageOptions);
        services.AddSingleton(assistantIdentityOptions);
        services.AddSingleton(credentialProxyOptions);
        services.AddSingleton(containerRuntimeOptions);
        services.AddSingleton(agentRuntimeOptions);
        services.AddSingleton(ipcWatcherOptions);
        services.AddSingleton(messageLoopOptions);
        services.AddSingleton(schedulerOptions);
        services.AddSingleton(channelWorkerOptions);
        services.AddSingleton(referenceFileChannelOptions);
        services.AddSingleton(terminalChannelOptions);
        services.AddSingleton(slackChannelOptions);

        services.AddSingleton<IFileSystem, PhysicalFileSystem>();
        services.AddSingleton<GroupPathResolver>();
        services.AddSingleton<MountAllowlistLoader>();
        services.AddSingleton<MountSecurityValidator>();
        services.AddSingleton<SenderAllowlistService>();
        services.AddSingleton<ISenderAuthorizationService>(serviceProvider => serviceProvider.GetRequiredService<SenderAllowlistService>());
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
        services.AddSingleton<IAgentEventRepository, SqliteAgentEventRepository>();
        services.AddSingleton<IFileAttachmentRepository, SqliteFileAttachmentRepository>();
        services.AddSingleton<IAgentEventSink, AgentEventSink>();
        services.AddSingleton<IContainerRuntime, DockerContainerRuntime>();
        services.AddSingleton<IAgentWorkspaceBuilder, NetClawAgentWorkspaceBuilder>();
        services.AddSingleton<IAgentToolRegistry, NetClawAgentToolRegistry>();
        services.AddSingleton<ICopilotToolFactory, NetClawCopilotToolFactory>();
        services.AddSingleton<ICopilotClientAdapterFactory, SdkCopilotClientAdapterFactory>();
        services.AddSingleton<ICopilotClientPool, CopilotClientPool>();
        services.AddSingleton<ICredentialProxyService, CredentialProxyService>();
        services.AddSingleton<ICodingAgentEngine, CopilotCodingAgentEngine>();
        services.AddSingleton<ICodingAgentEngine, ClaudeCodePlaceholderEngine>();
        services.AddSingleton<ICodingAgentEngine, CodexPlaceholderEngine>();
        services.AddSingleton<ICodingAgentEngine, OpenCodePlaceholderEngine>();
        services.AddSingleton<ContainerizedAgentEngine>();
        services.AddSingleton<IContainerExecutionService, ContainerExecutionService>();
        services.AddSingleton<IAgentRuntime, NetClawAgentRuntime>();
        services.AddSingleton<IIpcCommandWatcher, FileSystemIpcWatcher>();

        if (referenceFileChannelOptions.Enabled)
        {
            services.AddSingleton<IChannel, ReferenceFileChannel>();
        }

        if (terminalChannelOptions.Enabled)
        {
            services.AddSingleton<IChannel, TerminalChannel>();
        }

        if (slackChannelOptions.Enabled)
        {
            services.AddSingleton<ISlackSocketModeClient, SlackSocketModeClient>();
            services.AddSingleton<IChannel, SlackChannel>();
        }

        services.AddSingleton<IReadOnlyList<IChannel>>(serviceProvider => serviceProvider.GetServices<IChannel>().ToArray());
        services.AddSingleton<IMessageNotifier, NullMessageNotifier>();
        services.AddSingleton<ChannelIngressService>();
        services.AddSingleton<IMessageFormatter, XmlMessageFormatter>();
        services.AddSingleton<IOutboundRouter, ChannelOutboundRouter>();
        services.AddSingleton<ActiveGroupSessionRegistry>();
        services.AddSingleton<InboundMessagePollingService>(serviceProvider => new InboundMessagePollingService(
            serviceProvider.GetRequiredService<IMessageRepository>(),
            serviceProvider.GetRequiredService<IGroupRepository>(),
            serviceProvider.GetRequiredService<IRouterStateRepository>(),
            serviceProvider.GetRequiredService<IReadOnlyList<IChannel>>(),
            serviceProvider.GetRequiredService<ISenderAuthorizationService>(),
            serviceProvider.GetRequiredService<IMessageFormatter>(),
            serviceProvider.GetRequiredService<AssistantIdentityOptions>().Name,
            serviceProvider.GetRequiredService<MessageLoopOptions>().Timezone,
            serviceProvider.GetRequiredService<IGroupExecutionQueue>()));
        services.AddSingleton<GroupMessageProcessorService>(serviceProvider => new GroupMessageProcessorService(
            serviceProvider.GetRequiredService<IMessageRepository>(),
            serviceProvider.GetRequiredService<IGroupRepository>(),
            serviceProvider.GetRequiredService<IRouterStateRepository>(),
            serviceProvider.GetRequiredService<ISenderAuthorizationService>(),
            serviceProvider.GetRequiredService<IMessageFormatter>(),
            serviceProvider.GetRequiredService<IOutboundRouter>(),
            serviceProvider.GetRequiredService<IAgentRuntime>(),
            serviceProvider.GetRequiredService<IGroupExecutionQueue>(),
            serviceProvider.GetRequiredService<ActiveGroupSessionRegistry>(),
            serviceProvider.GetRequiredService<IReadOnlyList<IChannel>>(),
            serviceProvider.GetRequiredService<IAgentEventSink>(),
            serviceProvider.GetRequiredService<IFileAttachmentRepository>(),
            serviceProvider.GetRequiredService<AssistantIdentityOptions>().Name,
            serviceProvider.GetRequiredService<MessageLoopOptions>().Timezone,
            serviceProvider.GetRequiredService<StorageOptions>().GroupsDirectory,
            serviceProvider.GetRequiredService<ILoggerFactory>().CreateLogger<GroupMessageProcessorService>()));
        services.AddSingleton(serviceProvider =>
        {
            GroupExecutionQueue queue = new(maxConcurrentExecutions: 1);
            queue.SetMessageProcessor((groupJid, cancellationToken) => serviceProvider.GetRequiredService<GroupMessageProcessorService>().ProcessAsync(groupJid, cancellationToken));
            queue.SetInputHandlers(
                (groupJid, text) => serviceProvider.GetRequiredService<ActiveGroupSessionRegistry>().TryPostInput(groupJid, text),
                groupJid => serviceProvider.GetRequiredService<ActiveGroupSessionRegistry>().RequestClose(groupJid));
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
        services.AddHostedService<CredentialProxyWorker>();
        services.AddHostedService<ChannelWorker>();
        services.AddHostedService<MessageLoopWorker>();
        services.AddHostedService<IpcWatcherWorker>();
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
            Port = port,
            CopilotUpstreamUrl = configuration["NetClaw:CredentialProxy:CopilotUpstreamUrl"] ?? "https://api.githubcopilot.com",
            ClaudeUpstreamUrl = configuration["NetClaw:CredentialProxy:ClaudeUpstreamUrl"] ?? "https://api.anthropic.com",
            AuthMode = configuration["NetClaw:CredentialProxy:AuthMode"] ?? "api-key"
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

    private static AgentRuntimeOptions CreateAgentRuntimeOptions(IConfiguration configuration, HostPathOptions hostPathOptions)
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
            CopilotCliPath = configuration["NetClaw:AgentRuntime:CopilotCliPath"] ?? "copilot",
            CopilotConfigDirectory = configuration["NetClaw:AgentRuntime:CopilotConfigDirectory"] ?? Path.Combine(hostPathOptions.ProjectRoot, "data", "copilot"),
            CopilotCliUrl = configuration["NetClaw:AgentRuntime:CopilotCliUrl"],
            CopilotLogLevel = configuration["NetClaw:AgentRuntime:CopilotLogLevel"] ?? "info",
            CopilotUseStdio = !bool.TryParse(configuration["NetClaw:AgentRuntime:CopilotUseStdio"], out bool useStdio) || useStdio,
            CopilotAutoStart = !bool.TryParse(configuration["NetClaw:AgentRuntime:CopilotAutoStart"], out bool autoStart) || autoStart,
            CopilotAutoRestart = !bool.TryParse(configuration["NetClaw:AgentRuntime:CopilotAutoRestart"], out bool autoRestart) || autoRestart,
            CopilotGitHubToken = configuration["NetClaw:AgentRuntime:CopilotGitHubToken"],
            CopilotUseLoggedInUser = bool.TryParse(configuration["NetClaw:AgentRuntime:CopilotUseLoggedInUser"], out bool useLoggedInUser)
                ? useLoggedInUser
                : null,
            CopilotClientName = configuration["NetClaw:AgentRuntime:CopilotClientName"] ?? "NetClaw",
            CopilotModel = configuration["NetClaw:AgentRuntime:CopilotModel"] ?? "gpt-5",
            InteractiveIdleTimeout = TimeSpan.TryParse(configuration["NetClaw:AgentRuntime:InteractiveIdleTimeout"], out TimeSpan interactiveIdleTimeout)
                ? interactiveIdleTimeout
                : TimeSpan.FromSeconds(30),
            CopilotReasoningEffort = configuration["NetClaw:AgentRuntime:CopilotReasoningEffort"],
            CopilotStreaming = !bool.TryParse(configuration["NetClaw:AgentRuntime:CopilotStreaming"], out bool streaming) || streaming,
            CopilotEnableInfiniteSessions = !bool.TryParse(configuration["NetClaw:AgentRuntime:CopilotEnableInfiniteSessions"], out bool infiniteSessions) || infiniteSessions,
            CopilotBackgroundCompactionThreshold = double.TryParse(configuration["NetClaw:AgentRuntime:CopilotBackgroundCompactionThreshold"], out double backgroundThreshold)
                ? backgroundThreshold
                : null,
            CopilotBufferExhaustionThreshold = double.TryParse(configuration["NetClaw:AgentRuntime:CopilotBufferExhaustionThreshold"], out double bufferThreshold)
                ? bufferThreshold
                : null
        };
    }

    private static IpcWatcherOptions CreateIpcWatcherOptions(IConfiguration configuration)
    {
        TimeSpan pollInterval = TimeSpan.FromSeconds(2);
        if (TimeSpan.TryParse(configuration["NetClaw:Ipc:PollInterval"], out TimeSpan configuredPollInterval))
        {
            pollInterval = configuredPollInterval;
        }

        return new IpcWatcherOptions
        {
            PollInterval = pollInterval
        };
    }

    private static ChannelWorkerOptions CreateChannelWorkerOptions(IConfiguration configuration)
    {
        TimeSpan pollInterval = TimeSpan.FromSeconds(2);
        if (TimeSpan.TryParse(configuration["NetClaw:Channels:PollInterval"], out TimeSpan configuredPollInterval))
        {
            pollInterval = configuredPollInterval;
        }

        return new ChannelWorkerOptions
        {
            PollInterval = pollInterval,
            InitialSyncOnStart = !bool.TryParse(configuration["NetClaw:Channels:InitialSyncOnStart"], out bool initialSyncOnStart) || initialSyncOnStart
        };
    }

    private static ReferenceFileChannelOptions CreateReferenceFileChannelOptions(IConfiguration configuration, HostPathOptions hostPathOptions)
    {
        return new ReferenceFileChannelOptions
        {
            Enabled = bool.TryParse(configuration["NetClaw:Channels:ReferenceFile:Enabled"], out bool enabled) && enabled,
            RootDirectory = configuration["NetClaw:Channels:ReferenceFile:RootDirectory"]
                ?? Path.Combine(hostPathOptions.ProjectRoot, "data", "channels", "reference-file"),
            ClaimAllChats = !bool.TryParse(configuration["NetClaw:Channels:ReferenceFile:ClaimAllChats"], out bool claimAllChats) || claimAllChats
        };
    }

    private static TerminalChannelOptions CreateTerminalChannelOptions(IConfiguration configuration)
    {
        return new TerminalChannelOptions
        {
            Enabled = bool.TryParse(configuration["NetClaw:Channels:Terminal:Enabled"], out bool enabled) && enabled,
            ChatJid = configuration["NetClaw:Channels:Terminal:ChatJid"] ?? "terminal@local",
            Sender = configuration["NetClaw:Channels:Terminal:Sender"] ?? "terminal-user",
            SenderName = configuration["NetClaw:Channels:Terminal:SenderName"] ?? "Terminal User",
            ChatName = configuration["NetClaw:Channels:Terminal:ChatName"] ?? "Terminal",
            IsGroup = bool.TryParse(configuration["NetClaw:Channels:Terminal:IsGroup"], out bool isGroup) && isGroup,
            OutboundPrefix = configuration["NetClaw:Channels:Terminal:OutboundPrefix"] ?? "assistant> ",
            InputPrompt = configuration["NetClaw:Channels:Terminal:InputPrompt"] ?? "you> "
        };
    }

    private static SlackChannelOptions CreateSlackChannelOptions(IConfiguration configuration, AssistantIdentityOptions assistantIdentityOptions)
    {
        return new SlackChannelOptions
        {
            Enabled = bool.TryParse(configuration["NetClaw:Channels:Slack:Enabled"], out bool enabled) && enabled,
            BotToken = configuration["NetClaw:Channels:Slack:BotToken"] ?? string.Empty,
            AppToken = configuration["NetClaw:Channels:Slack:AppToken"] ?? string.Empty,
            ApiBaseUrl = configuration["NetClaw:Channels:Slack:ApiBaseUrl"] ?? "https://slack.com/api",
            MentionReplacement = configuration["NetClaw:Channels:Slack:MentionReplacement"] ?? $"@{assistantIdentityOptions.Name}",
            WorkingIndicatorText = configuration["NetClaw:Channels:Slack:WorkingIndicatorText"] ?? "Evaluating...",
            ReplyInThreadByDefault = !bool.TryParse(configuration["NetClaw:Channels:Slack:ReplyInThreadByDefault"], out bool replyInThreadByDefault) || replyInThreadByDefault
        };
    }

    private static MessageLoopOptions CreateMessageLoopOptions(IConfiguration configuration)
    {
        TimeSpan pollInterval = TimeSpan.FromSeconds(2);
        if (TimeSpan.TryParse(configuration["NetClaw:MessageLoop:PollInterval"], out TimeSpan configuredPollInterval))
        {
            pollInterval = configuredPollInterval;
        }

        return new MessageLoopOptions
        {
            PollInterval = pollInterval,
            Timezone = configuration["NetClaw:MessageLoop:Timezone"] ?? "UTC"
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
