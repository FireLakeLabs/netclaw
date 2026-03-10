using System.Text.Json;
using NetClaw.Domain.Contracts.Services;
using NetClaw.Domain.Entities;
using NetClaw.Domain.Enums;
using NetClaw.Domain.ValueObjects;
using NetClaw.Infrastructure.FileSystem;

namespace NetClaw.Infrastructure.Security;

public sealed class SenderAllowlistService : ISenderAuthorizationService
{
    private static readonly SenderAllowlist DefaultConfiguration = new(
        new SenderAllowlistRule([], AllowAll: true, SenderPolicyMode.Trigger),
        new Dictionary<string, SenderAllowlistRule>(),
        LogDenied: true);

    private readonly IFileSystem fileSystem;
    private SenderAllowlist configuration = DefaultConfiguration;

    public SenderAllowlistService(IFileSystem fileSystem)
    {
        this.fileSystem = fileSystem;
    }

    public IReadOnlyList<StoredMessage> ApplyInboundPolicy(ChatJid chatJid, IReadOnlyList<StoredMessage> messages)
    {
        SenderAllowlistRule rule = GetRule(chatJid);
        if (rule.Mode != SenderPolicyMode.Drop || rule.AllowAll)
        {
            return messages;
        }

        return messages.Where(message => message.IsFromMe || rule.AllowedSenders.Contains(message.Sender, StringComparer.Ordinal)).ToArray();
    }

    public bool CanTrigger(ChatJid chatJid, StoredMessage message)
    {
        if (message.IsFromMe)
        {
            return true;
        }

        SenderAllowlistRule rule = GetRule(chatJid);
        return rule.AllowAll || rule.AllowedSenders.Contains(message.Sender, StringComparer.Ordinal);
    }

    public async Task LoadAsync(string path, CancellationToken cancellationToken = default)
    {
        if (!fileSystem.FileExists(path))
        {
            configuration = DefaultConfiguration;
            return;
        }

        string json = await fileSystem.ReadAllTextAsync(path, cancellationToken);
        using JsonDocument document = JsonDocument.Parse(json, new JsonDocumentOptions { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip });
        if (!TryGetProperty(document.RootElement, out JsonElement defaultElement, "default") || defaultElement.ValueKind != JsonValueKind.Object)
        {
            configuration = DefaultConfiguration;
            return;
        }

        Dictionary<string, SenderAllowlistRule> chats = new(StringComparer.Ordinal);
        if (TryGetProperty(document.RootElement, out JsonElement chatsElement, "chats") && chatsElement.ValueKind == JsonValueKind.Object)
        {
            foreach (JsonProperty chatProperty in chatsElement.EnumerateObject())
            {
                if (chatProperty.Value.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                chats[chatProperty.Name] = BuildRule(chatProperty.Value);
            }
        }

        bool logDenied = !TryGetProperty(document.RootElement, out JsonElement logDeniedElement, "logDenied")
            || logDeniedElement.ValueKind != JsonValueKind.False;

        configuration = new SenderAllowlist(BuildRule(defaultElement), chats, logDenied);
    }

    private SenderAllowlistRule GetRule(ChatJid chatJid)
    {
        return configuration.Chats.TryGetValue(chatJid.Value, out SenderAllowlistRule? rule)
            ? rule
            : configuration.Default;
    }

    private static SenderAllowlistRule BuildRule(JsonElement element)
    {
        if (!TryGetProperty(element, out JsonElement allowElement, "allow"))
        {
            return DefaultConfiguration.Default;
        }

        bool allowAll = allowElement.ValueKind == JsonValueKind.String && string.Equals(allowElement.GetString(), "*", StringComparison.Ordinal);
        IReadOnlyList<string> allowedSenders = allowElement.ValueKind == JsonValueKind.Array
            ? allowElement.EnumerateArray().Where(item => item.ValueKind == JsonValueKind.String).Select(item => item.GetString()!).ToArray()
            : [];

        string? modeValue = TryGetProperty(element, out JsonElement modeElement, "mode") && modeElement.ValueKind == JsonValueKind.String
            ? modeElement.GetString()
            : null;
        SenderPolicyMode mode = string.Equals(modeValue, "drop", StringComparison.OrdinalIgnoreCase)
            ? SenderPolicyMode.Drop
            : SenderPolicyMode.Trigger;

        return new SenderAllowlistRule(allowedSenders, allowAll, mode);
    }

    private static bool TryGetProperty(JsonElement element, out JsonElement property, params string[] names)
    {
        foreach (string name in names)
        {
            if (element.TryGetProperty(name, out property))
            {
                return true;
            }
        }

        property = default;
        return false;
    }
}