using NetClaw.Domain.ValueObjects;

namespace NetClaw.Domain.Tests.ValueObjects;

public sealed class GroupFolderTests
{
    [Theory]
    [InlineData("main")]
    [InlineData("group_01")]
    [InlineData("Team-Alpha")]
    public void Constructor_AcceptsValidFolders(string value)
    {
        GroupFolder folder = new(value);

        Assert.Equal(value, folder.Value);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(" global")]
    [InlineData("global")]
    [InlineData("folder/name")]
    [InlineData("folder\\name")]
    [InlineData("../escape")]
    [InlineData("has space")]
    public void Constructor_RejectsInvalidFolders(string value)
    {
        Assert.Throws<ArgumentException>(() => new GroupFolder(value));
    }
}
