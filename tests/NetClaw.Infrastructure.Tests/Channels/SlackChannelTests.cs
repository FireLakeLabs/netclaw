using System.Threading.Channels;
using NetClaw.Domain.Contracts.Channels;
using NetClaw.Domain.ValueObjects;
using NetClaw.Infrastructure.Channels;
using NetClaw.Infrastructure.Configuration;

namespace NetClaw.Infrastructure.Tests.Channels;

public sealed class SlackChannelTests
{
    [Fact]
    public async Task PollInboundAsync_AcknowledgesEnvelopeAndNormalizesMention()
    {
        FakeSlackSocketModeClient client = new("U-BOT", new SlackConversationInfo("C12345", "general", true));
        client.Connection.Enqueue(new SlackSocketEnvelope(
            "envelope-1",
            "events_api",
            new SlackSocketPayload(
                "events_api",
                new SlackEventPayload(
                    "message",
                    "C12345",
                    "channel",
                    "U-USER",
                    "<@U-BOT> hello",
                    "1710115200.000100",
                    null,
                    "client-message-1",
                    null,
                    null))));

        SlackChannel channel = new(
            new SlackChannelOptions
            {
                Enabled = true,
                BotToken = "xoxb-test",
                AppToken = "xapp-test",
                MentionReplacement = "@Andy",
                WorkingIndicatorText = "Evaluating..."
            },
            client);

        await channel.ConnectAsync();
        await Task.Delay(50);

        List<ChannelMetadataEvent> metadata = [];
        List<ChannelMessage> messages = [];
        await channel.PollInboundAsync(
            (message, _) =>
            {
                messages.Add(message);
                return Task.CompletedTask;
            },
            (eventData, _) =>
            {
                metadata.Add(eventData);
                return Task.CompletedTask;
            });

        Assert.Equal(["envelope-1"], client.Connection.AcknowledgedEnvelopeIds);
        Assert.Single(metadata);
        Assert.Equal("general", metadata[0].Name);
        Assert.True(metadata[0].IsGroup);
        Assert.Single(messages);
        Assert.Equal("@Andy hello", messages[0].Message.Content);
        Assert.True(channel.Owns(new ChatJid("C12345")));

        await channel.DisconnectAsync();
    }

    [Fact]
    public async Task SetTypingAsync_PostsPlaceholderAndSendMessageUpdatesIt()
    {
        FakeSlackSocketModeClient client = new("U-BOT", new SlackConversationInfo("C12345", "general", true));
        client.Connection.Enqueue(new SlackSocketEnvelope(
            "envelope-1",
            "events_api",
            new SlackSocketPayload(
                "events_api",
                new SlackEventPayload(
                    "message",
                    "C12345",
                    "channel",
                    "U-USER",
                    "<@U-BOT> hello",
                    "1710115200.000100",
                    null,
                    "client-message-1",
                    null,
                    null))));

        SlackChannel channel = new(
            new SlackChannelOptions
            {
                Enabled = true,
                BotToken = "xoxb-test",
                AppToken = "xapp-test",
                WorkingIndicatorText = "Evaluating..."
            },
            client);

        await channel.ConnectAsync();
        await Task.Delay(50);
        await channel.PollInboundAsync((_, _) => Task.CompletedTask, (_, _) => Task.CompletedTask);

        ChatJid chatJid = new("C12345");
        await channel.SetTypingAsync(chatJid, true);
        await channel.SendMessageAsync(chatJid, "assistant reply");

        Assert.Single(client.PostedMessages);
        Assert.Equal("Evaluating...", client.PostedMessages[0].Text);
        Assert.Equal("1710115200.000100", client.PostedMessages[0].ThreadTs);
        Assert.Single(client.UpdatedMessages);
        Assert.Equal("assistant reply", client.UpdatedMessages[0].Text);
        Assert.Equal(client.PostedMessages[0].Ts, client.UpdatedMessages[0].Ts);

        await channel.DisconnectAsync();
    }

    [Fact]
    public async Task DirectMessageTypingWithoutAssistantThread_DoesNotCreateOrDeletePlaceholder()
    {
        FakeSlackSocketModeClient client = new("U-BOT", new SlackConversationInfo("D12345", "Direct Message", false));
        SlackChannel channel = new(
            new SlackChannelOptions
            {
                Enabled = true,
                BotToken = "xoxb-test",
                AppToken = "xapp-test",
                WorkingIndicatorText = "Evaluating..."
            },
            client);

        await channel.ConnectAsync();

        ChatJid chatJid = new("D12345");
        await channel.SetTypingAsync(chatJid, true);
        await channel.SetTypingAsync(chatJid, false);

        Assert.Empty(client.PostedMessages);
        Assert.Empty(client.DeletedMessages);

        await channel.DisconnectAsync();
    }

    [Fact]
    public async Task DirectMessageWithoutAssistantThread_DoesNotPostPlaceholder()
    {
        FakeSlackSocketModeClient client = new("U-BOT", new SlackConversationInfo("D12345", "Direct Message", false))
        {
            FailAssistantStatus = true
        };
        SlackChannel channel = new(
            new SlackChannelOptions
            {
                Enabled = true,
                BotToken = "xoxb-test",
                AppToken = "xapp-test",
                WorkingIndicatorText = "Evaluating..."
            },
            client);

        await channel.ConnectAsync();

        await channel.SetTypingAsync(new ChatJid("D12345"), true);

        Assert.Empty(client.PostedMessages);
        Assert.Empty(client.UpdatedMessages);
        Assert.Empty(client.DeletedMessages);
        Assert.Empty(client.AssistantStatusUpdates);

        await channel.DisconnectAsync();
    }

    [Fact]
    public async Task DirectMessageThread_UsesAssistantStatusAndRepliesInSameThread()
    {
        FakeSlackSocketModeClient client = new("U-BOT", new SlackConversationInfo("D12345", "Direct Message", false));
        client.Connection.Enqueue(new SlackSocketEnvelope(
            "envelope-1",
            "events_api",
            new SlackSocketPayload(
                "events_api",
                new SlackEventPayload(
                    "message",
                    "D12345",
                    "im",
                    "U-USER",
                    "hello",
                    "1710115200.000100",
                    "1710115200.000100",
                    "client-message-1",
                    null,
                    null))));

        SlackChannel channel = new(
            new SlackChannelOptions
            {
                Enabled = true,
                BotToken = "xoxb-test",
                AppToken = "xapp-test",
                WorkingIndicatorText = "Evaluating..."
            },
            client);

        await channel.ConnectAsync();
        await Task.Delay(50);
        await channel.PollInboundAsync((_, _) => Task.CompletedTask, (_, _) => Task.CompletedTask);

        ChatJid chatJid = new("D12345");
        await channel.SetTypingAsync(chatJid, true);
        await channel.SendMessageAsync(chatJid, "assistant reply");
        await channel.SetTypingAsync(chatJid, false);

        Assert.DoesNotContain(client.PostedMessages, message => message.Text == "Evaluating...");
        Assert.Equal(
            [("D12345", "1710115200.000100", "Evaluating..."), ("D12345", "1710115200.000100", string.Empty)],
            client.AssistantStatusUpdates);
        Assert.Single(client.PostedMessages);
        Assert.Equal("assistant reply", client.PostedMessages[0].Text);
        Assert.Equal("1710115200.000100", client.PostedMessages[0].ThreadTs);

        await channel.DisconnectAsync();
    }

    [Fact]
    public async Task DirectMessageThread_FallsBackToPlaceholderWhenAssistantStatusFails()
    {
        FakeSlackSocketModeClient client = new("U-BOT", new SlackConversationInfo("D12345", "Direct Message", false))
        {
            FailAssistantStatus = true
        };
        client.Connection.Enqueue(new SlackSocketEnvelope(
            "envelope-1",
            "events_api",
            new SlackSocketPayload(
                "events_api",
                new SlackEventPayload(
                    "message",
                    "D12345",
                    "im",
                    "U-USER",
                    "hello",
                    "1710115200.000100",
                    "1710115200.000100",
                    "client-message-1",
                    null,
                    null))));

        SlackChannel channel = new(
            new SlackChannelOptions
            {
                Enabled = true,
                BotToken = "xoxb-test",
                AppToken = "xapp-test",
                WorkingIndicatorText = "Evaluating..."
            },
            client);

        await channel.ConnectAsync();
        await Task.Delay(50);
        await channel.PollInboundAsync((_, _) => Task.CompletedTask, (_, _) => Task.CompletedTask);

        ChatJid chatJid = new("D12345");
        await channel.SetTypingAsync(chatJid, true);
        await channel.SendMessageAsync(chatJid, "assistant reply");

        Assert.Single(client.PostedMessages);
        Assert.Equal("assistant reply", client.PostedMessages[0].Text);
        Assert.Equal("1710115200.000100", client.PostedMessages[0].ThreadTs);
        Assert.Empty(client.DeletedMessages);
        Assert.Empty(client.UpdatedMessages);
        Assert.NotEmpty(client.AssistantStatusUpdates);

        await channel.DisconnectAsync();
    }

    [Fact]
    public async Task DirectMessageThread_ClearsFallbackPlaceholderWhenAssistantStatusLaterSucceeds()
    {
        FakeSlackSocketModeClient client = new("U-BOT", new SlackConversationInfo("D12345", "Direct Message", false));
        SlackChannel channel = new(
            new SlackChannelOptions
            {
                Enabled = true,
                BotToken = "xoxb-test",
                AppToken = "xapp-test",
                WorkingIndicatorText = "Evaluating..."
            },
            client);

        await channel.ConnectAsync();

        ChatJid chatJid = new("D12345");
        await channel.SetTypingAsync(chatJid, true);

        client.Connection.Enqueue(new SlackSocketEnvelope(
            "envelope-1",
            "events_api",
            new SlackSocketPayload(
                "events_api",
                new SlackEventPayload(
                    "message",
                    "D12345",
                    "im",
                    "U-USER",
                    "hello",
                    "1710115200.000100",
                    "1710115200.000100",
                    "client-message-1",
                    null,
                    null))));

        await Task.Delay(50);
        await channel.PollInboundAsync((_, _) => Task.CompletedTask, (_, _) => Task.CompletedTask);
        await channel.SetTypingAsync(chatJid, true);
        await channel.SendMessageAsync(chatJid, "assistant reply");

        Assert.Single(client.PostedMessages);
        Assert.Equal("assistant reply", client.PostedMessages[0].Text);
        Assert.Equal("1710115200.000100", client.PostedMessages[0].ThreadTs);
        Assert.Empty(client.DeletedMessages);
        Assert.Empty(client.UpdatedMessages);
        Assert.Single(client.AssistantStatusUpdates);
        Assert.Equal("Evaluating...", client.AssistantStatusUpdates[0].Status);

        await channel.DisconnectAsync();
    }

    private sealed class FakeSlackSocketModeClient : ISlackSocketModeClient
    {
        private int messageSequence;

        public FakeSlackSocketModeClient(string botUserId, SlackConversationInfo conversationInfo)
        {
            BotUserId = botUserId;
            ConversationInfo = conversationInfo;
            Connection = new FakeSlackSocketModeConnection();
        }

        public string BotUserId { get; }

        public SlackConversationInfo ConversationInfo { get; }

        public FakeSlackSocketModeConnection Connection { get; }

        public List<(string ConversationId, string Text, string? ThreadTs, string Ts)> PostedMessages { get; } = [];

        public List<(string ConversationId, string Ts, string Text)> UpdatedMessages { get; } = [];

        public List<(string ConversationId, string Ts)> DeletedMessages { get; } = [];

        public List<(string ConversationId, string ThreadTs, string Status)> AssistantStatusUpdates { get; } = [];

        public bool FailAssistantStatus { get; set; }

        public Task<SlackAuthInfo> AuthTestAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(new SlackAuthInfo(BotUserId));

        public Task<ISlackSocketModeConnection> ConnectAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<ISlackSocketModeConnection>(Connection);

        public Task<SlackConversationInfo> GetConversationInfoAsync(string conversationId, CancellationToken cancellationToken = default)
            => Task.FromResult(ConversationInfo);

        public Task<SlackPostedMessage> PostMessageAsync(string conversationId, string text, string? threadTs, CancellationToken cancellationToken = default)
        {
            string ts = $"posted-{Interlocked.Increment(ref messageSequence):D4}";
            PostedMessages.Add((conversationId, text, threadTs, ts));
            return Task.FromResult(new SlackPostedMessage(conversationId, ts));
        }

        public Task UpdateMessageAsync(string conversationId, string ts, string text, CancellationToken cancellationToken = default)
        {
            UpdatedMessages.Add((conversationId, ts, text));
            return Task.CompletedTask;
        }

        public Task DeleteMessageAsync(string conversationId, string ts, CancellationToken cancellationToken = default)
        {
            DeletedMessages.Add((conversationId, ts));
            return Task.CompletedTask;
        }

        public Task SetAssistantStatusAsync(string conversationId, string threadTs, string status, CancellationToken cancellationToken = default)
        {
            AssistantStatusUpdates.Add((conversationId, threadTs, status));
            if (FailAssistantStatus)
            {
                throw new InvalidOperationException("assistant status failed");
            }

            return Task.CompletedTask;
        }
    }

    private sealed class FakeSlackSocketModeConnection : ISlackSocketModeConnection
    {
        private readonly Channel<SlackSocketEnvelope?> envelopes = Channel.CreateUnbounded<SlackSocketEnvelope?>();

        public List<string> AcknowledgedEnvelopeIds { get; } = [];

        public void Enqueue(SlackSocketEnvelope envelope)
        {
            envelopes.Writer.TryWrite(envelope);
        }

        public Task AcknowledgeAsync(string envelopeId, CancellationToken cancellationToken = default)
        {
            AcknowledgedEnvelopeIds.Add(envelopeId);
            return Task.CompletedTask;
        }

        public async Task<SlackSocketEnvelope?> ReceiveAsync(CancellationToken cancellationToken = default)
        {
            return await envelopes.Reader.ReadAsync(cancellationToken);
        }

        public ValueTask DisposeAsync()
        {
            envelopes.Writer.TryComplete();
            return ValueTask.CompletedTask;
        }
    }
}
