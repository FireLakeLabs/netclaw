namespace NetClaw.Setup;

public sealed class SetupCommand
{
    private SetupCommand(string step, IReadOnlyDictionary<string, string?> options, IReadOnlySet<string> flags)
    {
        Step = step;
        Options = options;
        Flags = flags;
    }

    public string Step { get; }

    public IReadOnlyDictionary<string, string?> Options { get; }

    public IReadOnlySet<string> Flags { get; }

    public bool HasFlag(string name) => Flags.Contains(name);

    public string? GetOption(string name) => Options.TryGetValue(name, out string? value) ? value : null;

    public static SetupCommand Parse(string[] args)
    {
        Dictionary<string, string?> options = new(StringComparer.OrdinalIgnoreCase);
        HashSet<string> flags = new(StringComparer.OrdinalIgnoreCase);

        for (int index = 0; index < args.Length; index++)
        {
            string argument = args[index];
            if (!argument.StartsWith("--", StringComparison.Ordinal))
            {
                continue;
            }

            string name = argument[2..];
            string? value = null;

            if (index + 1 < args.Length && !args[index + 1].StartsWith("--", StringComparison.Ordinal))
            {
                value = args[++index];
                options[name] = value;
                continue;
            }

            flags.Add(name);
        }

        string step = options.TryGetValue("step", out string? configuredStep) && !string.IsNullOrWhiteSpace(configuredStep)
            ? configuredStep
            : string.Empty;

        return new SetupCommand(step, options, flags);
    }
}
