using System.Text.Json;
using System.Text.Json.Serialization;
using FireLakeLabs.NetClaw.Domain.Contracts.Containers;
using FireLakeLabs.NetClaw.Domain.Enums;
using FireLakeLabs.NetClaw.Domain.ValueObjects;

namespace FireLakeLabs.NetClaw.AgentRunner;

public static class Program
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters =
        {
            new JsonStringEnumConverter(JsonNamingPolicy.CamelCase),
            new GroupFolderJsonConverter(),
            new ChatJidJsonConverter(),
            new SessionIdJsonConverter()
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

internal sealed class GroupFolderJsonConverter : JsonConverter<GroupFolder>
{
    public override GroupFolder Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        string value = ValueObjectJson.ReadValueObjectString(ref reader, nameof(GroupFolder));
        return new GroupFolder(value);
    }

    public override void Write(Utf8JsonWriter writer, GroupFolder value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.Value);
    }
}

internal sealed class ChatJidJsonConverter : JsonConverter<ChatJid>
{
    public override ChatJid Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        string value = ValueObjectJson.ReadValueObjectString(ref reader, nameof(ChatJid));
        return new ChatJid(value);
    }

    public override void Write(Utf8JsonWriter writer, ChatJid value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.Value);
    }
}

internal sealed class SessionIdJsonConverter : JsonConverter<SessionId>
{
    public override SessionId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        string value = ValueObjectJson.ReadValueObjectString(ref reader, nameof(SessionId));
        return new SessionId(value);
    }

    public override void Write(Utf8JsonWriter writer, SessionId value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.Value);
    }
}

internal static class ValueObjectJson
{
    internal static string ReadValueObjectString(ref Utf8JsonReader reader, string typeName)
    {
        if (reader.TokenType == JsonTokenType.String)
        {
            string? directValue = reader.GetString();
            if (string.IsNullOrWhiteSpace(directValue))
            {
                throw new JsonException($"{typeName} cannot be empty.");
            }

            return directValue;
        }

        if (reader.TokenType == JsonTokenType.StartObject)
        {
            using JsonDocument document = JsonDocument.ParseValue(ref reader);
            if (document.RootElement.TryGetProperty("value", out JsonElement valueElement) && valueElement.ValueKind == JsonValueKind.String)
            {
                string? objectValue = valueElement.GetString();
                if (string.IsNullOrWhiteSpace(objectValue))
                {
                    throw new JsonException($"{typeName}.value cannot be empty.");
                }

                return objectValue;
            }

            throw new JsonException($"Expected object with string property 'value' for {typeName}.");
        }

        throw new JsonException($"Expected string or object token for {typeName}.");
    }
}

public sealed record AgentRunnerContext(
    ContainerInput Input,
    string IpcInputDirectory,
    string IpcMessagesDirectory,
    string IpcTasksDirectory);
