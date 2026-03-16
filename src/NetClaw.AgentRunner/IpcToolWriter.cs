using System.Text.Json;
using System.Text.Json.Serialization;

namespace NetClaw.AgentRunner;

/// <summary>
/// Writes IPC tool calls as JSON files to the mounted IPC directories.
/// The host's FileSystemIpcWatcher polls these directories for pickup.
/// </summary>
public sealed class IpcToolWriter
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false
    };

    private readonly string messagesDirectory;
    private readonly string tasksDirectory;
    private int messageSequence;

    public IpcToolWriter(string messagesDirectory, string tasksDirectory)
    {
        this.messagesDirectory = messagesDirectory;
        this.tasksDirectory = tasksDirectory;
    }

    public async Task SendMessageAsync(string chatJid, string text)
    {
        Directory.CreateDirectory(messagesDirectory);
        int seq = Interlocked.Increment(ref messageSequence);
        string fileName = $"{DateTimeOffset.UtcNow:yyyyMMddHHmmss}-{seq:D4}.json";
        string filePath = Path.Combine(messagesDirectory, fileName);

        var payload = new { type = "message", chatJid, text };
        string json = JsonSerializer.Serialize(payload, JsonOptions);
        await File.WriteAllTextAsync(filePath, json);
    }

    public async Task ScheduleTaskAsync(string? taskId, string prompt, string scheduleType, string scheduleValue, string contextMode, string targetJid)
    {
        Directory.CreateDirectory(tasksDirectory);
        int seq = Interlocked.Increment(ref messageSequence);
        string fileName = $"{DateTimeOffset.UtcNow:yyyyMMddHHmmss}-task-{seq:D4}.json";
        string filePath = Path.Combine(tasksDirectory, fileName);

        var payload = new
        {
            type = "schedule_task",
            taskId,
            prompt,
            scheduleType,
            scheduleValue,
            contextMode,
            targetJid
        };
        string json = JsonSerializer.Serialize(payload, JsonOptions);
        await File.WriteAllTextAsync(filePath, json);
    }

    public async Task RegisterGroupAsync(string jid, string name, string folder, string trigger, bool requiresTrigger, bool isMain)
    {
        Directory.CreateDirectory(messagesDirectory);
        int seq = Interlocked.Increment(ref messageSequence);
        string fileName = $"{DateTimeOffset.UtcNow:yyyyMMddHHmmss}-reg-{seq:D4}.json";
        string filePath = Path.Combine(messagesDirectory, fileName);

        var payload = new
        {
            type = "register_group",
            jid,
            name,
            folder,
            trigger,
            requiresTrigger,
            isMain
        };
        string json = JsonSerializer.Serialize(payload, JsonOptions);
        await File.WriteAllTextAsync(filePath, json);
    }

    public async Task CloseGroupInputAsync(string chatJid)
    {
        Directory.CreateDirectory(messagesDirectory);
        int seq = Interlocked.Increment(ref messageSequence);
        string fileName = $"{DateTimeOffset.UtcNow:yyyyMMddHHmmss}-close-{seq:D4}.json";
        string filePath = Path.Combine(messagesDirectory, fileName);

        var payload = new { type = "message", chatJid, text = "/close" };
        string json = JsonSerializer.Serialize(payload, JsonOptions);
        await File.WriteAllTextAsync(filePath, json);
    }
}
