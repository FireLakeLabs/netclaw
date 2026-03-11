using NetClaw.Domain.Enums;

namespace NetClaw.Domain.Entities;

public sealed record SenderAllowlist(SenderAllowlistRule Default, IReadOnlyDictionary<string, SenderAllowlistRule> Chats, bool LogDenied);

public sealed record SenderAllowlistRule(IReadOnlyList<string> AllowedSenders, bool AllowAll, SenderPolicyMode Mode);
