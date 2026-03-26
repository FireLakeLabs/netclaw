using FireLakeLabs.NetClaw.Infrastructure.Configuration;

namespace FireLakeLabs.NetClaw.Infrastructure.Persistence.FileSystem;

/// <summary>
/// Centralizes all file-based persistence path resolution.
/// All paths are derived from <see cref="StorageOptions"/> and are absolute.
/// </summary>
public sealed class FileStoragePaths
{
    public FileStoragePaths(StorageOptions storageOptions)
    {
        storageOptions.Validate();

        DataDirectory = storageOptions.DataDirectory;
        GroupsDirectory = storageOptions.GroupsDirectory;

        ChatsDirectory = Path.Combine(DataDirectory, "chats");
        TasksDirectory = Path.Combine(DataDirectory, "tasks");
        EventsDirectory = Path.Combine(DataDirectory, "events");

        StateFilePath = Path.Combine(DataDirectory, "state.json");
        GroupsFilePath = Path.Combine(DataDirectory, "groups.json");
        ChatGroupsFilePath = Path.Combine(DataDirectory, "chat-groups.json");
        EventCounterFilePath = Path.Combine(EventsDirectory, "next-id.txt");
    }

    public string DataDirectory { get; }
    public string GroupsDirectory { get; }
    public string ChatsDirectory { get; }
    public string TasksDirectory { get; }
    public string EventsDirectory { get; }
    public string StateFilePath { get; }
    public string GroupsFilePath { get; }
    public string ChatGroupsFilePath { get; }
    public string EventCounterFilePath { get; }

    public string ChatDirectory(string chatJid) =>
        Path.Combine(ChatsDirectory, chatJid);

    public string MessagesFilePath(string chatJid) =>
        Path.Combine(ChatsDirectory, chatJid, "messages.jsonl");

    public string ChatMetadataFilePath(string chatJid) =>
        Path.Combine(ChatsDirectory, chatJid, "metadata.json");

    public string AttachmentsDirectory(string chatJid) =>
        Path.Combine(ChatsDirectory, chatJid, "attachments");

    public string AttachmentFilePath(string chatJid, string fileId) =>
        Path.Combine(ChatsDirectory, chatJid, "attachments", fileId + ".json");

    public string TaskDirectory(string taskId) =>
        Path.Combine(TasksDirectory, taskId);

    public string TaskConfigFilePath(string taskId) =>
        Path.Combine(TasksDirectory, taskId, "config.json");

    public string TaskRunsFilePath(string taskId) =>
        Path.Combine(TasksDirectory, taskId, "runs.jsonl");

    public string EventsGroupDirectory(string groupFolder) =>
        Path.Combine(EventsDirectory, groupFolder);

    public string EventsDailyFilePath(string groupFolder, DateOnly date) =>
        Path.Combine(EventsDirectory, groupFolder, date.ToString("yyyy-MM-dd") + ".jsonl");

    public string SessionFilePath(string groupFolder) =>
        Path.Combine(GroupsDirectory, groupFolder, "session.json");
}
