using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NetClaw.Domain.Contracts.Agents;
using NetClaw.Domain.Contracts.Persistence;
using NetClaw.Domain.Contracts.Services;
using NetClaw.Infrastructure.Configuration;
using NetClaw.Infrastructure.Runtime;

namespace NetClaw.Host.Tests;

public sealed class ProgramTests
{
    [Fact]
    public async Task CreateHostBuilder_BuildsAndStartsHost()
    {
        string projectRoot = Path.Combine(Path.GetTempPath(), $"netclaw-host-{Guid.NewGuid():N}");
        try
        {
            using IHost host = NetClaw.Host.Program.CreateHostBuilder([])
                .ConfigureAppConfiguration(configurationBuilder =>
                {
                    configurationBuilder.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["NetClaw:ProjectRoot"] = projectRoot,
                        ["NetClaw:Scheduler:PollInterval"] = "00:00:30"
                    });
                })
                .Build();

            await host.StartAsync();

            Assert.True(Directory.Exists(Path.Combine(projectRoot, "store")));
            Assert.True(Directory.Exists(Path.Combine(projectRoot, "groups")));
            Assert.True(Directory.Exists(Path.Combine(projectRoot, "data", "ipc")));
            Assert.True(File.Exists(Path.Combine(projectRoot, "data", "netclaw.db")));

            await host.StopAsync();
        }
        finally
        {
            if (Directory.Exists(projectRoot))
            {
                Directory.Delete(projectRoot, recursive: true);
            }
        }
    }

    [Fact]
    public async Task CreateHostBuilder_RegistersCoreServices()
    {
        string projectRoot = Path.Combine(Path.GetTempPath(), $"netclaw-host-services-{Guid.NewGuid():N}");
        try
        {
            using IHost host = NetClaw.Host.Program.CreateHostBuilder([])
                .ConfigureAppConfiguration(configurationBuilder =>
                {
                    configurationBuilder.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["NetClaw:ProjectRoot"] = projectRoot,
                        ["NetClaw:Assistant:Name"] = "NetClaw",
                        ["NetClaw:Ipc:PollInterval"] = "00:00:05",
                        ["NetClaw:MessageLoop:PollInterval"] = "00:00:07",
                        ["NetClaw:MessageLoop:Timezone"] = "UTC",
                        ["NetClaw:Scheduler:PollInterval"] = "00:00:45"
                    });
                })
                .Build();

            await host.StartAsync();

            Assert.NotNull(host.Services.GetService<IMessageRepository>());
            Assert.NotNull(host.Services.GetService<IGroupRepository>());
            Assert.NotNull(host.Services.GetService<ISessionRepository>());
            Assert.NotNull(host.Services.GetService<ITaskRepository>());
            Assert.NotNull(host.Services.GetService<IRouterStateRepository>());
            Assert.NotNull(host.Services.GetService<IMessageFormatter>());
            Assert.NotNull(host.Services.GetService<IOutboundRouter>());
            Assert.NotNull(host.Services.GetService<IGroupExecutionQueue>());
            Assert.NotNull(host.Services.GetService<NetClaw.Application.Execution.InboundMessagePollingService>());
            Assert.NotNull(host.Services.GetService<NetClaw.Application.Execution.GroupMessageProcessorService>());
            Assert.NotNull(host.Services.GetService<ITaskSchedulerService>());
            Assert.NotNull(host.Services.GetService<IIpcCommandProcessor>());
            Assert.NotNull(host.Services.GetService<IIpcCommandWatcher>());
            Assert.NotNull(host.Services.GetService<IContainerRuntime>());
            Assert.NotNull(host.Services.GetService<IAgentRuntime>());
            Assert.NotNull(host.Services.GetService<IAgentWorkspaceBuilder>());
            Assert.NotNull(host.Services.GetService<IAgentToolRegistry>());
            Assert.True(host.Services.GetServices<ICodingAgentEngine>().Any());
            Assert.Equal("NetClaw", host.Services.GetRequiredService<AssistantIdentityOptions>().Name);
            Assert.Equal(TimeSpan.FromSeconds(5), host.Services.GetRequiredService<IpcWatcherOptions>().PollInterval);
            Assert.Equal(TimeSpan.FromSeconds(7), host.Services.GetRequiredService<MessageLoopOptions>().PollInterval);
            Assert.Equal("UTC", host.Services.GetRequiredService<MessageLoopOptions>().Timezone);
            Assert.Equal(TimeSpan.FromSeconds(45), host.Services.GetRequiredService<SchedulerOptions>().PollInterval);
            Assert.Equal(NetClaw.Domain.Enums.AgentProviderKind.Copilot, host.Services.GetRequiredService<AgentRuntimeOptions>().GetDefaultProvider());
            Assert.NotNull(host.Services.GetRequiredService<PlatformInfo>());

            await host.StopAsync();
        }
        finally
        {
            if (Directory.Exists(projectRoot))
            {
                Directory.Delete(projectRoot, recursive: true);
            }
        }
    }
}