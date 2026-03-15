using NetClaw.Domain.Entities;

namespace NetClaw.Application.Observability;

public interface IMessageNotifier
{
    void NotifyNewMessage(StoredMessage message);
}

public sealed class NullMessageNotifier : IMessageNotifier
{
    public void NotifyNewMessage(StoredMessage message) { }
}
