using System.Collections.Concurrent;
using NetClaw.Domain.Contracts.Containers;
using NetClaw.Domain.ValueObjects;

namespace NetClaw.Application.Execution;

public sealed class ActiveGroupSessionRegistry
{
    private readonly ConcurrentDictionary<string, IInteractiveContainerSession> sessions = new(StringComparer.Ordinal);

    public bool Register(ChatJid chatJid, IInteractiveContainerSession session)
    {
        return sessions.TryAdd(chatJid.Value, session);
    }

    public void Remove(ChatJid chatJid, IInteractiveContainerSession session)
    {
        if (sessions.TryGetValue(chatJid.Value, out IInteractiveContainerSession? current) && ReferenceEquals(current, session))
        {
            sessions.TryRemove(chatJid.Value, out _);
        }
    }

    public bool TryPostInput(ChatJid chatJid, string text)
    {
        return sessions.TryGetValue(chatJid.Value, out IInteractiveContainerSession? session) && session.TryPostInput(text);
    }

    public void RequestClose(ChatJid chatJid)
    {
        if (sessions.TryGetValue(chatJid.Value, out IInteractiveContainerSession? session))
        {
            session.RequestClose();
        }
    }
}