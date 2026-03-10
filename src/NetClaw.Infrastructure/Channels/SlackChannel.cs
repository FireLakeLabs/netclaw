using System.Collections.Concurrent;
using System.Globalization;
using NetClaw.Domain.Contracts.Channels;
using NetClaw.Domain.Entities;
using NetClaw.Domain.ValueObjects;
using NetClaw.Infrastructure.Configuration;

namespace NetClaw.Infrastructure.Channels;

public sealed class SlackChannel : IInboundChannel
{
    private readonly object gate = new();
    private readonly SlackChannelOptions options;
    private readonly ISlackSocketModeClient slackClient;
    private readonly ConcurrentDictionary<string, byte> ownedChats = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, SlackConversationInfo> conversationInfoCache = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, string> activePlaceholderTs = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, string?> replyThreads = new(StringComparer.Ordinal);
    private readonly ConcurrentQueue<ChannelMetadataEvent> pendingMetadata = new();
    private readonly ConcurrentQueue<StoredMessage> pendingMessages = new();
    private CancellationTokenSource? receiveLoopCancellation;
    private Task? receiveLoopTask;
    private ISlackSocketModeConnection? connection;
    private string? botUserId;
    private bool isConnected;

    public SlackChannel(SlackChannelOptions options, ISlackSocketModeClient slackClient)
    {
        this.options = options;
        this.slackClient = slackClient;
    }

    public ChannelName Name => new("slack");

    public bool IsConnected
    {
        get
        {
            lock (gate)
            {
                return isConnected;
            }
        }
    }

    public async Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        lock (gate)
        {
            if (isConnected)
            {
                return;
            }

            isConnected = true;
            receiveLoopCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        }

        try
        {
            botUserId = (await slackClient.AuthTestAsync(cancellationToken)).UserId;
            receiveLoopTask = Task.Run(() => ReceiveLoopAsync(receiveLoopCancellation!.Token), CancellationToken.None);
        }
        catch
        {
            lock (gate)
            {
                isConnected = false;
            }

            receiveLoopCancellation?.Dispose();
            receiveLoopCancellation = null;
            throw;
        }
    }

    public async Task DisconnectAsync(CancellationToken cancellationToken = default)
    {
        Task? loopTask;
        CancellationTokenSource? cancellationSource;
        ISlackSocketModeConnection? currentConnection;

        lock (gate)
        {
            if (!isConnected)
            {
                return;
            }

            isConnected = false;
            loopTask = receiveLoopTask;
            cancellationSource = receiveLoopCancellation;
            currentConnection = connection;
            receiveLoopTask = null;
            receiveLoopCancellation = null;
            connection = null;
        }

        cancellationSource?.Cancel();

        if (loopTask is not null)
        {
            try
            {
                await loopTask.WaitAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
            }
        }

        if (currentConnection is not null)
        {
            await currentConnection.DisposeAsync();
        }

        cancellationSource?.Dispose();
    }

    public bool Owns(ChatJid chatJid)
    {
        return ownedChats.ContainsKey(chatJid.Value) || LooksLikeSlackConversationId(chatJid.Value);
    }

    public async Task SendMessageAsync(ChatJid chatJid, string text, CancellationToken cancellationToken = default)
    {
        if (!IsConnected)
        {
            throw new InvalidOperationException("Slack channel is not connected.");
        }

        ownedChats.TryAdd(chatJid.Value, 0);
        if (activePlaceholderTs.TryRemove(chatJid.Value, out string? placeholderTs)
            && !string.IsNullOrWhiteSpace(placeholderTs))
        {
            await slackClient.UpdateMessageAsync(chatJid.Value, placeholderTs, text, cancellationToken);
            return;
        }

        replyThreads.TryGetValue(chatJid.Value, out string? threadTs);
        await slackClient.PostMessageAsync(chatJid.Value, text, threadTs, cancellationToken);
    }

    public async Task SetTypingAsync(ChatJid chatJid, bool isTyping, CancellationToken cancellationToken = default)
    {
        if (!IsConnected)
        {
            return;
        }

        if (isTyping)
        {
            if (activePlaceholderTs.ContainsKey(chatJid.Value))
            {
                return;
            }

            replyThreads.TryGetValue(chatJid.Value, out string? threadTs);
            SlackPostedMessage placeholder = await slackClient.PostMessageAsync(chatJid.Value, options.WorkingIndicatorText, threadTs, cancellationToken);
            activePlaceholderTs[chatJid.Value] = placeholder.Ts;
            ownedChats.TryAdd(chatJid.Value, 0);
            return;
        }

        if (activePlaceholderTs.TryRemove(chatJid.Value, out string? placeholderTs)
            && !string.IsNullOrWhiteSpace(placeholderTs))
        {
            try
            {
                await slackClient.DeleteMessageAsync(chatJid.Value, placeholderTs, cancellationToken);
            }
            catch
            {
            }
        }
    }

    public Task SyncGroupsAsync(bool force, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public async Task PollInboundAsync(
        Func<ChannelMessage, CancellationToken, Task> onMessage,
        Func<ChannelMetadataEvent, CancellationToken, Task> onMetadata,
        CancellationToken cancellationToken = default)
    {
        if (!IsConnected)
        {
            return;
        }

        while (pendingMetadata.TryDequeue(out ChannelMetadataEvent? metadata))
        {
            if (metadata is null)
            {
                continue;
            }

            await onMetadata(metadata, cancellationToken);
        }

        while (pendingMessages.TryDequeue(out StoredMessage? message))
        {
            if (message is null)
            {
                continue;
            }

            await onMessage(new ChannelMessage(message.ChatJid, message), cancellationToken);
        }
    }

    private async Task ReceiveLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            ISlackSocketModeConnection currentConnection = await EnsureConnectionAsync(cancellationToken);
            SlackSocketEnvelope? envelope;

            try
            {
                envelope = await currentConnection.ReceiveAsync(cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch
            {
                await ResetConnectionAsync(currentConnection);
                continue;
            }

            if (envelope is null)
            {
                await ResetConnectionAsync(currentConnection);
                continue;
            }

            try
            {
                await currentConnection.AcknowledgeAsync(envelope.EnvelopeId, cancellationToken);
            }
            catch
            {
            }

            await HandleEnvelopeAsync(envelope, cancellationToken);
        }
    }

    private async Task HandleEnvelopeAsync(SlackSocketEnvelope envelope, CancellationToken cancellationToken)
    {
        if (!string.Equals(envelope.Type, "events_api", StringComparison.Ordinal) || envelope.Payload?.Event is null)
        {
            return;
        }

        SlackEventPayload slackEvent = envelope.Payload.Event;
        if (!string.Equals(slackEvent.Type, "message", StringComparison.Ordinal)
            || !string.IsNullOrWhiteSpace(slackEvent.Subtype)
            || !string.IsNullOrWhiteSpace(slackEvent.BotId)
            || string.IsNullOrWhiteSpace(slackEvent.Channel)
            || string.IsNullOrWhiteSpace(slackEvent.User)
            || string.IsNullOrWhiteSpace(slackEvent.Text)
            || string.IsNullOrWhiteSpace(slackEvent.Ts)
            || string.Equals(slackEvent.User, botUserId, StringComparison.Ordinal))
        {
            return;
        }

        string conversationId = slackEvent.Channel;
        SlackConversationInfo conversationInfo = await GetConversationInfoAsync(conversationId, slackEvent.ChannelType, cancellationToken);
        ownedChats.TryAdd(conversationId, 0);
        replyThreads[conversationId] = GetReplyThreadTs(conversationInfo.IsGroup, slackEvent);

        DateTimeOffset timestamp = ParseSlackTimestamp(slackEvent.Ts);
        ChatJid chatJid = new(conversationId);
        pendingMetadata.Enqueue(new ChannelMetadataEvent(chatJid, timestamp, conversationInfo.Name, Name, conversationInfo.IsGroup));

        string content = NormalizeContent(slackEvent.Text);
        string messageId = string.IsNullOrWhiteSpace(slackEvent.ClientMessageId)
            ? $"slack:{conversationId}:{slackEvent.Ts}"
            : slackEvent.ClientMessageId;

        pendingMessages.Enqueue(new StoredMessage(
            messageId,
            chatJid,
            slackEvent.User,
            slackEvent.User,
            content,
            timestamp,
            isFromMe: false,
            isBotMessage: false));
    }

    private async Task<ISlackSocketModeConnection> EnsureConnectionAsync(CancellationToken cancellationToken)
    {
        if (connection is not null)
        {
            return connection;
        }

        ISlackSocketModeConnection created = await slackClient.ConnectAsync(cancellationToken);
        lock (gate)
        {
            connection ??= created;
            return connection;
        }
    }

    private async Task ResetConnectionAsync(ISlackSocketModeConnection currentConnection)
    {
        lock (gate)
        {
            if (ReferenceEquals(connection, currentConnection))
            {
                connection = null;
            }
        }

        await currentConnection.DisposeAsync();
    }

    private async Task<SlackConversationInfo> GetConversationInfoAsync(string conversationId, string? channelType, CancellationToken cancellationToken)
    {
        if (conversationInfoCache.TryGetValue(conversationId, out SlackConversationInfo? cached))
        {
            return cached;
        }

        try
        {
            SlackConversationInfo info = await slackClient.GetConversationInfoAsync(conversationId, cancellationToken);
            conversationInfoCache[conversationId] = info;
            return info;
        }
        catch
        {
            bool isGroup = !string.Equals(channelType, "im", StringComparison.OrdinalIgnoreCase);
            SlackConversationInfo fallback = new(conversationId, conversationId, isGroup);
            conversationInfoCache[conversationId] = fallback;
            return fallback;
        }
    }

    private string NormalizeContent(string text)
    {
        if (string.IsNullOrWhiteSpace(botUserId))
        {
            return text.Trim();
        }

        return text.Replace($"<@{botUserId}>", options.MentionReplacement, StringComparison.Ordinal).Trim();
    }

    private string? GetReplyThreadTs(bool isGroup, SlackEventPayload slackEvent)
    {
        if (!isGroup || !options.ReplyInThreadByDefault)
        {
            return null;
        }

        return slackEvent.ThreadTs ?? slackEvent.Ts;
    }

    private static bool LooksLikeSlackConversationId(string value)
    {
        return value.Length > 1 && (value[0] is 'C' or 'D' or 'G');
    }

    private static DateTimeOffset ParseSlackTimestamp(string timestamp)
    {
        if (!decimal.TryParse(timestamp, NumberStyles.Number, CultureInfo.InvariantCulture, out decimal seconds))
        {
            return DateTimeOffset.UtcNow;
        }

        long wholeSeconds = decimal.ToInt64(decimal.Truncate(seconds));
        decimal fractional = seconds - wholeSeconds;
        long ticks = decimal.ToInt64(decimal.Round(fractional * TimeSpan.TicksPerSecond, 0, MidpointRounding.ToZero));
        return DateTimeOffset.FromUnixTimeSeconds(wholeSeconds).AddTicks(ticks);
    }
}