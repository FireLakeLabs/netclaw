using FireLakeLabs.NetClaw.Domain.Entities;
using FireLakeLabs.NetClaw.Domain.ValueObjects;

namespace FireLakeLabs.NetClaw.Domain.Tests.Entities;

public sealed class RegisteredGroupTests
{
    [Fact]
    public void Constructor_ForMainGroup_DisablesTriggerRequirement()
    {
        RegisteredGroup group = new(
            name: "Main",
            folder: new GroupFolder("main"),
            trigger: "@assistant",
            addedAt: DateTimeOffset.UtcNow,
            requiresTrigger: true,
            isMain: true);

        Assert.True(group.IsMain);
        Assert.False(group.RequiresTrigger);
    }

    [Fact]
    public void Constructor_RejectsBlankName()
    {
        Assert.Throws<ArgumentException>(
            () => new RegisteredGroup(" ", new GroupFolder("team"), "@assistant", DateTimeOffset.UtcNow));
    }

    [Fact]
    public void Constructor_PreservesContainerConfiguration()
    {
        ContainerConfig containerConfig = new([new AdditionalMount("/tmp")], TimeSpan.FromMinutes(5));
        RegisteredGroup group = new("Team", new GroupFolder("team"), "@assistant", DateTimeOffset.UtcNow, containerConfig);

        Assert.Equal(containerConfig, group.ContainerConfig);
    }
}
