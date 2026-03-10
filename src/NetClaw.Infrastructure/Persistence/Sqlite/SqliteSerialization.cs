using System.Text.Json;
using NetClaw.Domain.Entities;

namespace NetClaw.Infrastructure.Persistence.Sqlite;

internal static class SqliteSerialization
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static string? SerializeContainerConfig(ContainerConfig? containerConfig)
    {
        return containerConfig is null ? null : JsonSerializer.Serialize(containerConfig, SerializerOptions);
    }

    public static ContainerConfig? DeserializeContainerConfig(string? payload)
    {
        return string.IsNullOrWhiteSpace(payload)
            ? null
            : JsonSerializer.Deserialize<ContainerConfig>(payload, SerializerOptions);
    }
}