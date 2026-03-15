using System.Buffers;
using System.Net.Http.Headers;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using NetClaw.Infrastructure.Configuration;

namespace NetClaw.Infrastructure.Channels;

public sealed class SlackSocketModeClient : ISlackSocketModeClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient httpClient;
    private readonly SlackChannelOptions options;

    public SlackSocketModeClient(SlackChannelOptions options)
        : this(options, new HttpClient())
    {
    }

    public SlackSocketModeClient(SlackChannelOptions options, HttpClient httpClient)
    {
        this.options = options;
        this.httpClient = httpClient;
    }

    public async Task<SlackAuthInfo> AuthTestAsync(CancellationToken cancellationToken = default)
    {
        SlackAuthTestResponse response = await SendAsync<SlackAuthTestResponse>(
            HttpMethod.Post,
            "auth.test",
            options.BotToken,
            body: null,
            cancellationToken);

        if (string.IsNullOrWhiteSpace(response.UserId))
        {
            throw new InvalidOperationException("Slack auth.test did not return a bot user id.");
        }

        return new SlackAuthInfo(response.UserId);
    }

    public async Task<ISlackSocketModeConnection> ConnectAsync(CancellationToken cancellationToken = default)
    {
        SlackSocketOpenResponse response = await SendAsync<SlackSocketOpenResponse>(
            HttpMethod.Post,
            "apps.connections.open",
            options.AppToken,
            body: null,
            cancellationToken);

        if (string.IsNullOrWhiteSpace(response.Url))
        {
            throw new InvalidOperationException("Slack apps.connections.open did not return a socket URL.");
        }

        ClientWebSocket socket = new();
        socket.Options.KeepAliveInterval = TimeSpan.FromSeconds(20);
        await socket.ConnectAsync(new Uri(response.Url), cancellationToken);
        return new SlackSocketModeConnection(socket);
    }

    public async Task<SlackConversationInfo> GetConversationInfoAsync(string conversationId, CancellationToken cancellationToken = default)
    {
        string escapedConversationId = Uri.EscapeDataString(conversationId);
        SlackConversationInfoResponse response = await SendAsync<SlackConversationInfoResponse>(
            HttpMethod.Get,
            $"conversations.info?channel={escapedConversationId}",
            options.BotToken,
            body: null,
            cancellationToken);

        if (response.Channel is null)
        {
            throw new InvalidOperationException("Slack conversations.info did not return channel details.");
        }

        bool isGroup = !response.Channel.IsIm;
        string name = string.IsNullOrWhiteSpace(response.Channel.Name)
            ? response.Channel.Id ?? conversationId
            : response.Channel.Name;

        return new SlackConversationInfo(conversationId, name, isGroup);
    }

    public async Task<SlackUserInfo> GetUserInfoAsync(string userId, CancellationToken cancellationToken = default)
    {
        string escapedUserId = Uri.EscapeDataString(userId);
        SlackUserInfoResponse response = await SendAsync<SlackUserInfoResponse>(
            HttpMethod.Get,
            $"users.info?user={escapedUserId}",
            options.BotToken,
            body: null,
            cancellationToken);

        if (response.User is null)
        {
            throw new InvalidOperationException("Slack users.info did not return user details.");
        }

        string? displayName = response.User.Profile?.DisplayName;
        if (string.IsNullOrWhiteSpace(displayName))
        {
            displayName = response.User.Profile?.RealName;
        }

        if (string.IsNullOrWhiteSpace(displayName))
        {
            displayName = response.User.RealName;
        }

        if (string.IsNullOrWhiteSpace(displayName))
        {
            displayName = response.User.Name ?? userId;
        }

        return new SlackUserInfo(userId, displayName);
    }

    public async Task<SlackPostedMessage> PostMessageAsync(string conversationId, string text, string? threadTs, CancellationToken cancellationToken = default)
    {
        SlackChatWriteResponse response = await SendAsync<SlackChatWriteResponse>(
            HttpMethod.Post,
            "chat.postMessage",
            options.BotToken,
            new SlackPostMessageRequest(conversationId, text, threadTs),
            cancellationToken);

        return new SlackPostedMessage(response.Channel ?? conversationId, response.Ts ?? throw new InvalidOperationException("Slack chat.postMessage did not return a timestamp."));
    }

    public Task UpdateMessageAsync(string conversationId, string ts, string text, CancellationToken cancellationToken = default)
    {
        return SendAsync<SlackChatWriteResponse>(
            HttpMethod.Post,
            "chat.update",
            options.BotToken,
            new SlackUpdateMessageRequest(conversationId, ts, text),
            cancellationToken);
    }

    public Task DeleteMessageAsync(string conversationId, string ts, CancellationToken cancellationToken = default)
    {
        return SendAsync<SlackDeleteMessageResponse>(
            HttpMethod.Post,
            "chat.delete",
            options.BotToken,
            new SlackDeleteMessageRequest(conversationId, ts),
            cancellationToken);
    }

    public Task SetAssistantStatusAsync(string conversationId, string threadTs, string status, CancellationToken cancellationToken = default)
    {
        return SendAsync<SlackAssistantStatusResponse>(
            HttpMethod.Post,
            "assistant.threads.setStatus",
            options.BotToken,
            new SlackAssistantStatusRequest(conversationId, threadTs, status),
            cancellationToken);
    }

    public async Task DownloadFileAsync(string urlPrivate, string destinationPath, CancellationToken cancellationToken = default)
    {
        using HttpRequestMessage request = new(HttpMethod.Get, urlPrivate);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", options.BotToken);

        using HttpResponseMessage response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();

        string? directory = Path.GetDirectoryName(destinationPath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await using Stream contentStream = await response.Content.ReadAsStreamAsync(cancellationToken);
        await using FileStream fileStream = new(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None);
        await contentStream.CopyToAsync(fileStream, cancellationToken);
    }

    private async Task<TResponse> SendAsync<TResponse>(HttpMethod method, string path, string token, object? body, CancellationToken cancellationToken)
    {
        using HttpRequestMessage request = new(method, BuildApiUri(path));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        if (body is not null)
        {
            string payload = JsonSerializer.Serialize(body, JsonOptions);
            request.Content = new StringContent(payload, Encoding.UTF8, "application/json");
        }

        using HttpResponseMessage response = await httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        await using Stream contentStream = await response.Content.ReadAsStreamAsync(cancellationToken);
        SlackApiEnvelope<TResponse>? envelope = await JsonSerializer.DeserializeAsync<SlackApiEnvelope<TResponse>>(contentStream, JsonOptions, cancellationToken);
        if (envelope is null)
        {
            throw new InvalidOperationException($"Slack API '{path}' returned an empty response.");
        }

        if (!envelope.Ok)
        {
            throw new InvalidOperationException($"Slack API '{path}' failed: {envelope.Error ?? "unknown_error"}.");
        }

        return envelope.Data;
    }

    private string BuildApiUri(string path)
    {
        return $"{options.ApiBaseUrl.TrimEnd('/')}/{path}";
    }

    private sealed class SlackSocketModeConnection : ISlackSocketModeConnection
    {
        private readonly ClientWebSocket socket;
        private readonly SemaphoreSlim sendLock = new(1, 1);

        public SlackSocketModeConnection(ClientWebSocket socket)
        {
            this.socket = socket;
        }

        public async Task<SlackSocketEnvelope?> ReceiveAsync(CancellationToken cancellationToken = default)
        {
            byte[] buffer = ArrayPool<byte>.Shared.Rent(16 * 1024);
            try
            {
                ArrayBufferWriter<byte> payload = new();
                while (true)
                {
                    WebSocketReceiveResult result = await socket.ReceiveAsync(buffer, cancellationToken);
                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        return null;
                    }

                    payload.Write(buffer.AsSpan(0, result.Count));
                    if (result.EndOfMessage)
                    {
                        break;
                    }
                }

                return JsonSerializer.Deserialize<SlackSocketEnvelope>(payload.WrittenSpan, JsonOptions);
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }

        public async Task AcknowledgeAsync(string envelopeId, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(envelopeId))
            {
                return;
            }

            byte[] payload = JsonSerializer.SerializeToUtf8Bytes(new SlackSocketAcknowledgement(envelopeId), JsonOptions);
            await sendLock.WaitAsync(cancellationToken);
            try
            {
                await socket.SendAsync(payload, WebSocketMessageType.Text, endOfMessage: true, cancellationToken);
            }
            finally
            {
                sendLock.Release();
            }
        }

        public async ValueTask DisposeAsync()
        {
            try
            {
                if (socket.State is WebSocketState.Open or WebSocketState.CloseReceived)
                {
                    await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "disconnect", CancellationToken.None);
                }
            }
            catch
            {
            }

            socket.Dispose();
            sendLock.Dispose();
        }

        private sealed record SlackSocketAcknowledgement([property: JsonPropertyName("envelope_id")] string EnvelopeId);
    }

    private sealed class SlackApiEnvelope<T>
    {
        [JsonPropertyName("ok")]
        public bool Ok { get; init; }

        [JsonPropertyName("error")]
        public string? Error { get; init; }

        [JsonExtensionData]
        public IDictionary<string, JsonElement>? ExtensionData { get; init; }

        public T Data => JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(ExtensionData ?? new Dictionary<string, JsonElement>(), JsonOptions), JsonOptions)
            ?? throw new InvalidOperationException("Slack API response payload could not be parsed.");
    }

    private sealed record SlackAuthTestResponse([property: JsonPropertyName("user_id")] string? UserId);

    private sealed record SlackSocketOpenResponse([property: JsonPropertyName("url")] string? Url);

    private sealed record SlackConversationInfoResponse([property: JsonPropertyName("channel")] SlackConversationResponseChannel? Channel);

    private sealed record SlackConversationResponseChannel(
        [property: JsonPropertyName("id")] string? Id,
        [property: JsonPropertyName("name")] string? Name,
        [property: JsonPropertyName("is_im")] bool IsIm);

    private sealed record SlackChatWriteResponse(
        [property: JsonPropertyName("channel")] string? Channel,
        [property: JsonPropertyName("ts")] string? Ts);

    private sealed record SlackDeleteMessageResponse([property: JsonPropertyName("ts")] string? Ts);

    private sealed record SlackAssistantStatusResponse();

    private sealed record SlackPostMessageRequest(
        [property: JsonPropertyName("channel")] string Channel,
        [property: JsonPropertyName("text")] string Text,
        [property: JsonPropertyName("thread_ts")] string? ThreadTs);

    private sealed record SlackUpdateMessageRequest(
        [property: JsonPropertyName("channel")] string Channel,
        [property: JsonPropertyName("ts")] string Ts,
        [property: JsonPropertyName("text")] string Text);

    private sealed record SlackDeleteMessageRequest(
        [property: JsonPropertyName("channel")] string Channel,
        [property: JsonPropertyName("ts")] string Ts);

    private sealed record SlackAssistantStatusRequest(
        [property: JsonPropertyName("channel_id")] string ChannelId,
        [property: JsonPropertyName("thread_ts")] string ThreadTs,
        [property: JsonPropertyName("status")] string Status);

    private sealed record SlackUserInfoResponse([property: JsonPropertyName("user")] SlackUserResponseUser? User);

    private sealed record SlackUserResponseUser(
        [property: JsonPropertyName("id")] string? Id,
        [property: JsonPropertyName("name")] string? Name,
        [property: JsonPropertyName("real_name")] string? RealName,
        [property: JsonPropertyName("profile")] SlackUserResponseProfile? Profile);

    private sealed record SlackUserResponseProfile(
        [property: JsonPropertyName("display_name")] string? DisplayName,
        [property: JsonPropertyName("real_name")] string? RealName);
}
