using System.Text.Json;
using System.Text.Json.Serialization;
using NetClaw.Domain.Contracts.Containers;
using NetClaw.Domain.Enums;
using NetClaw.Domain.ValueObjects;

namespace NetClaw.AgentRunner;

public static class Program
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters =
        {
            new JsonStringEnumConverter(JsonNamingPolicy.CamelCase)
        }
    };

    public static async Task<int> Main(string[] args)
    {
        try
        {
            string stdinContent = await Console.In.ReadToEndAsync();

            if (string.IsNullOrWhiteSpace(stdinContent))
            {
                WriteError("No input received on stdin.");
                return 1;
            }

            ContainerInput? input = JsonSerializer.Deserialize<ContainerInput>(stdinContent, JsonOptions);

            if (input is null)
            {
                WriteError("Failed to deserialize ContainerInput from stdin.");
                return 1;
            }

            string providerEnv = Environment.GetEnvironmentVariable("NETCLAW_PROVIDER") ?? "copilot";

            IAgentProvider provider = providerEnv.ToLowerInvariant() switch
            {
                "copilot" => new CopilotAgentProvider(),
                "claude-code" or "claudecode" or "claude" => new ClaudeCodeAgentProvider(),
                _ => throw new InvalidOperationException($"Unsupported provider: {providerEnv}")
            };

            string ipcInputDir = "/workspace/ipc/input";
            string ipcMessagesDir = Path.Combine("/workspace/ipc", input.GroupFolder.Value, "messages");
            string ipcTasksDir = Path.Combine("/workspace/ipc", input.GroupFolder.Value, "tasks");

            Directory.CreateDirectory(ipcMessagesDir);
            Directory.CreateDirectory(ipcTasksDir);

            AgentRunnerContext context = new(input, ipcInputDir, ipcMessagesDir, ipcTasksDir);

            ContainerOutput result = await provider.ExecuteAsync(context, WriteOutputLine, CancellationToken.None);
            WriteOutputLine(result);

            return result.Status == ContainerRunStatus.Success ? 0 : 1;
        }
        catch (Exception ex)
        {
            WriteError($"Agent runner failed: {ex.Message}");
            WriteOutputLine(new ContainerOutput(ContainerRunStatus.Error, null, null, ex.Message));
            return 1;
        }
    }

    internal static void WriteOutputLine(ContainerOutput output)
    {
        string json = JsonSerializer.Serialize(output, JsonOptions);
        Console.Out.WriteLine(json);
        Console.Out.Flush();
    }

    private static void WriteError(string message)
    {
        Console.Error.WriteLine($"[AgentRunner] {message}");
    }
}

public sealed record AgentRunnerContext(
    ContainerInput Input,
    string IpcInputDirectory,
    string IpcMessagesDirectory,
    string IpcTasksDirectory);
