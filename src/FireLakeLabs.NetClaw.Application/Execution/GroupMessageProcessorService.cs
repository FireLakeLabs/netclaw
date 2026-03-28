using FireLakeLabs.NetClaw.Application.Observability;
using FireLakeLabs.NetClaw.Domain.Contracts.Channels;
using FireLakeLabs.NetClaw.Domain.Contracts.Containers;
using FireLakeLabs.NetClaw.Domain.Contracts.Persistence;
using FireLakeLabs.NetClaw.Domain.Contracts.Services;
using FireLakeLabs.NetClaw.Domain.Entities;
using FireLakeLabs.NetClaw.Domain.Enums;
using FireLakeLabs.NetClaw.Domain.ValueObjects;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace FireLakeLabs.NetClaw.Application.Execution;

public sealed class GroupMessageProcessorService
{
    private readonly IAgentRuntime agentRuntime;
    private readonly IAgentEventSink agentEventSink;
    private readonly ActiveGroupSessionRegistry activeSessionRegistry;
    private readonly string assistantName;
    private readonly IReadOnlyList<IChannel> channels;
    private readonly IGroupExecutionQueue groupExecutionQueue;
    private readonly ILogger<GroupMessageProcessorService> logger;
    private readonly IMessageFormatter messageFormatter;
    private readonly IMessageRepository messageRepository;
    private readonly IGroupRepository groupRepository;
    private readonly IFileAttachmentRepository fileAttachmentRepository;
    private readonly IOutboundRouter outboundRouter;
    private readonly IRouterStateRepository routerStateRepository;
    private readonly ISenderAuthorizationService senderAuthorizationService;
    private readonly string timezone;
    private readonly string groupsDirectory;

    public GroupMessageProcessorService(
        IMessageRepository messageRepository,
        IGroupRepository groupRepository,
        IRouterStateRepository routerStateRepository,
        ISenderAuthorizationService senderAuthorizationService,
        IMessageFormatter messageFormatter,
        IOutboundRouter outboundRouter,
        IAgentRuntime agentRuntime,
        IGroupExecutionQueue groupExecutionQueue,
        ActiveGroupSessionRegistry activeSessionRegistry,
        IReadOnlyList<IChannel> channels,
        IAgentEventSink agentEventSink,
        IFileAttachmentRepository fileAttachmentRepository,
        string assistantName,
        string timezone,
        string groupsDirectory)
        : this(messageRepository, groupRepository, routerStateRepository, senderAuthorizationService,
               messageFormatter, outboundRouter, agentRuntime, groupExecutionQueue, activeSessionRegistry,
               channels, agentEventSink, fileAttachmentRepository, assistantName, timezone, groupsDirectory,
               NullLogger<GroupMessageProcessorService>.Instance)
    {
    }

    public GroupMessageProcessorService(
        IMessageRepository messageRepository,
        IGroupRepository groupRepository,
        IRouterStateRepository routerStateRepository,
        ISenderAuthorizationService senderAuthorizationService,
        IMessageFormatter messageFormatter,
        IOutboundRouter outboundRouter,
        IAgentRuntime agentRuntime,
        IGroupExecutionQueue groupExecutionQueue,
        ActiveGroupSessionRegistry activeSessionRegistry,
        IReadOnlyList<IChannel> channels,
        IAgentEventSink agentEventSink,
        IFileAttachmentRepository fileAttachmentRepository,
        string assistantName,
        string timezone,
        string groupsDirectory,
        ILogger<GroupMessageProcessorService> logger)
    {
        this.messageRepository = messageRepository;
        this.groupRepository = groupRepository;
        this.routerStateRepository = routerStateRepository;
        this.senderAuthorizationService = senderAuthorizationService;
        this.messageFormatter = messageFormatter;
        this.outboundRouter = outboundRouter;
        this.agentRuntime = agentRuntime;
        this.groupExecutionQueue = groupExecutionQueue;
        this.activeSessionRegistry = activeSessionRegistry;
        this.channels = channels;
        this.agentEventSink = agentEventSink;
        this.fileAttachmentRepository = fileAttachmentRepository;
        this.assistantName = assistantName;
        this.timezone = timezone;
        this.groupsDirectory = groupsDirectory;
        this.logger = logger;
    }

    public async Task<bool> ProcessAsync(ChatJid groupJid, CancellationToken cancellationToken = default)
    {
        IChannel? channel = channels.FirstOrDefault(candidate => candidate.IsConnected && candidate.Owns(groupJid));

        try
        {
            RegisteredGroup? group = await groupRepository.GetByJidAsync(groupJid, cancellationToken);
            if (group is null)
            {
                return true;
            }

            DateTimeOffset? lastAgentTimestamp = await GetLastAgentTimestampAsync(groupJid, cancellationToken);
            IReadOnlyList<StoredMessage> pendingMessages = senderAuthorizationService.ApplyInboundPolicy(
                groupJid,
                await messageRepository.GetMessagesSinceAsync(groupJid, lastAgentTimestamp, assistantName, cancellationToken));
            if (pendingMessages.Count == 0)
            {
                return true;
            }

            if (!group.IsMain && group.RequiresTrigger && !pendingMessages.Any(message => senderAuthorizationService.CanTrigger(groupJid, message) && ContainsTrigger(message.Content, group.Trigger)))
            {
                return true;
            }

            pendingMessages = await EnrichWithAttachmentsAsync(pendingMessages, groupJid, cancellationToken);
            StageAttachmentFiles(pendingMessages, group.Folder);

            string prompt = messageFormatter.FormatInbound(pendingMessages, timezone);
            bool streamedCompletedMessage = false;
            try
            {
                await SetTypingAsync(channel, groupJid, isTyping: true, cancellationToken);

                await using IInteractiveContainerSession interactiveSession = await agentRuntime.StartInteractiveSessionAsync(
                    new ContainerInput(prompt, null, group.Folder, groupJid, group.IsMain, false, assistantName, ResolveSessionScope(channel)),
                    async (streamEvent, ct) =>
                    {
                        agentEventSink.Record(streamEvent, group.Folder, groupJid, isScheduledTask: false, taskId: null);

                        switch (streamEvent.Kind)
                        {
                            case ContainerEventKind.MessageCompleted:
                                {
                                    string rawOutput = streamEvent.Output.Result ?? string.Empty;
                                    try
                                    {
                                        await RouteFileReferencesAsync(rawOutput, group.Folder, groupJid, ct);
                                    }
                                    catch (Exception ex)
                                    {
                                        logger.LogWarning(ex, "Failed to route outbound file references for {GroupJid}.", groupJid.Value);
                                    }

                                    string text = messageFormatter.NormalizeOutbound(rawOutput);
                                    if (!string.IsNullOrWhiteSpace(text))
                                    {
                                        await outboundRouter.RouteAsync(channels, groupJid, text, ct);
                                        streamedCompletedMessage = true;
                                    }

                                    break;
                                }
                            case ContainerEventKind.Idle:
                                groupExecutionQueue.NotifyIdle(groupJid);
                                break;
                        }
                    },
                    cancellationToken: cancellationToken);

                activeSessionRegistry.Register(groupJid, interactiveSession);
                ContainerExecutionResult executionResult;
                try
                {
                    executionResult = await interactiveSession.Completion;
                }
                finally
                {
                    activeSessionRegistry.Remove(groupJid, interactiveSession);
                }

                if (executionResult.Status != ContainerRunStatus.Success)
                {
                    return false;
                }

                string outboundText = messageFormatter.NormalizeOutbound(executionResult.Result ?? string.Empty);
                if (!streamedCompletedMessage)
                {
                    try
                    {
                        await RouteFileReferencesAsync(executionResult.Result ?? string.Empty, group.Folder, groupJid, cancellationToken);
                    }
                    catch (Exception ex)
                    {
                        logger.LogWarning(ex, "Failed to route outbound file references for {GroupJid}.", groupJid.Value);
                    }

                    if (!string.IsNullOrWhiteSpace(outboundText))
                    {
                        await outboundRouter.RouteAsync(channels, groupJid, outboundText, cancellationToken);
                    }
                }

                await routerStateRepository.SetAsync(
                    new RouterStateEntry(GetLastAgentTimestampKey(groupJid), pendingMessages[^1].Timestamp.ToString("O")),
                    cancellationToken);

                return true;
            }
            finally
            {
                await SetTypingAsync(channel, groupJid, isTyping: false, cancellationToken);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            return false;
        }
    }

    private async Task<DateTimeOffset?> GetLastAgentTimestampAsync(ChatJid groupJid, CancellationToken cancellationToken)
    {
        RouterStateEntry? state = await routerStateRepository.GetAsync(GetLastAgentTimestampKey(groupJid), cancellationToken);
        return state is not null && DateTimeOffset.TryParse(state.Value, out DateTimeOffset parsed)
            ? parsed
            : null;
    }

    private static string GetLastAgentTimestampKey(ChatJid groupJid)
    {
        return $"last_agent_timestamp:{groupJid.Value}";
    }

    private static bool ContainsTrigger(string content, string trigger)
    {
        return content.Contains(trigger, StringComparison.OrdinalIgnoreCase);
    }

    private static async Task SetTypingAsync(IChannel? channel, ChatJid groupJid, bool isTyping, CancellationToken cancellationToken)
    {
        if (channel is null)
        {
            return;
        }

        try
        {
            await channel.SetTypingAsync(groupJid, isTyping, cancellationToken);
        }
        catch
        {
        }
    }

    private static SessionScope ResolveSessionScope(IChannel? channel)
    {
        string channelName = channel?.Name.Value ?? string.Empty;

        return channelName switch
        {
            "terminal" => SessionScope.Private,
            "reference-file" => SessionScope.Private,
            "slack" => SessionScope.Group,
            _ => SessionScope.Group
        };
    }

    private async Task<IReadOnlyList<StoredMessage>> EnrichWithAttachmentsAsync(IReadOnlyList<StoredMessage> messages, ChatJid chatJid, CancellationToken cancellationToken)
    {
        List<string> messageIds = messages.Select(m => m.Id).ToList();
        IReadOnlyDictionary<string, IReadOnlyList<FileAttachment>> attachmentsByMessage =
            await fileAttachmentRepository.GetByMessagesAsync(messageIds, chatJid, cancellationToken);

        if (attachmentsByMessage.Count == 0)
        {
            return messages;
        }

        List<StoredMessage> enriched = [];
        foreach (StoredMessage message in messages)
        {
            if (attachmentsByMessage.TryGetValue(message.Id, out IReadOnlyList<FileAttachment>? attachments) && attachments.Count > 0)
            {
                enriched.Add(new StoredMessage(
                    message.Id,
                    message.ChatJid,
                    message.Sender,
                    message.SenderName,
                    message.Content,
                    message.Timestamp,
                    message.IsFromMe,
                    message.IsBotMessage,
                    attachments));
            }
            else
            {
                enriched.Add(message);
            }
        }

        return enriched;
    }

    private void StageAttachmentFiles(IReadOnlyList<StoredMessage> messages, GroupFolder groupFolder)
    {
        string uploadsDir = Path.Combine(groupsDirectory, groupFolder.Value, ".uploads");
        bool directoryCreated = false;

        foreach (StoredMessage message in messages)
        {
            foreach (FileAttachment attachment in message.Attachments)
            {
                if (!File.Exists(attachment.LocalPath))
                {
                    continue;
                }

                if (!directoryCreated)
                {
                    Directory.CreateDirectory(uploadsDir);
                    directoryCreated = true;
                }

                string safeName = Path.GetFileName(attachment.FileName);
                if (string.IsNullOrWhiteSpace(safeName))
                {
                    safeName = attachment.FileId;
                }

                string destination = Path.GetFullPath(Path.Combine(uploadsDir, safeName));
                if (!destination.StartsWith(uploadsDir, StringComparison.Ordinal))
                {
                    continue;
                }

                File.Copy(attachment.LocalPath, destination, overwrite: true);
            }
        }
    }

    private async Task RouteFileReferencesAsync(string rawOutput, GroupFolder groupFolder, ChatJid groupJid, CancellationToken cancellationToken)
    {
        IReadOnlyList<OutboundFileReference> fileRefs = messageFormatter.ExtractFileReferences(rawOutput);
        if (fileRefs.Count == 0)
        {
            return;
        }

        logger.LogInformation("Found {Count} outbound file reference(s) for {GroupJid}.", fileRefs.Count, groupJid.Value);
        string workspaceRoot = Path.GetFullPath(Path.Combine(groupsDirectory, groupFolder.Value));

        foreach (OutboundFileReference fileRef in fileRefs)
        {
            string resolvedPath = Path.GetFullPath(Path.Combine(workspaceRoot, fileRef.RelativePath));
            if (!resolvedPath.StartsWith(workspaceRoot, StringComparison.Ordinal))
            {
                logger.LogWarning("Outbound file reference '{Path}' escapes workspace root. Skipping.", fileRef.RelativePath);
                continue;
            }

            if (!File.Exists(resolvedPath))
            {
                logger.LogWarning("Outbound file reference '{Path}' not found at '{ResolvedPath}'. Skipping.", fileRef.RelativePath, resolvedPath);
                continue;
            }

            string fileName = Path.GetFileName(resolvedPath);
            logger.LogInformation("Routing outbound file '{FileName}' from '{ResolvedPath}' for {GroupJid}.", fileName, resolvedPath, groupJid.Value);
            await outboundRouter.RouteFileAsync(channels, groupJid, resolvedPath, fileName, cancellationToken);
        }
    }
}
