using System.Text.Json;
using NetClaw.Domain.Contracts.Channels;
using NetClaw.Domain.ValueObjects;
using NetClaw.Infrastructure.Channels;
using NetClaw.Infrastructure.Configuration;

namespace NetClaw.Infrastructure.Tests.Channels;

public sealed class ReferenceFileChannelTests
{
    [Fact]
    public async Task PollInboundAsync_EmitsMetadataAndMessageAndMovesFileToProcessed()
    {
        string root = CreateTemporaryPath();

        try
        {
            ReferenceFileChannel channel = new(new ReferenceFileChannelOptions
            {
                Enabled = true,
                RootDirectory = root,
                ClaimAllChats = false
            });
            await channel.ConnectAsync();

            string inboxDirectory = Path.Combine(root, "inbox");
            string messagePath = Path.Combine(inboxDirectory, "message.json");
            await File.WriteAllTextAsync(messagePath,
                """
                {
                  "id": "message-1",
                  "chatJid": "team@jid",
                  "sender": "sender-1",
                  "senderName": "User",
                  "content": "hello",
                  "timestamp": "2026-03-10T00:00:00Z",
                  "chatName": "Team",
                  "isGroup": true
                }
                """);

            List<ChannelMetadataEvent> metadataEvents = [];
            List<ChannelMessage> messages = [];
            await channel.PollInboundAsync(
                (message, _) =>
                {
                    messages.Add(message);
                    return Task.CompletedTask;
                },
                (metadata, _) =>
                {
                    metadataEvents.Add(metadata);
                    return Task.CompletedTask;
                });

            Assert.Single(metadataEvents);
            Assert.Equal("Team", metadataEvents[0].Name);
            Assert.Single(messages);
            Assert.Equal("hello", messages[0].Message.Content);
            Assert.True(channel.Owns(new ChatJid("team@jid")));
            Assert.False(File.Exists(messagePath));
            Assert.Single(Directory.GetFiles(Path.Combine(root, "processed"), "*.json"));
        }
        finally
        {
            DeleteTemporaryPath(root);
        }
    }

    [Fact]
    public async Task SendMessageAsync_WritesOutboundEnvelope()
    {
        string root = CreateTemporaryPath();

        try
        {
            ReferenceFileChannel channel = new(new ReferenceFileChannelOptions
            {
                Enabled = true,
                RootDirectory = root,
                ClaimAllChats = true
            });
            await channel.ConnectAsync();

            await channel.SendMessageAsync(new ChatJid("team@jid"), "assistant reply");

            string outboxFile = Assert.Single(Directory.GetFiles(Path.Combine(root, "outbox"), "*.json"));
            using JsonDocument document = JsonDocument.Parse(await File.ReadAllTextAsync(outboxFile));
            Assert.Equal("team@jid", document.RootElement.GetProperty("chatJid").GetString());
            Assert.Equal("assistant reply", document.RootElement.GetProperty("text").GetString());
        }
        finally
        {
            DeleteTemporaryPath(root);
        }
    }

    private static string CreateTemporaryPath()
    {
        string path = Path.Combine(Path.GetTempPath(), $"netclaw-reference-channel-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }

    private static void DeleteTemporaryPath(string path)
    {
        if (Directory.Exists(path))
        {
            Directory.Delete(path, recursive: true);
        }
    }
}
