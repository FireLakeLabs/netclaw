namespace NetClaw.Setup;

public sealed record SetupResult(string StepName, int ExitCode, IReadOnlyDictionary<string, string> Status);
