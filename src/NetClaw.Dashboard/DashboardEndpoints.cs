using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using NetClaw.Application.Execution;
using NetClaw.Dashboard.Models;
using NetClaw.Dashboard.Services;
using NetClaw.Domain.Contracts.Channels;
using NetClaw.Domain.Contracts.Persistence;
using NetClaw.Domain.Entities;
using NetClaw.Domain.ValueObjects;

namespace NetClaw.Dashboard;

public static class DashboardEndpoints
{
    public static void MapDashboardApi(this IEndpointRouteBuilder routes)
    {
        RouteGroupBuilder api = routes.MapGroup("/api");

        MapActivityEndpoints(api);
        MapMessageEndpoints(api);
        MapTaskEndpoints(api);
        MapGroupEndpoints(api);
        MapSystemEndpoints(api);
        MapWorkspaceEndpoints(api);
    }

    private static void MapActivityEndpoints(RouteGroupBuilder api)
    {
        RouteGroupBuilder activity = api.MapGroup("/activity");

        activity.MapGet("/recent", async (
            IAgentEventRepository repo,
            int? limit,
            DateTimeOffset? since,
            string? group,
            CancellationToken ct) =>
        {
            int clampedLimit = Math.Clamp(limit ?? 100, 1, 500);
            IReadOnlyList<AgentActivityEvent> events = await repo.GetRecentAsync(
                clampedLimit, since, group, ct);

            return Results.Ok(events.Select(MapEvent).ToArray());
        });

        activity.MapGet("/session/{sessionId}", async (
            IAgentEventRepository repo,
            string sessionId,
            CancellationToken ct) =>
        {
            IReadOnlyList<AgentActivityEvent> events = await repo.GetBySessionAsync(sessionId, ct);
            return Results.Ok(events.Select(MapEvent).ToArray());
        });

        activity.MapGet("/live-state", (GroupExecutionQueue queue) =>
        {
            QueueSnapshot snapshot = queue.GetSnapshot();
            return Results.Ok(MapQueueState(snapshot));
        });
    }

    private static void MapMessageEndpoints(RouteGroupBuilder api)
    {
        RouteGroupBuilder messages = api.MapGroup("/messages");

        messages.MapGet("/chats", async (IMessageRepository repo, CancellationToken ct) =>
        {
            IReadOnlyList<ChatInfo> chats = await repo.GetAllChatsAsync(ct);
            return Results.Ok(chats.Select(c => new ChatSummaryDto(
                c.Jid.Value, c.Name, c.LastMessageTime, c.Channel.Value, c.IsGroup)).ToArray());
        });

        messages.MapGet("/chats/{jid}", async (
            IMessageRepository repo,
            string jid,
            DateTimeOffset? since,
            int? limit,
            string? assistantName,
            CancellationToken ct) =>
        {
            int clampedLimit = Math.Clamp(limit ?? 200, 1, 500);
            IReadOnlyList<StoredMessage> msgs = await repo.GetMessagesSinceAsync(
                new ChatJid(jid), since, assistantName ?? "__none__", ct);

            IEnumerable<StoredMessage> result = msgs.TakeLast(clampedLimit);
            return Results.Ok(result.Select(m => new MessageDto(
                m.Id, m.ChatJid.Value, m.Sender, m.SenderName, m.Content,
                m.Timestamp, m.IsFromMe, m.IsBotMessage)).ToArray());
        });
    }

    private static void MapTaskEndpoints(RouteGroupBuilder api)
    {
        RouteGroupBuilder tasks = api.MapGroup("/tasks");

        tasks.MapGet("/", async (ITaskRepository repo, CancellationToken ct) =>
        {
            IReadOnlyList<ScheduledTask> all = await repo.GetAllAsync(ct);
            return Results.Ok(all.Select(MapTask).ToArray());
        });

        tasks.MapGet("/{id}", async (ITaskRepository repo, string id, CancellationToken ct) =>
        {
            ScheduledTask? task = await repo.GetByIdAsync(new TaskId(id), ct);
            return task is null ? Results.NotFound() : Results.Ok(MapTask(task));
        });

        tasks.MapGet("/{id}/runs", async (
            ITaskRepository repo,
            string id,
            int? limit,
            CancellationToken ct) =>
        {
            int clampedLimit = Math.Clamp(limit ?? 50, 1, 500);
            IReadOnlyList<TaskRunLog> logs = await repo.GetRunLogsAsync(new TaskId(id), clampedLimit, ct);
            return Results.Ok(logs.Select(l => new TaskRunDto(
                l.TaskId.Value, l.RunAt, (long)l.Duration.TotalMilliseconds,
                l.Status.ToString(), l.Result, l.Error)).ToArray());
        });
    }

    private static void MapGroupEndpoints(RouteGroupBuilder api)
    {
        RouteGroupBuilder groups = api.MapGroup("/groups");

        groups.MapGet("/", async (
            IGroupRepository groupRepo,
            ISessionRepository sessionRepo,
            CancellationToken ct) =>
        {
            IReadOnlyDictionary<ChatJid, RegisteredGroup> all = await groupRepo.GetAllAsync(ct);
            IReadOnlyDictionary<GroupFolder, SessionId> sessions = await sessionRepo.GetAllAsync(ct);

            return Results.Ok(all.Select(pair =>
            {
                SessionId? sessionId = sessions.TryGetValue(pair.Value.Folder, out SessionId sid) ? sid : null;
                return MapGroup(pair.Key, pair.Value, sessionId);
            }).ToArray());
        });

        groups.MapGet("/{jid}", async (
            IGroupRepository groupRepo,
            ISessionRepository sessionRepo,
            string jid,
            CancellationToken ct) =>
        {
            RegisteredGroup? group = await groupRepo.GetByJidAsync(new ChatJid(jid), ct);
            if (group is null)
            {
                return Results.NotFound();
            }

            SessionId? sessionId = await sessionRepo.GetByGroupFolderAsync(group.Folder, ct);
            return Results.Ok(MapGroup(new ChatJid(jid), group, sessionId));
        });
    }

    private static void MapSystemEndpoints(RouteGroupBuilder api)
    {
        RouteGroupBuilder system = api.MapGroup("/system");

        system.MapGet("/health", (
            GroupExecutionQueue queue,
            IReadOnlyList<IChannel> channels,
            DashboardStateService stateService) =>
        {
            QueueSnapshot snapshot = queue.GetSnapshot();
            IReadOnlyList<ChannelStatusDto> channelStatuses = channels
                .Select(c => new ChannelStatusDto(c.GetType().Name.Replace("Channel", ""), c.IsConnected))
                .ToArray();

            return Results.Ok(new SystemHealthDto(
                DateTimeOffset.UtcNow,
                (long)stateService.Uptime.TotalSeconds,
                channelStatuses,
                MapQueueState(snapshot)));
        });

        system.MapGet("/router-state", async (IRouterStateRepository repo, CancellationToken ct) =>
        {
            IReadOnlyList<RouterStateEntry> entries = await repo.GetAllAsync(ct);
            return Results.Ok(entries.Select(e => new RouterStateDto(e.Key, e.Value)).ToArray());
        });
    }

    private static void MapWorkspaceEndpoints(RouteGroupBuilder api)
    {
        RouteGroupBuilder workspace = api.MapGroup("/workspace");

        workspace.MapGet("/{groupFolder}/tree", (WorkspaceFileService svc, string groupFolder) =>
        {
            try
            {
                IReadOnlyList<WorkspaceTreeEntryDto> tree = svc.GetTree(new GroupFolder(groupFolder));
                return Results.Ok(tree);
            }
            catch (InvalidOperationException)
            {
                return Results.NotFound();
            }
            catch (ArgumentException)
            {
                return Results.BadRequest("Invalid group folder.");
            }
        });

        workspace.MapGet("/{groupFolder}/file", (WorkspaceFileService svc, string groupFolder, string path) =>
        {
            try
            {
                WorkspaceFileDto? file = svc.ReadFile(new GroupFolder(groupFolder), path);
                return file is null ? Results.NotFound() : Results.Ok(file);
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("not allowed", StringComparison.OrdinalIgnoreCase))
            {
                return Results.BadRequest("Path is not allowed.");
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("size", StringComparison.OrdinalIgnoreCase))
            {
                return Results.Json(new { error = ex.Message }, statusCode: 413);
            }
            catch (InvalidOperationException)
            {
                return Results.BadRequest("Invalid request.");
            }
            catch (ArgumentException)
            {
                return Results.BadRequest("Invalid group folder.");
            }
        });
    }

    private static AgentActivityEventDto MapEvent(AgentActivityEvent e)
    {
        return new AgentActivityEventDto(
            e.Id, e.GroupFolder, e.ChatJid, e.SessionId,
            e.EventKind.ToString(), e.Content, e.ToolName, e.Error,
            e.IsScheduledTask, e.TaskId, e.ObservedAt, e.CapturedAt);
    }

    private static TaskDto MapTask(ScheduledTask t)
    {
        return new TaskDto(
            t.Id.Value, t.GroupFolder.Value, t.ChatJid.Value, t.Prompt,
            t.ScheduleType.ToString(), t.ScheduleValue, t.ContextMode.ToString(),
            t.NextRun, t.LastRun, t.LastResult, t.Status.ToString(), t.CreatedAt);
    }

    private static GroupDto MapGroup(ChatJid jid, RegisteredGroup g, SessionId? sessionId)
    {
        return new GroupDto(
            jid.Value, g.Name, g.Folder.Value, g.Trigger, g.RequiresTrigger,
            g.IsMain, g.AddedAt, sessionId?.Value);
    }

    private static QueueStateDto MapQueueState(QueueSnapshot snapshot)
    {
        return new QueueStateDto(
            snapshot.ActiveExecutions,
            snapshot.MaxConcurrentExecutions,
            snapshot.WaitingGroupCount,
            snapshot.Groups.Select(g => new GroupQueueStateDto(
                g.ChatJid, g.Active, g.IsTaskExecution, g.PendingMessages,
                g.PendingTaskCount, g.IdleWaiting, g.RetryCount, g.RunningTaskIds)).ToArray());
    }
}
