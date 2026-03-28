using System.Globalization;
using System.Text.RegularExpressions;
using FireLakeLabs.NetClaw.Domain.Contracts.Agents;
using FireLakeLabs.NetClaw.Domain.Contracts.Containers;
using FireLakeLabs.NetClaw.Domain.Contracts.Services;
using FireLakeLabs.NetClaw.Domain.Entities;
using FireLakeLabs.NetClaw.Domain.Enums;
using FireLakeLabs.NetClaw.Infrastructure.Configuration;
using FireLakeLabs.NetClaw.Infrastructure.FileSystem;
using FireLakeLabs.NetClaw.Infrastructure.Paths;

namespace FireLakeLabs.NetClaw.Infrastructure.Runtime.Agents;

public sealed class NetClawAgentWorkspaceBuilder : IAgentWorkspaceBuilder
{
    private const int HeaderWidth = 55;
    private const int TotalInjectedCharsMax = 30000;
    private const int SoulMaxChars = 4000;
    private const int IdentityMaxChars = 1000;
    private const int UserMaxChars = 2000;
    private const int AgentsMaxChars = 8000;
    private const int ToolsMaxChars = 4000;
    private const int MemoryMaxChars = 8000;
    private const int DailyMemoryMaxChars = 4000;
    private const int BootstrapMaxChars = 2000;
    private const int RuntimeMaxChars = 6000;

    private const string DefaultSoul = """
# SOUL.md - Who You Are

*You're not a chatbot. You're becoming someone.*

## Core Truths

Be genuinely helpful. Be resourceful before asking. Earn trust with competence.

## Boundaries

- Private things stay private.
- Ask before external actions.
- Be careful in group chats.

## Continuity

Workspace files are your memory across sessions.
""";

    private const string DefaultAgents = """
# AGENTS.md - How You Operate

This workspace is home. Treat it that way.

## First Run

If BOOTSTRAP.md exists, follow it first. This overrides normal greeting and task flow.

## Memory

- Daily notes: memory/YYYY-MM-DD.md
- Long-term: MEMORY.md

## When Given No Task

If the user says things like "you tell me", "up to you", or gives no concrete task:

1. Read MEMORY.md and the most recent daily note if they exist.
2. Summarize active or recently touched work.
3. Propose 1-3 concrete next actions with a brief reason for each.
4. Start with the most useful option unless the user chooses differently.

## Safety

- Don't exfiltrate private data.
- Don't run destructive commands without asking.
""";

    private const string CoreOnboarding = """
If the user gives no concrete task (for example: "you tell me"), do not default to a generic intro.

Read MEMORY.md and the most recent daily note if present, summarize active work, and propose 1-3 concrete next actions with a brief reason for each.

Then begin with the most useful option unless the user chooses differently.
""";

    private const string CoreBootstrapPolicy = """
Bootstrap mode is mandatory when BOOTSTRAP.md is present.

If BOOTSTRAP.md exists in the injected documents:

1. Do onboarding first, before normal help/task handling.
2. Treat greetings or small talk as the start of onboarding, not as a generic chat turn.
3. Ask for missing identity details, write updates to IDENTITY.md/USER.md/SOUL.md as directed by BOOTSTRAP.md, then delete BOOTSTRAP.md.
4. Do not postpone bootstrap work unless the user explicitly asks to skip it.
""";

    private readonly AssistantIdentityOptions assistantIdentityOptions;
    private readonly IFileSystem fileSystem;
    private readonly GroupPathResolver groupPathResolver;
    private readonly MessageLoopOptions messageLoopOptions;
    private readonly StorageOptions storageOptions;

    public NetClawAgentWorkspaceBuilder(
        GroupPathResolver groupPathResolver,
        StorageOptions storageOptions,
        MessageLoopOptions messageLoopOptions,
        IFileSystem fileSystem,
        AssistantIdentityOptions assistantIdentityOptions)
    {
        this.groupPathResolver = groupPathResolver;
        this.storageOptions = storageOptions;
        this.messageLoopOptions = messageLoopOptions;
        this.fileSystem = fileSystem;
        this.assistantIdentityOptions = assistantIdentityOptions;
    }

    public async Task<AgentWorkspaceContext> BuildAsync(
        RegisteredGroup group,
        ContainerInput input,
        SessionScope sessionScope = SessionScope.Group,
        CancellationToken cancellationToken = default)
    {
        string workingDirectory = groupPathResolver.ResolveGroupDirectory(group.Folder);
        string sessionDirectory = groupPathResolver.ResolveGroupSessionDirectory(group.Folder);
        string workspaceDirectory = groupPathResolver.ResolveGroupAgentWorkspaceDirectory(group.Folder);
        string sourceWorkspaceDirectory = groupPathResolver.ResolveGroupDirectory(group.Folder);

        fileSystem.CreateDirectory(workingDirectory);
        fileSystem.CreateDirectory(sessionDirectory);
        fileSystem.CreateDirectory(workspaceDirectory);

        List<string> additionalDirectories = [];
        string globalDirectory = Path.Combine(storageOptions.GroupsDirectory, "global");
        if (!group.IsMain && fileSystem.DirectoryExists(globalDirectory))
        {
            additionalDirectories.Add(fileSystem.GetFullPath(globalDirectory));
        }

        string timezone = string.IsNullOrWhiteSpace(messageLoopOptions.Timezone)
            ? "UTC"
            : messageLoopOptions.Timezone;
        DateTimeOffset now = ResolveNow(timezone);

        List<InstructionPart> parts = [];

        string? resolvedName = await ParseAgentNameAsync(sourceWorkspaceDirectory, cancellationToken);
        string identityName = resolvedName ?? assistantIdentityOptions.Name ?? "an assistant";
        parts.Add(new InstructionPart(
            "NETCLAW_IDENTITY_PREAMBLE.md",
            WrapWithHeader(
                "NETCLAW_IDENTITY_PREAMBLE.md",
                $"You are {identityName}, a personal assistant running in the NetClaw platform. The following documents define your personality, context, and operational guidelines."),
            isGenerated: true,
            IsCore: true));

        parts.Add(new InstructionPart(
            "NETCLAW_ONBOARDING.md",
            WrapWithHeader("NETCLAW_ONBOARDING.md", CoreOnboarding),
            isGenerated: true,
            IsCore: true));

        if (sessionScope != SessionScope.Subagent)
        {
            if (fileSystem.FileExists(Path.Combine(sourceWorkspaceDirectory, "BOOTSTRAP.md")))
            {
                parts.Add(new InstructionPart(
                    "NETCLAW_BOOTSTRAP_POLICY.md",
                    WrapWithHeader("NETCLAW_BOOTSTRAP_POLICY.md", CoreBootstrapPolicy),
                    isGenerated: true,
                    IsCore: true));
            }

            await TryAddExistingFileAsync(parts, sourceWorkspaceDirectory, "BOOTSTRAP.md", BootstrapMaxChars, cancellationToken);
        }

        await AddRequiredFileOrDefaultAsync(parts, sourceWorkspaceDirectory, "SOUL.md", SoulMaxChars, DefaultSoul, cancellationToken);

        if (sessionScope is SessionScope.Private or SessionScope.Group)
        {
            await TryAddExistingFileAsync(parts, sourceWorkspaceDirectory, "IDENTITY.md", IdentityMaxChars, cancellationToken);
            if (sessionScope == SessionScope.Private)
            {
                await TryAddExistingFileAsync(parts, sourceWorkspaceDirectory, "USER.md", UserMaxChars, cancellationToken);
            }

            await AddRequiredFileOrDefaultAsync(parts, sourceWorkspaceDirectory, "AGENTS.md", AgentsMaxChars, DefaultAgents, cancellationToken);
            await TryAddExistingFileAsync(parts, sourceWorkspaceDirectory, "TOOLS.md", ToolsMaxChars, cancellationToken);
        }
        else
        {
            await TryAddExistingFileAsync(parts, sourceWorkspaceDirectory, "IDENTITY.md", IdentityMaxChars, cancellationToken);
        }

        if (sessionScope == SessionScope.Private)
        {
            await TryAddExistingFileAsync(parts, sourceWorkspaceDirectory, "MEMORY.md", MemoryMaxChars, cancellationToken);

            string memoryDirectory = Path.Combine(sourceWorkspaceDirectory, "memory");
            string today = now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            string yesterday = now.AddDays(-1).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

            await TryAddExistingFileAsync(parts, memoryDirectory, $"{today}.md", DailyMemoryMaxChars, cancellationToken, $"memory/{today}.md", isDailyMemory: true);
            await TryAddExistingFileAsync(parts, memoryDirectory, $"{yesterday}.md", DailyMemoryMaxChars, cancellationToken, $"memory/{yesterday}.md", isDailyMemory: true);
        }

        string runtimeContent = GenerateRuntimeContext(group, sourceWorkspaceDirectory, now, timezone);
        parts.Add(new InstructionPart(
            "NETCLAW_RUNTIME.md",
            WrapWithHeader("NETCLAW_RUNTIME.md", Truncate(runtimeContent, RuntimeMaxChars)),
            isGenerated: true,
            IsCore: true));

        ApplyTotalCap(parts, TotalInjectedCharsMax);

        AgentInstructionSet instructionSet = new(
            parts
                .Where(static p => !string.IsNullOrWhiteSpace(p.Content))
                .Select(static p => new AgentInstructionDocument(p.RelativePath, p.Content, p.IsGenerated))
                .ToArray(),
            RuntimeAppendix: input.IsScheduledTask
                ? "This execution originated from a scheduled task."
                : "This execution originated from an interactive group flow.");

        await MaterializeInstructionsAsync(workingDirectory, workspaceDirectory, instructionSet, cancellationToken);

        return new AgentWorkspaceContext(
            group.Folder,
            workingDirectory,
            sessionDirectory,
            workspaceDirectory,
            group.IsMain,
            additionalDirectories,
            instructionSet);
    }

    private async Task AddRequiredFileOrDefaultAsync(
        List<InstructionPart> parts,
        string rootDirectory,
        string fileName,
        int maxChars,
        string fallback,
        CancellationToken cancellationToken)
    {
        string path = Path.Combine(rootDirectory, fileName);
        if (fileSystem.FileExists(path))
        {
            string content = await fileSystem.ReadAllTextAsync(path, cancellationToken);
            parts.Add(new InstructionPart(fileName, WrapWithHeader(fileName, Truncate(content, maxChars)), isGenerated: false, IsCore: true));
            return;
        }

        parts.Add(new InstructionPart(fileName, WrapWithHeader(fileName, Truncate(fallback, maxChars)), isGenerated: true, IsCore: true));
    }

    private async Task TryAddExistingFileAsync(
        List<InstructionPart> parts,
        string rootDirectory,
        string fileName,
        int maxChars,
        CancellationToken cancellationToken,
        string? relativePath = null,
        bool isDailyMemory = false)
    {
        string path = Path.Combine(rootDirectory, fileName);
        if (!fileSystem.FileExists(path))
        {
            return;
        }

        string content = await fileSystem.ReadAllTextAsync(path, cancellationToken);
        string name = relativePath ?? fileName;
        parts.Add(new InstructionPart(name, WrapWithHeader(name, Truncate(content, maxChars)), isGenerated: false, IsDailyMemory: isDailyMemory));
    }

    private async Task<string?> ParseAgentNameAsync(string workspaceDirectory, CancellationToken cancellationToken)
    {
        string identityPath = Path.Combine(workspaceDirectory, "IDENTITY.md");
        if (!fileSystem.FileExists(identityPath))
        {
            return null;
        }

        string content = await fileSystem.ReadAllTextAsync(identityPath, cancellationToken);
        Match match = Regex.Match(content, @"^\s*-?\s*\*\*Name:\*\*\s*(.*)$", RegexOptions.Multiline);
        if (!match.Success)
        {
            return null;
        }

        string candidate = match.Groups[1].Value.Trim();
        candidate = StripWrapping(candidate);

        if (string.IsNullOrWhiteSpace(candidate))
        {
            return null;
        }

        string normalized = candidate.Trim().ToLowerInvariant();
        if (normalized is "pick something you like"
            or "tbd"
            or "todo"
            or "(none)"
            or "none"
            or "n/a"
            or "...")
        {
            return null;
        }

        return candidate;
    }

    private static string StripWrapping(string value)
    {
        string result = value.Trim();

        if (result.StartsWith("*(", StringComparison.Ordinal) && result.EndsWith(")*", StringComparison.Ordinal) && result.Length > 4)
        {
            result = result[2..^2].Trim();
        }

        if (result.Length >= 2)
        {
            if ((result[0] == '"' && result[^1] == '"')
                || (result[0] == '\'' && result[^1] == '\'')
                || (result[0] == '`' && result[^1] == '`')
                || (result[0] == '(' && result[^1] == ')'))
            {
                result = result[1..^1].Trim();
            }
        }

        return result;
    }

    private static DateTimeOffset ResolveNow(string timezone)
    {
        try
        {
            TimeZoneInfo zone = TimeZoneInfo.FindSystemTimeZoneById(timezone);
            return TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, zone);
        }
        catch
        {
            return DateTimeOffset.UtcNow;
        }
    }

    private static string GenerateRuntimeContext(RegisteredGroup group, string workspaceDirectory, DateTimeOffset now, string timezone)
    {
        return string.Join(
            Environment.NewLine,
            [
                "# NetClaw Runtime Context",
                string.Empty,
                "## Workspace",
                $"You're working in the {group.Name} workspace. Your workspace directory is: {workspaceDirectory}",
                string.Empty,
                "## Current Time",
                $"{now:yyyy-MM-dd HH:mm:ss zzz} ({timezone})",
                string.Empty,
                "## Available Tools",
                "You have access to tools for scheduling, group management, and messaging.",
                string.Empty,
                "## File Operations",
                "When referencing file content, use file tags: <file path=\"relative/path\"/>.",
                "Paths are relative to the workspace root.",
                string.Empty,
                "## Memory File Conventions",
                "- Daily logs: memory/YYYY-MM-DD.md",
                "- Long-term memory: MEMORY.md",
                "- Create/update these files directly when useful"
            ]);
    }

    private static string WrapWithHeader(string filename, string content)
    {
        int padLength = Math.Max(0, HeaderWidth - filename.Length - 8);
        string header = "====== " + filename + " " + new string('=', padLength);
        return header + Environment.NewLine + content;
    }

    private static string Truncate(string content, int maxChars)
    {
        if (string.IsNullOrEmpty(content) || content.Length <= maxChars)
        {
            return content;
        }

        return content[..maxChars];
    }

    private static void ApplyTotalCap(List<InstructionPart> parts, int maxChars)
    {
        int total = parts.Sum(static p => p.Content.Length);
        if (total <= maxChars)
        {
            return;
        }

        foreach (InstructionPart dailyPart in parts.Where(static p => p.IsDailyMemory).OrderBy(static p => p.RelativePath, StringComparer.Ordinal))
        {
            if (total <= maxChars)
            {
                return;
            }

            total -= dailyPart.Content.Length;
            dailyPart.Content = string.Empty;
        }

        if (total <= maxChars)
        {
            return;
        }

        InstructionPart? memoryPart = parts.FirstOrDefault(static p => p.RelativePath == "MEMORY.md");
        if (memoryPart is null || string.IsNullOrEmpty(memoryPart.Content))
        {
            return;
        }

        int overflow = total - maxChars;
        if (overflow >= memoryPart.Content.Length)
        {
            memoryPart.Content = string.Empty;
            return;
        }

        memoryPart.Content = memoryPart.Content[..^overflow];
    }

    private async Task MaterializeInstructionsAsync(
        string workingDirectory,
        string workspaceDirectory,
        AgentInstructionSet instructionSet,
        CancellationToken cancellationToken)
    {
        foreach (AgentInstructionDocument document in instructionSet.Documents)
        {
            if (!document.IsGenerated)
            {
                continue;
            }

            string workingPath = ResolveDocumentPath(workingDirectory, document.RelativePath);
            string workspacePath = ResolveDocumentPath(workspaceDirectory, document.RelativePath);

            string? workingParent = Path.GetDirectoryName(workingPath);
            if (!string.IsNullOrWhiteSpace(workingParent))
            {
                fileSystem.CreateDirectory(workingParent);
            }

            string? workspaceParent = Path.GetDirectoryName(workspacePath);
            if (!string.IsNullOrWhiteSpace(workspaceParent))
            {
                fileSystem.CreateDirectory(workspaceParent);
            }

            await fileSystem.WriteAllTextAsync(workingPath, document.Content, cancellationToken);
            await fileSystem.WriteAllTextAsync(workspacePath, document.Content, cancellationToken);
        }
    }

    private string ResolveDocumentPath(string root, string relativePath)
    {
        string fullRoot = fileSystem.GetFullPath(root);
        string fullPath = fileSystem.GetFullPath(Path.Combine(root, relativePath));
        string relative = Path.GetRelativePath(fullRoot, fullPath);

        if (relative.StartsWith("..", StringComparison.Ordinal) || Path.IsPathFullyQualified(relative))
        {
            throw new InvalidOperationException($"Instruction path escapes workspace root: {relativePath}");
        }

        return fullPath;
    }

    private sealed class InstructionPart
    {
        public InstructionPart(string relativePath, string content, bool isGenerated, bool IsCore = false, bool IsDailyMemory = false)
        {
            RelativePath = relativePath;
            Content = content;
            IsGenerated = isGenerated;
            this.IsCore = IsCore;
            this.IsDailyMemory = IsDailyMemory;
        }

        public string RelativePath { get; }

        public string Content { get; set; }

        public bool IsGenerated { get; }

        public bool IsCore { get; }

        public bool IsDailyMemory { get; }
    }
}
