using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using NetClaw.Domain.Contracts.Channels;
using NetClaw.Domain.Contracts.Services;
using NetClaw.Domain.Entities;

namespace NetClaw.Application.Formatting;

public sealed partial class XmlMessageFormatter : IMessageFormatter
{
    public string FormatInbound(IReadOnlyList<StoredMessage> messages, string timezone)
    {
        StringBuilder builder = new();
        builder.Append("<context timezone=\"")
            .Append(EscapeXml(timezone))
            .AppendLine("\" />")
            .AppendLine("<messages>");

        foreach (StoredMessage message in messages)
        {
            string displayTime = TimeZoneInfo.ConvertTime(message.Timestamp, ResolveTimeZone(timezone))
                .ToString("yyyy-MM-dd HH:mm:ss zzz", CultureInfo.InvariantCulture);

            builder.Append("<message sender=\"")
                .Append(EscapeXml(message.SenderName))
                .Append("\" time=\"")
                .Append(EscapeXml(displayTime))
                .Append("\">")
                .Append(EscapeXml(message.Content));

            if (message.Attachments.Count > 0)
            {
                builder.Append("<attachments>");
                foreach (var attachment in message.Attachments)
                {
                    builder.Append("<file name=\"")
                        .Append(EscapeXml(attachment.FileName))
                        .Append("\" path=\".uploads/")
                        .Append(EscapeXml(attachment.FileName))
                        .Append("\" size=\"")
                        .Append(attachment.FileSize)
                        .Append('"');
                    if (!string.IsNullOrWhiteSpace(attachment.MimeType))
                    {
                        builder.Append(" type=\"")
                            .Append(EscapeXml(attachment.MimeType))
                            .Append('"');
                    }

                    builder.Append(" />");
                }

                builder.Append("</attachments>");
            }

            builder.AppendLine("</message>");
        }

        builder.Append("</messages>");
        return builder.ToString();
    }

    public string NormalizeOutbound(string rawText)
    {
        string stripped = StripInternalTags(rawText);
        stripped = StripFileTags(stripped);
        return stripped.Trim();
    }

    public IReadOnlyList<OutboundFileReference> ExtractFileReferences(string rawText)
    {
        List<OutboundFileReference> references = [];
        foreach (Match match in FileTagRegex().Matches(rawText))
        {
            string path = match.Groups["path"].Value;
            if (string.IsNullOrWhiteSpace(path))
            {
                continue;
            }

            string? altText = match.Groups["alt"].Success && !string.IsNullOrWhiteSpace(match.Groups["alt"].Value)
                ? match.Groups["alt"].Value
                : null;
            references.Add(new OutboundFileReference(path, altText));
        }

        return references;
    }

    internal static string StripFileTags(string value)
    {
        return FileTagRegex().Replace(value, string.Empty);
    }

    [GeneratedRegex(@"<file\s+path=""(?<path>[^""]+)""\s*(?:/>|>(?<alt>.*?)</file>)", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex FileTagRegex();

    internal static string EscapeXml(string value)
    {
        return string.IsNullOrEmpty(value)
            ? string.Empty
            : value
                .Replace("&", "&amp;", StringComparison.Ordinal)
                .Replace("<", "&lt;", StringComparison.Ordinal)
                .Replace(">", "&gt;", StringComparison.Ordinal)
                .Replace("\"", "&quot;", StringComparison.Ordinal);
    }

    internal static string StripInternalTags(string value)
    {
        string current = value;
        const string startTag = "<internal>";
        const string endTag = "</internal>";

        while (true)
        {
            int startIndex = current.IndexOf(startTag, StringComparison.OrdinalIgnoreCase);
            if (startIndex < 0)
            {
                return current;
            }

            int endIndex = current.IndexOf(endTag, startIndex, StringComparison.OrdinalIgnoreCase);
            if (endIndex < 0)
            {
                return current[..startIndex];
            }

            current = current.Remove(startIndex, (endIndex + endTag.Length) - startIndex);
        }
    }

    private static TimeZoneInfo ResolveTimeZone(string timezone)
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(timezone);
        }
        catch (TimeZoneNotFoundException)
        {
            return TimeZoneInfo.Utc;
        }
    }
}
