using System.Diagnostics;
using System.Text;
using FireLakeLabs.NetClaw.Domain.Contracts.Containers;
using FireLakeLabs.NetClaw.Domain.Enums;
using FireLakeLabs.NetClaw.Domain.ValueObjects;

namespace FireLakeLabs.NetClaw.AgentRunner;

public sealed class ClaudeCodeAgentProvider : IAgentProvider
{
    public async Task<ContainerOutput> ExecuteAsync(AgentRunnerContext context, Action<ContainerOutput> onStreamOutput, CancellationToken cancellationToken)
    {
        string workingDir = "/workspace/group";
        string sessionDir = Environment.GetEnvironmentVariable("NETCLAW_SESSION_DIR") ?? "/home/user/.claude";

        Directory.CreateDirectory(sessionDir);

        string prompt = context.Input.Prompt;

        StringBuilder argsBuilder = new();
        argsBuilder.Append("--print ");

        if (context.Input.SessionId is { } sessionId)
        {
            argsBuilder.Append($"--resume {sessionId.Value} ");
        }

        ProcessStartInfo startInfo = new()
        {
            FileName = "claude",
            Arguments = argsBuilder.ToString().Trim(),
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = workingDir,
            Environment =
            {
                ["CLAUDE_CONFIG_DIR"] = sessionDir
            }
        };

        string? proxyUrl = Environment.GetEnvironmentVariable("NETCLAW_CREDENTIAL_PROXY_URL");
        if (!string.IsNullOrWhiteSpace(proxyUrl))
        {
            startInfo.Environment["ANTHROPIC_BASE_URL"] = proxyUrl;
        }

        startInfo.Environment["ANTHROPIC_API_KEY"] = Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY") ?? "placeholder";

        using Process process = new() { StartInfo = startInfo };

        process.Start();

        await process.StandardInput.WriteLineAsync(prompt);
        process.StandardInput.Close();

        StringBuilder outputBuilder = new();
        StringBuilder errorBuilder = new();

        Task outputTask = Task.Run(async () =>
        {
            string? line;
            while ((line = await process.StandardOutput.ReadLineAsync(cancellationToken)) is not null)
            {
                outputBuilder.AppendLine(line);
            }
        }, cancellationToken);

        Task errorTask = Task.Run(async () =>
        {
            string? line;
            while ((line = await process.StandardError.ReadLineAsync(cancellationToken)) is not null)
            {
                errorBuilder.AppendLine(line);
                Console.Error.WriteLine($"[claude] {line}");
            }
        }, cancellationToken);

        await Task.WhenAll(outputTask, errorTask);
        await process.WaitForExitAsync(cancellationToken);

        string result = outputBuilder.ToString().Trim();
        string error = errorBuilder.ToString().Trim();

        if (process.ExitCode != 0)
        {
            return new ContainerOutput(
                ContainerRunStatus.Error,
                string.IsNullOrWhiteSpace(result) ? null : result,
                null,
                string.IsNullOrWhiteSpace(error) ? $"Claude CLI exited with code {process.ExitCode}" : error);
        }

        SessionId? newSessionId = context.Input.SessionId;

        return new ContainerOutput(
            ContainerRunStatus.Success,
            string.IsNullOrWhiteSpace(result) ? null : result,
            newSessionId,
            null);
    }
}
