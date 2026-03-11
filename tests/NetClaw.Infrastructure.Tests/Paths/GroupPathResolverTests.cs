using NetClaw.Domain.ValueObjects;
using NetClaw.Infrastructure.Configuration;
using NetClaw.Infrastructure.FileSystem;
using NetClaw.Infrastructure.Paths;

namespace NetClaw.Infrastructure.Tests.Paths;

public sealed class GroupPathResolverTests
{
    [Fact]
    public void ResolveGroupDirectory_ReturnsPathWithinGroupsRoot()
    {
        StorageOptions options = StorageOptions.Create("/tmp/netclaw");
        GroupPathResolver resolver = new(options, new PhysicalFileSystem());

        string groupPath = resolver.ResolveGroupDirectory(new GroupFolder("team"));

        Assert.Equal("/tmp/netclaw/groups/team", groupPath);
    }

    [Fact]
    public void ResolveGroupIpcDirectory_ReturnsPathWithinDataRoot()
    {
        StorageOptions options = StorageOptions.Create("/tmp/netclaw");
        GroupPathResolver resolver = new(options, new PhysicalFileSystem());

        string ipcPath = resolver.ResolveGroupIpcDirectory(new GroupFolder("team"));

        Assert.Equal("/tmp/netclaw/data/ipc/team", ipcPath);
    }

    [Fact]
    public void ResolveGroupSessionDirectory_ReturnsPathWithinSessionRoot()
    {
        StorageOptions options = StorageOptions.Create("/tmp/netclaw");
        GroupPathResolver resolver = new(options, new PhysicalFileSystem());

        string sessionPath = resolver.ResolveGroupSessionDirectory(new GroupFolder("team"));

        Assert.Equal("/tmp/netclaw/data/sessions/team", sessionPath);
    }

    [Fact]
    public void ResolveGroupAgentWorkspaceDirectory_ReturnsPathWithinWorkspaceRoot()
    {
        StorageOptions options = StorageOptions.Create("/tmp/netclaw");
        GroupPathResolver resolver = new(options, new PhysicalFileSystem());

        string workspacePath = resolver.ResolveGroupAgentWorkspaceDirectory(new GroupFolder("team"));

        Assert.Equal("/tmp/netclaw/data/agent-workspaces/team", workspacePath);
    }
}
