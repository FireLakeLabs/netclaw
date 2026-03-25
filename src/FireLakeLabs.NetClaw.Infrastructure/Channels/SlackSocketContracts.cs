using System.Text.Json.Serialization;

namespace FireLakeLabs.NetClaw.Infrastructure.Channels;

public interface ISlackSocketModeClient
{
    Task<SlackAuthInfo> AuthTestAsync(CancellationToken cancellationToken = default);

    Task<ISlackSocketModeConnection> ConnectAsync(CancellationToken cancellationToken = default);

    Task<SlackConversationInfo> GetConversationInfoAsync(string conversationId, CancellationToken cancellationToken = default);

    Task<SlackPostedMessage> PostMessageAsync(string conversationId, string text, string? threadTs, CancellationToken cancellationToken = default);

    Task UpdateMessageAsync(string conversationId, string ts, string text, CancellationToken cancellationToken = default);

    Task DeleteMessageAsync(string conversationId, string ts, CancellationToken cancellationToken = default);

    Task SetAssistantStatusAsync(string conversationId, string threadTs, string status, CancellationToken cancellationToken = default);

    Task<SlackUserInfo> GetUserInfoAsync(string userId, CancellationToken cancellationToken = default);

    Task DownloadFileAsync(string urlPrivate, string destinationPath, CancellationToken cancellationToken = default);

    Task<SlackFileUploadUrl> GetUploadUrlExternalAsync(string fileName, long fileSize, CancellationToken cancellationToken = default);

    Task UploadFileToUrlAsync(string uploadUrl, string filePath, CancellationToken cancellationToken = default);

    Task CompleteUploadExternalAsync(string fileId, string channelId, string? threadTs, CancellationToken cancellationToken = default);
}

public interface ISlackSocketModeConnection : IAsyncDisposable
{
    Task<SlackSocketEnvelope?> ReceiveAsync(CancellationToken cancellationToken = default);

    Task AcknowledgeAsync(string envelopeId, CancellationToken cancellationToken = default);
}

public sealed record SlackAuthInfo(string UserId);

public sealed record SlackConversationInfo(string ConversationId, string Name, bool IsGroup);

public sealed record SlackPostedMessage(string ConversationId, string Ts);

public sealed record SlackUserInfo(string UserId, string DisplayName);

public sealed record SlackSocketEnvelope(
    [property: JsonPropertyName("envelope_id")] string EnvelopeId,
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("payload")] SlackSocketPayload? Payload);

public sealed record SlackSocketPayload(
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("event")] SlackEventPayload? Event);

public sealed record SlackEventPayload(
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("channel")] string? Channel,
    [property: JsonPropertyName("channel_type")] string? ChannelType,
    [property: JsonPropertyName("user")] string? User,
    [property: JsonPropertyName("text")] string? Text,
    [property: JsonPropertyName("ts")] string? Ts,
    [property: JsonPropertyName("thread_ts")] string? ThreadTs,
    [property: JsonPropertyName("client_msg_id")] string? ClientMessageId,
    [property: JsonPropertyName("subtype")] string? Subtype,
    [property: JsonPropertyName("bot_id")] string? BotId,
    [property: JsonPropertyName("files")] IReadOnlyList<SlackFileObject>? Files = null);

public sealed record SlackFileObject(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("name")] string? Name,
    [property: JsonPropertyName("size")] long Size,
    [property: JsonPropertyName("mimetype")] string? MimeType,
    [property: JsonPropertyName("url_private")] string? UrlPrivate,
    [property: JsonPropertyName("filetype")] string? FileType);

public sealed record SlackFileUploadUrl(string UploadUrl, string FileId);
