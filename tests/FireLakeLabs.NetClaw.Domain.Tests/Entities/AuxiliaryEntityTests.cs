using FireLakeLabs.NetClaw.Domain.Entities;
using FireLakeLabs.NetClaw.Domain.ValueObjects;

namespace FireLakeLabs.NetClaw.Domain.Tests.Entities;

public sealed class AuxiliaryEntityTests
{
    [Fact]
    public void ChatInfo_RejectsBlankName()
    {
        Assert.Throws<ArgumentException>(
            () => new ChatInfo(new ChatJid("chat@jid"), " ", DateTimeOffset.UtcNow, new ChannelName("whatsapp"), true));
    }

    [Fact]
    public void RouterStateEntry_RequiresKey()
    {
        Assert.Throws<ArgumentException>(() => new RouterStateEntry(" ", "value"));
    }

    [Fact]
    public void ContainerConfig_RejectsNonPositiveTimeout()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new ContainerConfig(timeout: TimeSpan.Zero));
    }

    [Fact]
    public void SessionState_PreservesIdentifiers()
    {
        SessionState session = new(new GroupFolder("team"), new SessionId("session-1"));

        Assert.Equal("team", session.GroupFolder.Value);
        Assert.Equal("session-1", session.SessionId.Value);
    }

    [Fact]
    public void MountAllowlist_PreservesConfiguration()
    {
        MountAllowlist allowlist = new(
            [new AllowedRoot("~/projects", allowReadWrite: true, description: "Projects")],
            [".ssh", ".env"],
            nonMainReadOnly: true);

        Assert.Single(allowlist.AllowedRoots);
        Assert.Equal(2, allowlist.BlockedPatterns.Count);
        Assert.True(allowlist.NonMainReadOnly);
    }
}
