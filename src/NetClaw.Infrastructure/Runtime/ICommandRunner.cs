namespace NetClaw.Infrastructure.Runtime;

public interface ICommandRunner
{
    Task<CommandResult> RunAsync(string fileName, string arguments, CancellationToken cancellationToken = default);
}
