using System.Diagnostics;
using System.Text;
using System.Text.Json;
using NetClaw.Domain.Contracts.Containers;
using NetClaw.Domain.Enums;
using NetClaw.Domain.ValueObjects;

namespace NetClaw.AgentRunner;

public sealed class CopilotAgentProvider : IAgentProvider
{
    public async Task<ContainerOutput> ExecuteAsync(AgentRunnerContext context, Action<ContainerOutput> onStreamOutput, CancellationToken cancellationToken)
    {
        string model = Environment.GetEnvironmentVariable("NETCLAW_COPILOT_MODEL") ?? "gpt-5";
        string workingDir = "/workspace/group";
        string sessionDir = Environment.GetEnvironmentVariable("NETCLAW_SESSION_DIR") ?? "/home/user/.copilot";

        Directory.CreateDirectory(sessionDir);

        string prompt = context.Input.Prompt;

        StringBuilder argsBuilder = new();
        argsBuilder.Append("--print ");
        argsBuilder.Append($"--model {model} ");

        if (context.Input.SessionId is { } sessionId)
        {
            argsBuilder.Append($"--session-id {sessionId.Value} ");
        }

        string? reasoning = Environment.GetEnvironmentVariable("NETCLAW_COPILOT_REASONING_EFFORT");
        if (!string.IsNullOrWhiteSpace(reasoning))
        {
            argsBuilder.Append($"--reasoning-effort {reasoning} ");
        }

        ProcessStartInfo startInfo = new()
        {
            FileName = "copilot",
            Arguments = argsBuilder.ToString().Trim(),
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = workingDir,
            Environment =
            {
                ["COPILOT_CONFIG_DIR"] = sessionDir
            }
        };

        string? proxyUrl = Environment.GetEnvironmentVariable("NETCLAW_CREDENTIAL_PROXY_URL");
        if (!string.IsNullOrWhiteSpace(proxyUrl))
        {
            startInfo.Environment["COPILOT_CLI_URL"] = proxyUrl;
        }

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
                Console.Error.WriteLine($"[copilot] {line}");
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
                string.IsNullOrWhiteSpace(error) ? $"Copilot CLI exited with code {process.ExitCode}" : error);
        }

        SessionId? newSessionId = context.Input.SessionId;

        return new ContainerOutput(
            ContainerRunStatus.Success,
            string.IsNullOrWhiteSpace(result) ? null : result,
            newSessionId,
            null);
    }
}
