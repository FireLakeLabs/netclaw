using System.Net;
using System.Text;
using FireLakeLabs.NetClaw.Domain.Contracts.Services;
using FireLakeLabs.NetClaw.Infrastructure.Configuration;
using Microsoft.Extensions.Logging;

namespace FireLakeLabs.NetClaw.Infrastructure.Runtime;

public sealed class CredentialProxyService : ICredentialProxyService
{
    private static readonly HashSet<string> HopByHopHeaders = new(StringComparer.OrdinalIgnoreCase)
    {
        "Connection", "Keep-Alive", "Proxy-Authenticate", "Proxy-Authorization",
        "TE", "Trailers", "Transfer-Encoding", "Upgrade"
    };

    private readonly CredentialProxyOptions options;
    private readonly AgentRuntimeOptions agentOptions;
    private readonly ILogger<CredentialProxyService> logger;
    private readonly HttpClient httpClient;
    private HttpListener? listener;
    private CancellationTokenSource? cts;
    private Task? listenTask;

    public CredentialProxyService(
        CredentialProxyOptions options,
        AgentRuntimeOptions agentOptions,
        ILogger<CredentialProxyService> logger)
    {
        this.options = options;
        this.agentOptions = agentOptions;
        this.logger = logger;
        httpClient = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };
    }

    public Uri BindAddress => new($"http://{options.Host}:{options.Port}/");

    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        WarnIfCopilotContainerAuthIsMissing();

        listener = new HttpListener();
        listener.Prefixes.Add($"http://{GetListenerHost(options.Host)}:{options.Port}/");
        listener.Start();
        cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        listenTask = AcceptLoopAsync(cts.Token);
        logger.LogInformation("Credential proxy started on {BindAddress}", BindAddress);
        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        cts?.Cancel();

        try
        {
            listener?.Stop();
        }
        catch (ObjectDisposedException)
        {
        }

        if (listenTask is not null)
        {
            try
            {
                await listenTask;
            }
            catch (OperationCanceledException)
            {
            }
        }

        listener?.Close();
        httpClient.Dispose();
        logger.LogInformation("Credential proxy stopped.");
    }

    private async Task AcceptLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            HttpListenerContext? context;
            try
            {
                context = await listener!.GetContextAsync().WaitAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (HttpListenerException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (ObjectDisposedException)
            {
                break;
            }

            _ = HandleRequestAsync(context);
        }
    }

    private async Task HandleRequestAsync(HttpListenerContext context)
    {
        try
        {
            HttpListenerRequest inbound = context.Request;
            string path = inbound.Url?.PathAndQuery ?? "/";

            bool isClaude = IsClaude(inbound);
            string upstreamBase = isClaude ? options.ClaudeUpstreamUrl : options.CopilotUpstreamUrl;
            Uri upstreamUri = new(new Uri(upstreamBase.TrimEnd('/')), path);

            using HttpRequestMessage forward = new(new HttpMethod(inbound.HttpMethod), upstreamUri);

            if (inbound.HasEntityBody)
            {
                using MemoryStream bodyBuffer = new();
                await inbound.InputStream.CopyToAsync(bodyBuffer);
                bodyBuffer.Position = 0;
                forward.Content = new ByteArrayContent(bodyBuffer.ToArray());

                if (inbound.ContentType is not null)
                {
                    forward.Content.Headers.TryAddWithoutValidation("Content-Type", inbound.ContentType);
                }
            }

            foreach (string? headerName in inbound.Headers.AllKeys)
            {
                if (headerName is null || HopByHopHeaders.Contains(headerName))
                {
                    continue;
                }

                if (headerName.Equals("Host", StringComparison.OrdinalIgnoreCase)
                    || headerName.Equals("Content-Type", StringComparison.OrdinalIgnoreCase)
                    || headerName.Equals("Content-Length", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                string? value = inbound.Headers[headerName];
                if (value is not null)
                {
                    forward.Headers.TryAddWithoutValidation(headerName, value);
                }
            }

            InjectCredentials(forward, isClaude);

            using HttpResponseMessage upstream = await httpClient.SendAsync(forward, HttpCompletionOption.ResponseHeadersRead);

            context.Response.StatusCode = (int)upstream.StatusCode;

            foreach (var header in upstream.Headers)
            {
                if (!HopByHopHeaders.Contains(header.Key))
                {
                    context.Response.Headers[header.Key] = string.Join(", ", header.Value);
                }
            }

            foreach (var header in upstream.Content.Headers)
            {
                if (header.Key.Equals("Content-Type", StringComparison.OrdinalIgnoreCase))
                {
                    context.Response.ContentType = string.Join(", ", header.Value);
                }
                else if (!header.Key.Equals("Content-Length", StringComparison.OrdinalIgnoreCase))
                {
                    context.Response.Headers[header.Key] = string.Join(", ", header.Value);
                }
            }

            await using Stream upstreamBody = await upstream.Content.ReadAsStreamAsync();
            await upstreamBody.CopyToAsync(context.Response.OutputStream);

            context.Response.Close();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Credential proxy failed to handle request.");
            try
            {
                context.Response.StatusCode = 502;
                byte[] errorBytes = Encoding.UTF8.GetBytes($"{{\"error\":\"{ex.Message}\"}}");
                context.Response.ContentType = "application/json";
                context.Response.ContentLength64 = errorBytes.Length;
                await context.Response.OutputStream.WriteAsync(errorBytes);
                context.Response.Close();
            }
            catch
            {
                // Best-effort error response
            }
        }
    }

    private void InjectCredentials(HttpRequestMessage request, bool isClaude)
    {
        if (isClaude)
        {
            string? apiKey = Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY");
            if (!string.IsNullOrWhiteSpace(apiKey))
            {
                request.Headers.Remove("x-api-key");
                request.Headers.TryAddWithoutValidation("x-api-key", apiKey);
            }
        }
        else
        {
            string? token = agentOptions.CopilotGitHubToken
                ?? Environment.GetEnvironmentVariable("COPILOT_GITHUB_TOKEN")
                ?? Environment.GetEnvironmentVariable("GH_TOKEN")
                ?? Environment.GetEnvironmentVariable("GITHUB_TOKEN");
            if (!string.IsNullOrWhiteSpace(token))
            {
                request.Headers.Remove("Authorization");
                request.Headers.TryAddWithoutValidation("Authorization", $"Bearer {token}");
            }
        }
    }

    private void WarnIfCopilotContainerAuthIsMissing()
    {
        string? token = agentOptions.CopilotGitHubToken
            ?? Environment.GetEnvironmentVariable("COPILOT_GITHUB_TOKEN")
            ?? Environment.GetEnvironmentVariable("GH_TOKEN")
            ?? Environment.GetEnvironmentVariable("GITHUB_TOKEN");
        if (!string.IsNullOrWhiteSpace(token))
        {
            return;
        }

        if (agentOptions.GetDefaultProvider() != FireLakeLabs.NetClaw.Domain.Enums.AgentProviderKind.Copilot
            || !agentOptions.KeepContainerBoundary)
        {
            return;
        }

        logger.LogWarning(
            "Containerized Copilot requires a host token. Set NetClaw:AgentRuntime:CopilotGitHubToken, COPILOT_GITHUB_TOKEN, GH_TOKEN, or GITHUB_TOKEN on the host. CopilotUseLoggedInUser is not supported in the current containerized CLI path.");
    }

    private static bool IsClaude(HttpListenerRequest request)
    {
        string? path = request.Url?.AbsolutePath;
        if (path is not null && (path.Contains("/v1/messages", StringComparison.OrdinalIgnoreCase)
            || path.Contains("/v1/complete", StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        string? apiKeyHeader = request.Headers["x-api-key"];
        return !string.IsNullOrWhiteSpace(apiKeyHeader);
    }

    private static string GetListenerHost(string host)
    {
        if (host == "0.0.0.0" || host == "::" || host == "[::]")
        {
            return "+";
        }

        return host;
    }
}
