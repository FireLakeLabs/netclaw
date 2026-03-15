using System.Collections.Concurrent;
using System.Globalization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NetClaw.Domain.Contracts.Channels;
using NetClaw.Domain.Entities;
using NetClaw.Domain.ValueObjects;
using NetClaw.Infrastructure.Configuration;

namespace NetClaw.Infrastructure.Channels;

public sealed class SlackChannel : IInboundChannel
{
    private readonly object gate = new();
    private readonly ILogger<SlackChannel> logger;
    private readonly SlackChannelOptions options;
    private readonly ISlackSocketModeClient slackClient;
    private readonly StorageOptions storageOptions;
    private readonly ConcurrentDictionary<string, byte> ownedChats = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, SlackConversationInfo> conversationInfoCache = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, string> activePlaceholderTs = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, string?> replyThreads = new(StringComparer.Ordinal);
    private readonly ConcurrentQueue<ChannelMetadataEvent> pendingMetadata = new();
    private readonly ConcurrentQueue<StoredMessage> pendingMessages = new();
    private readonly ConcurrentDictionary<string, string> userDisplayNameCache = new(StringComparer.Ordinal);
    private CancellationTokenSource? receiveLoopCancellation;
    private Task? receiveLoopTask;
    private ISlackSocketModeConnection? connection;
    private string? botUserId;
    private bool isConnected;

    public SlackChannel(SlackChannelOptions options, ISlackSocketModeClient slackClient, StorageOptions storageOptions)
        : this(options, slackClient, storageOptions, NullLogger<SlackChannel>.Instance)
    {
    }

    public SlackChannel(SlackChannelOptions options, ISlackSocketModeClient slackClient, StorageOptions storageOptions, ILogger<SlackChannel> logger)
    {
        this.options = options;
        this.slackClient = slackClient;
        this.storageOptions = storageOptions;
        this.logger = logger;
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
            logger.LogInformation("Slack channel authenticated as bot user {BotUserId}.", botUserId);
            CancellationToken loopToken = receiveLoopCancellation!.Token;
            receiveLoopTask = Task.Run(() => ReceiveLoopAsync(loopToken), CancellationToken.None);
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

    public async Task SendFileAsync(ChatJid chatJid, string filePath, string fileName, string? threadTs, CancellationToken cancellationToken = default)
    {
        if (!IsConnected)
        {
            throw new InvalidOperationException("Slack channel is not connected.");
        }

        ownedChats.TryAdd(chatJid.Value, 0);

        string? resolvedThreadTs = threadTs;
        if (resolvedThreadTs is null)
        {
            replyThreads.TryGetValue(chatJid.Value, out resolvedThreadTs);
        }

        long fileSize = new FileInfo(filePath).Length;
        SlackFileUploadUrl uploadUrl = await slackClient.GetUploadUrlExternalAsync(fileName, fileSize, cancellationToken);
        await slackClient.UploadFileToUrlAsync(uploadUrl.UploadUrl, filePath, cancellationToken);
        await slackClient.CompleteUploadExternalAsync(uploadUrl.FileId, chatJid.Value, resolvedThreadTs, cancellationToken);

        logger.LogInformation("Uploaded file {FileName} to Slack for {ChatJid}.", fileName, chatJid.Value);
    }

    public async Task SendMessageAsync(ChatJid chatJid, string text, CancellationToken cancellationToken = default)
    {
        if (!IsConnected)
        {
            throw new InvalidOperationException("Slack channel is not connected.");
        }

        ownedChats.TryAdd(chatJid.Value, 0);
        bool hasAssistantThread = TryGetAssistantThreadTs(chatJid.Value, out _);
        if (activePlaceholderTs.TryRemove(chatJid.Value, out string? placeholderTs)
            && !string.IsNullOrWhiteSpace(placeholderTs))
        {
            if (hasAssistantThread)
            {
                await DeletePlaceholderAsync(chatJid.Value, placeholderTs, cancellationToken);
            }
            else
            {
                logger.LogDebug("Updating Slack placeholder for {ChatJid} with final response.", chatJid.Value);
                await slackClient.UpdateMessageAsync(chatJid.Value, placeholderTs, text, cancellationToken);
                return;
            }
        }

        replyThreads.TryGetValue(chatJid.Value, out string? threadTs);
        logger.LogDebug("Posting Slack response to {ChatJid}. ThreadTs={ThreadTs}", chatJid.Value, threadTs ?? "<none>");
        await slackClient.PostMessageAsync(chatJid.Value, text, threadTs, cancellationToken);
    }

    public async Task SetTypingAsync(ChatJid chatJid, bool isTyping, CancellationToken cancellationToken = default)
    {
        if (!IsConnected)
        {
            return;
        }

        bool isDirectMessage = LooksLikeDirectMessageConversationId(chatJid.Value);

        if (TryGetAssistantThreadTs(chatJid.Value, out string? assistantThreadTs)
            && await TrySetAssistantStatusAsync(chatJid.Value, assistantThreadTs, isTyping ? options.WorkingIndicatorText : string.Empty, cancellationToken))
        {
            if (isTyping)
            {
                await ClearActivePlaceholderAsync(chatJid.Value, cancellationToken);
            }

            ownedChats.TryAdd(chatJid.Value, 0);
            return;
        }

        if (isDirectMessage)
        {
            if (!isTyping)
            {
                await ClearActivePlaceholderAsync(chatJid.Value, cancellationToken);
            }

            return;
        }

        if (isTyping)
        {
            if (activePlaceholderTs.ContainsKey(chatJid.Value))
            {
                return;
            }

            replyThreads.TryGetValue(chatJid.Value, out string? threadTs);
            logger.LogDebug("Posting Slack working indicator for {ChatJid}. ThreadTs={ThreadTs}", chatJid.Value, threadTs ?? "<none>");
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
                logger.LogDebug("Deleting Slack working indicator for {ChatJid}.", chatJid.Value);
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
                logger.LogWarning("Slack socket receive failed; resetting connection.");
                await ResetConnectionAsync(currentConnection);
                continue;
            }

            if (envelope is null)
            {
                logger.LogInformation("Slack socket closed by remote endpoint; reconnecting.");
                await ResetConnectionAsync(currentConnection);
                continue;
            }

            logger.LogDebug("Received Slack socket envelope. Type={EnvelopeType}", envelope.Type);

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
            logger.LogDebug("Ignoring Slack envelope. Type={EnvelopeType}", envelope.Type);
            return;
        }

        SlackEventPayload slackEvent = envelope.Payload.Event;
        bool hasFiles = slackEvent.Files is { Count: > 0 };
        bool isFileShare = string.Equals(slackEvent.Subtype, "file_share", StringComparison.Ordinal);
        if (!string.Equals(slackEvent.Type, "message", StringComparison.Ordinal)
            || (!string.IsNullOrWhiteSpace(slackEvent.Subtype) && !isFileShare)
            || !string.IsNullOrWhiteSpace(slackEvent.BotId)
            || string.IsNullOrWhiteSpace(slackEvent.Channel)
            || string.IsNullOrWhiteSpace(slackEvent.User)
            || (string.IsNullOrWhiteSpace(slackEvent.Text) && !hasFiles)
            || string.IsNullOrWhiteSpace(slackEvent.Ts)
            || string.Equals(slackEvent.User, botUserId, StringComparison.Ordinal))
        {
            logger.LogDebug(
                "Ignoring Slack event. EventType={EventType}, Subtype={Subtype}, Channel={Channel}, User={User}",
                slackEvent.Type,
                slackEvent.Subtype ?? "<none>",
                slackEvent.Channel ?? "<none>",
                slackEvent.User ?? "<none>");
            return;
        }

        string conversationId = slackEvent.Channel;
        SlackConversationInfo conversationInfo = await GetConversationInfoAsync(conversationId, slackEvent.ChannelType, cancellationToken);
        ownedChats.TryAdd(conversationId, 0);
        replyThreads[conversationId] = GetReplyThreadTs(conversationInfo.IsGroup, slackEvent);
        logger.LogInformation(
            "Queued Slack inbound message for {ConversationId}. IsGroup={IsGroup}, ThreadTs={ThreadTs}",
            conversationId,
            conversationInfo.IsGroup,
            replyThreads[conversationId] ?? "<none>");

        DateTimeOffset timestamp = ParseSlackTimestamp(slackEvent.Ts);
        ChatJid chatJid = new(conversationId);
        pendingMetadata.Enqueue(new ChannelMetadataEvent(chatJid, timestamp, conversationInfo.Name, Name, conversationInfo.IsGroup));

        string content = string.IsNullOrWhiteSpace(slackEvent.Text) ? string.Empty : NormalizeContent(slackEvent.Text);
        string messageId = string.IsNullOrWhiteSpace(slackEvent.ClientMessageId)
            ? $"slack:{conversationId}:{slackEvent.Ts}"
            : slackEvent.ClientMessageId;

        string senderDisplayName = await ResolveUserDisplayNameAsync(slackEvent.User, cancellationToken);

        List<FileAttachment> attachments = [];
        if (hasFiles)
        {
            foreach (SlackFileObject file in slackEvent.Files!)
            {
                if (string.IsNullOrWhiteSpace(file.UrlPrivate))
                {
                    continue;
                }

                try
                {
                    if (file.Size > options.MaxFileDownloadBytes)
                    {
                        logger.LogWarning("Skipping Slack file {FileId} ({Size} bytes) — exceeds max {Max} bytes.", file.Id, file.Size, options.MaxFileDownloadBytes);
                        continue;
                    }

                    string fileDir = Path.Combine(storageOptions.DataDirectory, "files", chatJid.Value, file.Id);
                    string fileName = SanitizeFileName(string.IsNullOrWhiteSpace(file.Name) ? file.Id : file.Name);
                    string localPath = Path.GetFullPath(Path.Combine(fileDir, fileName));
                    if (!localPath.StartsWith(fileDir, StringComparison.Ordinal))
                    {
                        logger.LogWarning("Skipping Slack file {FileId} — sanitized name escapes storage directory.", file.Id);
                        continue;
                    }

                    await slackClient.DownloadFileAsync(file.UrlPrivate, localPath, cancellationToken);
                    logger.LogInformation("Downloaded Slack file {FileId} ({FileName}) to {LocalPath}.", file.Id, fileName, localPath);

                    attachments.Add(new FileAttachment(
                        file.Id,
                        messageId,
                        chatJid,
                        fileName,
                        file.MimeType,
                        file.Size,
                        localPath,
                        DateTimeOffset.UtcNow));
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Failed to download Slack file {FileId}.", file.Id);
                }
            }
        }

        if (string.IsNullOrWhiteSpace(content) && attachments.Count > 0)
        {
            content = $"[Uploaded: {string.Join(", ", attachments.Select(a => a.FileName))}]";
        }

        pendingMessages.Enqueue(new StoredMessage(
            messageId,
            chatJid,
            slackEvent.User,
            senderDisplayName,
            content,
            timestamp,
            isFromMe: false,
            isBotMessage: false,
            attachments: attachments));
    }

    private static string SanitizeFileName(string name)
    {
        string safe = Path.GetFileName(name);
        if (string.IsNullOrWhiteSpace(safe))
        {
            return "file";
        }

        foreach (char c in Path.GetInvalidFileNameChars())
        {
            safe = safe.Replace(c, '_');
        }

        return safe;
    }

    private async Task<ISlackSocketModeConnection> EnsureConnectionAsync(CancellationToken cancellationToken)
    {
        if (connection is not null)
        {
            return connection;
        }

        ISlackSocketModeConnection created = await slackClient.ConnectAsync(cancellationToken);
        logger.LogInformation("Slack Socket Mode connection established.");
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

    private async Task<string> ResolveUserDisplayNameAsync(string userId, CancellationToken cancellationToken)
    {
        if (userDisplayNameCache.TryGetValue(userId, out string? cached))
        {
            return cached;
        }

        try
        {
            SlackUserInfo info = await slackClient.GetUserInfoAsync(userId, cancellationToken);
            userDisplayNameCache[userId] = info.DisplayName;
            return info.DisplayName;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to resolve Slack user display name for {UserId}. Ensure the bot token has the 'users:read' scope.", userId);
            userDisplayNameCache[userId] = userId;
            return userId;
        }
    }

    private async Task<bool> TrySetAssistantStatusAsync(string conversationId, string threadTs, string status, CancellationToken cancellationToken)
    {
        try
        {
            logger.LogDebug(
                "Setting Slack assistant thread status for {ConversationId}. ThreadTs={ThreadTs}, HasStatus={HasStatus}",
                conversationId,
                threadTs,
                !string.IsNullOrWhiteSpace(status));
            await slackClient.SetAssistantStatusAsync(conversationId, threadTs, status, cancellationToken);
            return true;
        }
        catch (Exception exception)
        {
            logger.LogDebug(exception, "Slack assistant thread status failed for {ConversationId}; falling back to placeholder messaging.", conversationId);
            return false;
        }
    }

    private async Task ClearActivePlaceholderAsync(string conversationId, CancellationToken cancellationToken)
    {
        if (activePlaceholderTs.TryRemove(conversationId, out string? placeholderTs)
            && !string.IsNullOrWhiteSpace(placeholderTs))
        {
            await DeletePlaceholderAsync(conversationId, placeholderTs, cancellationToken);
        }
    }

    private async Task DeletePlaceholderAsync(string conversationId, string placeholderTs, CancellationToken cancellationToken)
    {
        try
        {
            logger.LogDebug("Deleting Slack working indicator for {ConversationId}.", conversationId);
            await slackClient.DeleteMessageAsync(conversationId, placeholderTs, cancellationToken);
        }
        catch
        {
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
        if (!isGroup)
        {
            return string.IsNullOrWhiteSpace(slackEvent.ThreadTs)
                ? null
                : slackEvent.ThreadTs;
        }

        if (!options.ReplyInThreadByDefault)
        {
            return null;
        }

        return slackEvent.ThreadTs ?? slackEvent.Ts;
    }

    private bool TryGetAssistantThreadTs(string conversationId, out string threadTs)
    {
        threadTs = string.Empty;
        if (!LooksLikeDirectMessageConversationId(conversationId))
        {
            return false;
        }

        if (!replyThreads.TryGetValue(conversationId, out string? candidate)
            || string.IsNullOrWhiteSpace(candidate))
        {
            return false;
        }

        threadTs = candidate;
        return true;
    }

    private static bool LooksLikeSlackConversationId(string value)
    {
        return value.Length > 1 && (value[0] is 'C' or 'D' or 'G');
    }

    private static bool LooksLikeDirectMessageConversationId(string value)
    {
        return value.Length > 1 && value[0] == 'D';
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
