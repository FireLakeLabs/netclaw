using System.Text.Json;
using System.Text.Json.Serialization;

namespace FireLakeLabs.NetClaw.Infrastructure.Persistence.FileSystem;

/// <summary>
/// Shared <see cref="JsonSerializerOptions"/> instances for file-based persistence.
/// All options use Web defaults (camelCase + case-insensitive) and string enum serialization.
/// </summary>
internal static class FileSystemJsonOptions
{
    private static readonly JsonStringEnumConverter EnumConverter = new(JsonNamingPolicy.CamelCase);

    /// <summary>
    /// For JSON config files (metadata.json, config.json, groups.json, etc.) — pretty-printed.
    /// </summary>
    public static readonly JsonSerializerOptions Config = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        Converters = { EnumConverter }
    };

    /// <summary>
    /// For JSONL append-only files (messages.jsonl, runs.jsonl, events/*.jsonl) — compact.
    /// </summary>
    public static readonly JsonSerializerOptions Jsonl = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = false,
        Converters = { EnumConverter }
    };
}
