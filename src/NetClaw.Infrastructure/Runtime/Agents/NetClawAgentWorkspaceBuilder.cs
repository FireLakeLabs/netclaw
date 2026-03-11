using NetClaw.Domain.Contracts.Agents;
using NetClaw.Domain.Contracts.Containers;
using NetClaw.Domain.Contracts.Services;
using NetClaw.Domain.Entities;
using NetClaw.Infrastructure.Configuration;
using NetClaw.Infrastructure.FileSystem;
using NetClaw.Infrastructure.Paths;

namespace NetClaw.Infrastructure.Runtime.Agents;

public sealed class NetClawAgentWorkspaceBuilder : IAgentWorkspaceBuilder
{
    private readonly AssistantIdentityOptions assistantIdentityOptions;
    private readonly IFileSystem fileSystem;
    private readonly GroupPathResolver groupPathResolver;
    private readonly StorageOptions storageOptions;

    public NetClawAgentWorkspaceBuilder(
        GroupPathResolver groupPathResolver,
        StorageOptions storageOptions,
        IFileSystem fileSystem,
        AssistantIdentityOptions assistantIdentityOptions)
    {
        this.groupPathResolver = groupPathResolver;
        this.storageOptions = storageOptions;
        this.fileSystem = fileSystem;
        this.assistantIdentityOptions = assistantIdentityOptions;
    }

    public async Task<AgentWorkspaceContext> BuildAsync(RegisteredGroup group, ContainerInput input, CancellationToken cancellationToken = default)
    {
        string workingDirectory = groupPathResolver.ResolveGroupDirectory(group.Folder);
        string sessionDirectory = groupPathResolver.ResolveGroupSessionDirectory(group.Folder);
        string workspaceDirectory = groupPathResolver.ResolveGroupAgentWorkspaceDirectory(group.Folder);

        fileSystem.CreateDirectory(workingDirectory);
        fileSystem.CreateDirectory(sessionDirectory);
        fileSystem.CreateDirectory(workspaceDirectory);

        List<string> additionalDirectories = [];
        string globalDirectory = Path.Combine(storageOptions.GroupsDirectory, "global");
        if (!group.IsMain && fileSystem.DirectoryExists(globalDirectory))
        {
            additionalDirectories.Add(fileSystem.GetFullPath(globalDirectory));
        }

        string assistantName = input.AssistantName ?? assistantIdentityOptions.Name;
        string instructions = string.Join(
            Environment.NewLine,
            [
                "# AGENTS.md",
                string.Empty,
                $"You are operating inside the NetClaw workspace for group '{group.Name}'.",
                $"Assistant name: {assistantName}",
                $"Group folder: {group.Folder.Value}",
                $"Main group: {(group.IsMain ? "yes" : "no")}",
                string.Empty,
                "Prioritize safe file operations, preserve existing project structure, and route all external actions through NetClaw-owned tools when available.",
                "Treat this file as the provider-neutral instruction surface."
            ]);

        AgentInstructionSet instructionSet = new(
            [new AgentInstructionDocument("AGENTS.md", instructions, true)],
            RuntimeAppendix: input.IsScheduledTask
                ? "This execution originated from a scheduled task."
                : "This execution originated from an interactive group flow.");

        await MaterializeInstructionsAsync(workingDirectory, workspaceDirectory, instructionSet, cancellationToken);

        AgentWorkspaceContext context = new(
            group.Folder,
            workingDirectory,
            sessionDirectory,
            workspaceDirectory,
            group.IsMain,
            additionalDirectories,
            instructionSet);

        return context;
    }

    private async Task MaterializeInstructionsAsync(
        string workingDirectory,
        string workspaceDirectory,
        AgentInstructionSet instructionSet,
        CancellationToken cancellationToken)
    {
        foreach (AgentInstructionDocument document in instructionSet.Documents)
        {
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

            if (document.IsGenerated || !fileSystem.FileExists(workingPath))
            {
                await fileSystem.WriteAllTextAsync(workingPath, document.Content, cancellationToken);
            }

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
}
