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
    public void CredentialProxyOptions_RejectsInvalidPort()
    {
        CredentialProxyOptions options = new() { Port = 70000 };

        Assert.Throws<InvalidOperationException>(() => options.Validate());
    }
}