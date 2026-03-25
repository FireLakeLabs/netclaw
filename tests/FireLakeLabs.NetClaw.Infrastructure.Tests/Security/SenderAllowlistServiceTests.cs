using FireLakeLabs.NetClaw.Domain.Entities;
using FireLakeLabs.NetClaw.Domain.ValueObjects;
using FireLakeLabs.NetClaw.Infrastructure.FileSystem;
using FireLakeLabs.NetClaw.Infrastructure.Security;

namespace FireLakeLabs.NetClaw.Infrastructure.Tests.Security;

public sealed class SenderAllowlistServiceTests
{
    [Fact]
    public async Task LoadAsync_UsesDefaultTriggerModeWhenFileIsMissing()
    {
        SenderAllowlistService service = new(new PhysicalFileSystem());
        ChatJid chatJid = new("team@jid");
        StoredMessage message = new("message-1", chatJid, "sender-1", "User", "hello", DateTimeOffset.UtcNow);

        await service.LoadAsync(Path.Combine(Path.GetTempPath(), $"missing-sender-allowlist-{Guid.NewGuid():N}.json"));

        Assert.True(service.CanTrigger(chatJid, message));
        Assert.Single(service.ApplyInboundPolicy(chatJid, [message]));
    }

    [Fact]
    public async Task LoadAsync_ParsesDropModeAndFiltersDeniedSenders()
    {
        PhysicalFileSystem fileSystem = new();
        SenderAllowlistService service = new(fileSystem);
        string directory = Path.Combine(fileSystem.GetTempPath(), $"netclaw-sender-allowlist-{Guid.NewGuid():N}");
        string filePath = Path.Combine(directory, "sender-allowlist.json");
        ChatJid chatJid = new("team@jid");
        StoredMessage allowed = new("message-1", chatJid, "allowed", "Allowed", "hello", DateTimeOffset.UtcNow);
        StoredMessage denied = new("message-2", chatJid, "blocked", "Blocked", "hello", DateTimeOffset.UtcNow);

        try
        {
            fileSystem.CreateDirectory(directory);
            await fileSystem.WriteAllTextAsync(filePath, """
            {
              "default": { "allow": "*", "mode": "trigger" },
              "chats": {
                "team@jid": { "allow": ["allowed"], "mode": "drop" }
              },
              "logDenied": true
            }
            """);

            await service.LoadAsync(filePath);

            IReadOnlyList<StoredMessage> filtered = service.ApplyInboundPolicy(chatJid, [allowed, denied]);

            Assert.Single(filtered);
            Assert.Equal("allowed", filtered[0].Sender);
            Assert.True(service.CanTrigger(chatJid, allowed));
            Assert.False(service.CanTrigger(chatJid, denied));
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }
}
