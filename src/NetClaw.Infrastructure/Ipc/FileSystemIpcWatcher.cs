using System.Text.Json;
using Microsoft.Extensions.Logging;
using NetClaw.Domain.Contracts.Ipc;
using NetClaw.Domain.Contracts.Persistence;
using NetClaw.Domain.Contracts.Services;
using NetClaw.Domain.Entities;
using NetClaw.Domain.Enums;
using NetClaw.Domain.ValueObjects;
using NetClaw.Infrastructure.Configuration;
using NetClaw.Infrastructure.FileSystem;

namespace NetClaw.Infrastructure.Ipc;

public sealed class FileSystemIpcWatcher : IIpcCommandWatcher
{
    private static readonly JsonDocumentOptions JsonOptions = new()
    {
        AllowTrailingCommas = true,
        CommentHandling = JsonCommentHandling.Skip
    };

    private readonly IFileSystem fileSystem;
    private readonly IGroupRepository groupRepository;
    private readonly ILogger<FileSystemIpcWatcher> logger;
    private readonly IIpcCommandProcessor processor;
    private readonly StorageOptions storageOptions;

    public FileSystemIpcWatcher(
        StorageOptions storageOptions,
        IFileSystem fileSystem,
        IGroupRepository groupRepository,
        IIpcCommandProcessor processor,
        ILogger<FileSystemIpcWatcher> logger)
    {
        this.storageOptions = storageOptions;
        this.fileSystem = fileSystem;
        this.groupRepository = groupRepository;
        this.processor = processor;
        this.logger = logger;
    }

    public async Task PollOnceAsync(CancellationToken cancellationToken = default)
    {
        string ipcRoot = Path.Combine(storageOptions.DataDirectory, "ipc");
        if (!fileSystem.DirectoryExists(ipcRoot))
        {
            return;
        }

        IReadOnlyDictionary<ChatJid, RegisteredGroup> groups = await groupRepository.GetAllAsync(cancellationToken);
        HashSet<string> mainGroupFolders = groups.Values
            .Where(group => group.IsMain)
            .Select(group => group.Folder.Value)
            .ToHashSet(StringComparer.Ordinal);

        foreach (string groupDirectory in fileSystem.GetDirectories(ipcRoot).OrderBy(Path.GetFileName, StringComparer.Ordinal))
        {
            string? sourceFolderName = Path.GetFileName(groupDirectory);
            if (string.IsNullOrWhiteSpace(sourceFolderName) || string.Equals(sourceFolderName, "errors", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            GroupFolder sourceGroup;
            try
            {
                sourceGroup = new GroupFolder(sourceFolderName);
            }
            catch (ArgumentException exception)
            {
                logger.LogWarning(exception, "Skipping invalid IPC source directory '{SourceFolderName}'.", sourceFolderName);
                continue;
            }

            bool isMainGroup = mainGroupFolders.Contains(sourceGroup.Value);

            await ProcessCommandDirectoryAsync(sourceGroup, isMainGroup, Path.Combine(groupDirectory, "messages"), cancellationToken);
            await ProcessCommandDirectoryAsync(sourceGroup, isMainGroup, Path.Combine(groupDirectory, "tasks"), cancellationToken);
        }
    }

    private async Task ProcessCommandDirectoryAsync(
        GroupFolder sourceGroup,
        bool isMainGroup,
        string directoryPath,
        CancellationToken cancellationToken)
    {
        if (!fileSystem.DirectoryExists(directoryPath))
        {
            return;
        }

        foreach (string filePath in fileSystem.GetFiles(directoryPath, "*.json").OrderBy(Path.GetFileName, StringComparer.Ordinal))
        {
            try
            {
                string content = await fileSystem.ReadAllTextAsync(filePath, cancellationToken);
                IpcCommand command = ParseCommand(content);
                await processor.ProcessAsync(sourceGroup, isMainGroup, command, cancellationToken);
                fileSystem.DeleteFile(filePath);
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Failed to process IPC file '{FilePath}' for group '{GroupFolder}'.", filePath, sourceGroup.Value);
                QuarantineFile(sourceGroup, filePath);
            }
        }
    }

    private void QuarantineFile(GroupFolder sourceGroup, string filePath)
    {
        if (!fileSystem.FileExists(filePath))
        {
            return;
        }

        string errorsDirectory = Path.Combine(storageOptions.DataDirectory, "ipc", "errors");
        fileSystem.CreateDirectory(errorsDirectory);

        string destinationFileName = $"{sourceGroup.Value}-{Path.GetFileName(filePath)}";
        string destinationPath = Path.Combine(errorsDirectory, destinationFileName);

        if (fileSystem.FileExists(destinationPath))
        {
            destinationPath = Path.Combine(errorsDirectory, $"{sourceGroup.Value}-{Guid.NewGuid():N}-{Path.GetFileName(filePath)}");
        }

        fileSystem.MoveFile(filePath, destinationPath);
    }

    private static IpcCommand ParseCommand(string content)
    {
        using JsonDocument document = JsonDocument.Parse(content, JsonOptions);
        JsonElement root = document.RootElement;

        if (root.ValueKind != JsonValueKind.Object)
        {
            throw new FormatException("IPC payload must be a JSON object.");
        }

        string type = GetRequiredString(root, "type");

        return type switch
        {
            "message" => new IpcMessageCommand(
                new ChatJid(GetRequiredString(root, "chatJid", "chat_jid")),
                GetRequiredString(root, "text")),
            "schedule_task" => new IpcTaskCommand(
                TryGetOptionalString(root, "taskId", "task_id") is { } taskId ? new TaskId(taskId) : null,
                GetRequiredString(root, "prompt"),
                ParseScheduleType(GetRequiredString(root, "scheduleType", "schedule_type")),
                GetRequiredString(root, "scheduleValue", "schedule_value"),
                ParseContextMode(TryGetOptionalString(root, "contextMode", "context_mode")),
                new ChatJid(GetRequiredString(root, "targetJid", "target_jid", "chatJid", "chat_jid"))),
            "register_group" => new IpcRegisterGroupCommand(
                new ChatJid(GetRequiredString(root, "jid")),
                GetRequiredString(root, "name"),
                new GroupFolder(GetRequiredString(root, "folder")),
                GetRequiredString(root, "trigger"),
                TryGetOptionalBoolean(root, true, "requiresTrigger", "requires_trigger"),
                TryGetOptionalBoolean(root, false, "isMain", "is_main"),
                TryGetProperty(root, out JsonElement containerConfigElement, "containerConfig", "container_config") && containerConfigElement.ValueKind != JsonValueKind.Null
                    ? ParseContainerConfig(containerConfigElement)
                    : null),
            _ => throw new FormatException($"Unsupported IPC command type '{type}'.")
        };
    }

    private static ContainerConfig ParseContainerConfig(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            throw new FormatException("containerConfig must be a JSON object.");
        }

        List<AdditionalMount> additionalMounts = [];
        if (TryGetProperty(element, out JsonElement mountsElement, "additionalMounts", "additional_mounts") && mountsElement.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement mountElement in mountsElement.EnumerateArray())
            {
                additionalMounts.Add(new AdditionalMount(
                    GetRequiredString(mountElement, "hostPath", "host_path"),
                    TryGetOptionalString(mountElement, "containerPath", "container_path"),
                    TryGetOptionalBoolean(mountElement, true, "readonly", "readOnly", "isReadOnly", "is_read_only")));
            }
        }

        TimeSpan? timeout = null;
        if (TryGetProperty(element, out JsonElement timeoutElement, "timeout"))
        {
            timeout = timeoutElement.ValueKind switch
            {
                JsonValueKind.Number when timeoutElement.TryGetInt64(out long timeoutMilliseconds) => TimeSpan.FromMilliseconds(timeoutMilliseconds),
                JsonValueKind.String when long.TryParse(timeoutElement.GetString(), out long timeoutMilliseconds) => TimeSpan.FromMilliseconds(timeoutMilliseconds),
                _ => throw new FormatException("containerConfig.timeout must be a number of milliseconds.")
            };
        }

        return new ContainerConfig(additionalMounts, timeout);
    }

    private static ScheduleType ParseScheduleType(string value)
    {
        return value.Trim().ToLowerInvariant() switch
        {
            "cron" => ScheduleType.Cron,
            "interval" => ScheduleType.Interval,
            "once" => ScheduleType.Once,
            _ => throw new FormatException($"Unsupported schedule type '{value}'.")
        };
    }

    private static TaskContextMode ParseContextMode(string? value)
    {
        return value?.Trim().ToLowerInvariant() switch
        {
            "group" => TaskContextMode.Group,
            "isolated" => TaskContextMode.Isolated,
            null or "" => TaskContextMode.Isolated,
            _ => TaskContextMode.Isolated
        };
    }

    private static string GetRequiredString(JsonElement element, params string[] propertyNames)
    {
        string? value = TryGetOptionalString(element, propertyNames);
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new FormatException($"Missing required IPC property '{propertyNames[0]}'.");
        }

        return value;
    }

    private static string? TryGetOptionalString(JsonElement element, params string[] propertyNames)
    {
        if (!TryGetProperty(element, out JsonElement property, propertyNames))
        {
            return null;
        }

        return property.ValueKind switch
        {
            JsonValueKind.String => property.GetString(),
            JsonValueKind.Number => property.GetRawText(),
            JsonValueKind.True => bool.TrueString.ToLowerInvariant(),
            JsonValueKind.False => bool.FalseString.ToLowerInvariant(),
            JsonValueKind.Null => null,
            _ => property.GetRawText()
        };
    }

    private static bool TryGetOptionalBoolean(JsonElement element, bool defaultValue, params string[] propertyNames)
    {
        if (!TryGetProperty(element, out JsonElement property, propertyNames))
        {
            return defaultValue;
        }

        return property.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.String when bool.TryParse(property.GetString(), out bool parsed) => parsed,
            _ => defaultValue
        };
    }

    private static bool TryGetProperty(JsonElement element, out JsonElement property, params string[] propertyNames)
    {
        foreach (string propertyName in propertyNames)
        {
            if (element.TryGetProperty(propertyName, out property))
            {
                return true;
            }
        }

        property = default;
        return false;
    }
}