using System.Text.RegularExpressions;

namespace NetClaw.Domain.ValueObjects;

public readonly partial record struct GroupFolder
{
    private static readonly HashSet<string> ReservedFolders = new(StringComparer.OrdinalIgnoreCase)
    {
        "global"
    };

    public GroupFolder(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Group folder is required.", nameof(value));
        }

        if (!string.Equals(value, value.Trim(), StringComparison.Ordinal))
        {
            throw new ArgumentException("Group folder cannot contain leading or trailing whitespace.", nameof(value));
        }

        if (!FolderPattern().IsMatch(value))
        {
            throw new ArgumentException("Group folder must match the allowed naming pattern.", nameof(value));
        }

        if (value.Contains('/') || value.Contains('\\') || value.Contains("..", StringComparison.Ordinal))
        {
            throw new ArgumentException("Group folder cannot contain path traversal characters.", nameof(value));
        }

        if (ReservedFolders.Contains(value))
        {
            throw new ArgumentException("Group folder uses a reserved name.", nameof(value));
        }

        Value = value;
    }

    public string Value { get; }

    public override string ToString() => Value;

    [GeneratedRegex("^[A-Za-z0-9][A-Za-z0-9_-]{0,63}$", RegexOptions.CultureInvariant)]
    private static partial Regex FolderPattern();
}