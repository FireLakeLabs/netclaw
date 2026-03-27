using System.Diagnostics;
using System.Text;
using System.Text.Json;
using FireLakeLabs.NetClaw.Domain.Contracts.Containers;
using FireLakeLabs.NetClaw.Domain.Enums;
using FireLakeLabs.NetClaw.Domain.ValueObjects;

namespace FireLakeLabs.NetClaw.AgentRunner;

public sealed class CopilotAgentProvider : IAgentProvider
{
    public async Task<ContainerOutput> ExecuteAsync(AgentRunnerContext context, Action<ContainerOutput> onStreamOutput, CancellationToken cancellationToken)
    {
        string? model = Environment.GetEnvironmentVariable("NETCLAW_COPILOT_MODEL");
        string workingDir = "/workspace/group";
        string sessionDir = Environment.GetEnvironmentVariable("NETCLAW_SESSION_DIR") ?? "/home/user/.copilot";

        Directory.CreateDirectory(sessionDir);

        string? reasoning = Environment.GetEnvironmentVariable("NETCLAW_COPILOT_REASONING_EFFORT");

        ProcessStartInfo startInfo = new()
        {
            FileName = "copilot",
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

        startInfo.ArgumentList.Add("--prompt");
        startInfo.ArgumentList.Add(context.Input.Prompt);
        if (!string.IsNullOrWhiteSpace(model))
        {
            startInfo.ArgumentList.Add("--model");
            startInfo.ArgumentList.Add(model);
        }
        startInfo.ArgumentList.Add("--allow-all");
        startInfo.ArgumentList.Add("--silent");
        startInfo.ArgumentList.Add("--stream");
        startInfo.ArgumentList.Add("off");
        startInfo.ArgumentList.Add("--no-ask-user");

        if (context.Input.SessionId is { } sessionId)
        {
            startInfo.ArgumentList.Add($"--resume={sessionId.Value}");
        }

        if (!string.IsNullOrWhiteSpace(reasoning))
        {
            startInfo.ArgumentList.Add("--reasoning-effort");
            startInfo.ArgumentList.Add(reasoning);
        }

        string? proxyUrl = Environment.GetEnvironmentVariable("NETCLAW_CREDENTIAL_PROXY_URL");
        if (!string.IsNullOrWhiteSpace(proxyUrl))
        {
            startInfo.Environment["COPILOT_CLI_URL"] = proxyUrl;
        }

        CopyIfPresent("COPILOT_GITHUB_TOKEN");
        CopyIfPresent("GH_TOKEN");
        CopyIfPresent("GITHUB_TOKEN");

        using Process process = new() { StartInfo = startInfo };

        process.Start();

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

        void CopyIfPresent(string name)
        {
            string? value = Environment.GetEnvironmentVariable(name);
            if (!string.IsNullOrWhiteSpace(value))
            {
                startInfo.Environment[name] = value;
            }
        }
    }
}
