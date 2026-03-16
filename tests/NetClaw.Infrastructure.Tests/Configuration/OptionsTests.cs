using NetClaw.Infrastructure.Configuration;

namespace NetClaw.Infrastructure.Tests.Configuration;

public sealed class OptionsTests
{
    [Fact]
    public void StorageOptions_CreateBuildsExpectedPaths()
    {
        StorageOptions options = StorageOptions.Create("/tmp/netclaw");

        Assert.Equal("/tmp/netclaw", options.ProjectRoot);
        Assert.Equal("/tmp/netclaw/store", options.StoreDirectory);
        Assert.Equal("/tmp/netclaw/groups", options.GroupsDirectory);
        Assert.Equal("/tmp/netclaw/data", options.DataDirectory);
    }

    [Fact]
    public void SchedulerOptions_RejectsNonPositiveIntervals()
    {
        SchedulerOptions options = new() { PollInterval = TimeSpan.Zero };

        Assert.Throws<InvalidOperationException>(() => options.Validate());
    }

    [Fact]
    public void IpcWatcherOptions_RejectsNonPositiveIntervals()
    {
        IpcWatcherOptions options = new() { PollInterval = TimeSpan.Zero };

        Assert.Throws<InvalidOperationException>(() => options.Validate());
    }

    [Fact]
    public void MessageLoopOptions_RejectsMissingTimezone()
    {
        MessageLoopOptions options = new() { Timezone = " " };

        Assert.Throws<InvalidOperationException>(() => options.Validate());
    }

    [Fact]
    public void CredentialProxyOptions_RejectsInvalidPort()
    {
        CredentialProxyOptions options = new() { Port = 70000 };

        Assert.Throws<InvalidOperationException>(() => options.Validate());
    }

    [Fact]
    public void AgentRuntimeOptions_ParseConfiguredProvider()
    {
        AgentRuntimeOptions options = new()
        {
            DefaultProvider = "copilot",
            CopilotConfigDirectory = "/tmp/netclaw/copilot"
        };

        options.Validate();

        Assert.Equal(NetClaw.Domain.Enums.AgentProviderKind.Copilot, options.GetDefaultProvider());
    }

    [Fact]
    public void AgentRuntimeOptions_RejectUnsupportedProvider()
    {
        AgentRuntimeOptions options = new() { DefaultProvider = "unknown", CopilotConfigDirectory = "/tmp/netclaw/copilot" };

        Assert.Throws<InvalidOperationException>(() => options.Validate());
    }

    [Fact]
    public void AgentRuntimeOptions_RejectInvalidThresholds()
    {
        AgentRuntimeOptions options = new()
        {
            CopilotBackgroundCompactionThreshold = 0.95,
            CopilotBufferExhaustionThreshold = 0.90,
            CopilotConfigDirectory = "/tmp/netclaw/copilot"
        };

        Assert.Throws<InvalidOperationException>(() => options.Validate());
    }

    [Fact]
    public void AgentRuntimeOptions_RejectExternalCliAuthConflict()
    {
        AgentRuntimeOptions options = new()
        {
            CopilotCliUrl = "http://127.0.0.1:3000",
            CopilotGitHubToken = "token",
            CopilotConfigDirectory = "/tmp/netclaw/copilot"
        };

        Assert.Throws<InvalidOperationException>(() => options.Validate());
    }
}
