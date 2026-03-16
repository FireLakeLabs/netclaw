using System.Text.Json;

namespace NetClaw.AgentRunner;

public static class IpcInputPoller
{
    private static readonly JsonDocumentOptions JsonDocOptions = new()
    {
        AllowTrailingCommas = true,
        CommentHandling = JsonCommentHandling.Skip
    };

    public static async Task<string?> WaitForInputAsync(string ipcInputDirectory, TimeSpan pollInterval, TimeSpan timeout, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(ipcInputDirectory);

        DateTimeOffset deadline = DateTimeOffset.UtcNow + timeout;

        while (DateTimeOffset.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (TryReadCloseSignal(ipcInputDirectory))
            {
                return null;
            }

            string? message = TryReadNextMessage(ipcInputDirectory);
            if (message is not null)
            {
                return message;
            }

            await Task.Delay(pollInterval, cancellationToken);
        }

        return null;
    }

    private static bool TryReadCloseSignal(string directory)
    {
        string closePath = Path.Combine(directory, "_close");
        if (!File.Exists(closePath))
        {
            return false;
        }

        try
        {
            File.Delete(closePath);
        }
        catch (IOException)
        {
            // Best-effort deletion
        }

        return true;
    }

    private static string? TryReadNextMessage(string directory)
    {
        string[] files;
        try
        {
            files = Directory.GetFiles(directory, "*.json");
        }
        catch (DirectoryNotFoundException)
        {
            return null;
        }

        if (files.Length == 0)
        {
            return null;
        }

        Array.Sort(files, StringComparer.Ordinal);
        string filePath = files[0];

        try
        {
            string content = File.ReadAllText(filePath);
            File.Delete(filePath);

            using JsonDocument document = JsonDocument.Parse(content, JsonDocOptions);
            if (document.RootElement.TryGetProperty("text", out JsonElement textElement))
            {
                return textElement.GetString();
            }

            return content;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[IpcInputPoller] Failed to read IPC input file '{filePath}': {ex.Message}");

            try
            {
                File.Delete(filePath);
            }
            catch (IOException)
            {
                // Best-effort cleanup
            }

            return null;
        }
    }
}
