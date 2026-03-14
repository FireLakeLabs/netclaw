using NetClaw.Domain.Contracts.Services;
using NetClaw.Domain.ValueObjects;

namespace NetClaw.Application.Execution;

public sealed class GroupExecutionQueue : IGroupExecutionQueue
{
    private readonly TimeSpan baseRetryDelay;
    private readonly Dictionary<string, GroupExecutionState> groups = new(StringComparer.Ordinal);
    private readonly Queue<ChatJid> waitingGroups = new();
    private readonly object gate = new();
    private readonly int maxConcurrentExecutions;
    private int activeExecutions;

    private Func<ChatJid, CancellationToken, Task<bool>>? messageProcessor;
    private Func<ChatJid, string, bool>? inputWriter;
    private Action<ChatJid>? inputCloser;

    public GroupExecutionQueue(int maxConcurrentExecutions, TimeSpan? baseRetryDelay = null)
    {
        if (maxConcurrentExecutions <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxConcurrentExecutions), "Max concurrency must be positive.");
        }

        this.maxConcurrentExecutions = maxConcurrentExecutions;
        this.baseRetryDelay = baseRetryDelay ?? TimeSpan.FromSeconds(5);
    }

    public void SetMessageProcessor(Func<ChatJid, CancellationToken, Task<bool>> processor)
    {
        messageProcessor = processor;
    }

    public void SetInputHandlers(Func<ChatJid, string, bool> writer, Action<ChatJid> closer)
    {
        inputWriter = writer;
        inputCloser = closer;
    }

    public void EnqueueMessageCheck(ChatJid groupJid)
    {
        lock (gate)
        {
            GroupExecutionState state = GetState(groupJid);

            if (state.Active)
            {
                state.PendingMessages = true;
                return;
            }

            if (activeExecutions >= maxConcurrentExecutions)
            {
                state.PendingMessages = true;
                EnqueueWaitingGroup(groupJid);
                return;
            }

            state.PendingMessages = false;
            StartMessageRun(groupJid, state);
        }
    }

    public void EnqueueTask(ChatJid groupJid, TaskId taskId, Func<CancellationToken, Task> workItem)
    {
        lock (gate)
        {
            GroupExecutionState state = GetState(groupJid);

            if (state.RunningTaskIds.Contains(taskId.Value) || state.PendingTasks.Any(task => task.Id == taskId.Value))
            {
                return;
            }

            if (state.Active)
            {
                state.PendingTasks.Enqueue(new QueuedTask(taskId.Value, workItem));
                if (!state.IsTaskExecution)
                {
                    inputCloser?.Invoke(groupJid);
                }
                return;
            }

            if (activeExecutions >= maxConcurrentExecutions)
            {
                state.PendingTasks.Enqueue(new QueuedTask(taskId.Value, workItem));
                EnqueueWaitingGroup(groupJid);
                return;
            }

            StartTaskRun(groupJid, state, new QueuedTask(taskId.Value, workItem));
        }
    }

    public bool SendMessage(ChatJid groupJid, string text)
    {
        lock (gate)
        {
            GroupExecutionState state = GetState(groupJid);
            if (!state.Active || state.IsTaskExecution || state.PendingTasks.Count > 0 || inputWriter is null)
            {
                return false;
            }

            state.IdleWaiting = false;
            return inputWriter(groupJid, text);
        }
    }

    public void CloseInput(ChatJid groupJid)
    {
        lock (gate)
        {
            GroupExecutionState state = GetState(groupJid);
            if (!state.Active)
            {
                return;
            }

            inputCloser?.Invoke(groupJid);
        }
    }

    public void NotifyIdle(ChatJid groupJid)
    {
        lock (gate)
        {
            GroupExecutionState state = GetState(groupJid);
            state.IdleWaiting = true;
            if (state.PendingTasks.Count > 0)
            {
                inputCloser?.Invoke(groupJid);
            }
        }
    }

    public QueueSnapshot GetSnapshot()
    {
        lock (gate)
        {
            List<GroupStateSnapshot> groupSnapshots = [];
            foreach (KeyValuePair<string, GroupExecutionState> pair in groups)
            {
                groupSnapshots.Add(new GroupStateSnapshot(
                    pair.Key,
                    pair.Value.Active,
                    pair.Value.IsTaskExecution,
                    pair.Value.PendingMessages,
                    pair.Value.PendingTasks.Count,
                    pair.Value.IdleWaiting,
                    pair.Value.RetryCount,
                    pair.Value.RunningTaskIds.ToArray()));
            }

            return new QueueSnapshot(activeExecutions, maxConcurrentExecutions, waitingGroups.Count, groupSnapshots);
        }
    }

    private GroupExecutionState GetState(ChatJid groupJid)
    {
        if (!groups.TryGetValue(groupJid.Value, out GroupExecutionState? state))
        {
            state = new GroupExecutionState();
            groups[groupJid.Value] = state;
        }

        return state;
    }

    private void EnqueueWaitingGroup(ChatJid groupJid)
    {
        if (waitingGroups.All(current => current.Value != groupJid.Value))
        {
            waitingGroups.Enqueue(groupJid);
        }
    }

    private void StartMessageRun(ChatJid groupJid, GroupExecutionState state)
    {
        state.Active = true;
        state.IdleWaiting = false;
        state.IsTaskExecution = false;
        activeExecutions++;

        _ = Task.Run(async () =>
        {
            bool succeeded = true;
            try
            {
                if (messageProcessor is not null)
                {
                    succeeded = await messageProcessor(groupJid, CancellationToken.None);
                }
            }
            finally
            {
                await CompleteMessageRunAsync(groupJid, succeeded);
            }
        });
    }

    private void StartTaskRun(ChatJid groupJid, GroupExecutionState state, QueuedTask task)
    {
        state.Active = true;
        state.IdleWaiting = false;
        state.IsTaskExecution = true;
        state.RunningTaskIds.Add(task.Id);
        activeExecutions++;

        _ = Task.Run(async () =>
        {
            try
            {
                await task.WorkItem(CancellationToken.None);
            }
            finally
            {
                CompleteTaskRun(groupJid, task.Id);
            }
        });
    }

    private async Task CompleteMessageRunAsync(ChatJid groupJid, bool succeeded)
    {
        bool shouldRetry;

        lock (gate)
        {
            GroupExecutionState state = GetState(groupJid);
            state.Active = false;
            activeExecutions--;

            if (succeeded)
            {
                state.RetryCount = 0;
                shouldRetry = false;
            }
            else
            {
                state.RetryCount++;
                shouldRetry = state.RetryCount <= 5;
            }

            DrainGroup(groupJid, state);
        }

        if (shouldRetry)
        {
            int retryExponent = groups[groupJid.Value].RetryCount - 1;
            TimeSpan retryDelay = TimeSpan.FromMilliseconds(baseRetryDelay.TotalMilliseconds * Math.Pow(2, retryExponent));
            await Task.Delay(retryDelay);
            EnqueueMessageCheck(groupJid);
        }
    }

    private void CompleteTaskRun(ChatJid groupJid, string taskId)
    {
        lock (gate)
        {
            GroupExecutionState state = GetState(groupJid);
            state.Active = false;
            state.IsTaskExecution = false;
            state.RunningTaskIds.Remove(taskId);
            activeExecutions--;
            DrainGroup(groupJid, state);
        }
    }

    private void DrainGroup(ChatJid groupJid, GroupExecutionState state)
    {
        if (state.PendingTasks.Count > 0)
        {
            QueuedTask task = state.PendingTasks.Dequeue();
            StartTaskRun(groupJid, state, task);
            return;
        }

        if (state.PendingMessages)
        {
            state.PendingMessages = false;
            StartMessageRun(groupJid, state);
            return;
        }

        while (waitingGroups.Count > 0 && activeExecutions < maxConcurrentExecutions)
        {
            ChatJid waitingGroup = waitingGroups.Dequeue();
            GroupExecutionState waitingState = GetState(waitingGroup);
            if (waitingState.Active)
            {
                continue;
            }

            if (waitingState.PendingTasks.Count > 0)
            {
                StartTaskRun(waitingGroup, waitingState, waitingState.PendingTasks.Dequeue());
                break;
            }

            if (waitingState.PendingMessages)
            {
                waitingState.PendingMessages = false;
                StartMessageRun(waitingGroup, waitingState);
                break;
            }
        }
    }

    private sealed class GroupExecutionState
    {
        public bool Active { get; set; }

        public bool IdleWaiting { get; set; }

        public bool IsTaskExecution { get; set; }

        public bool PendingMessages { get; set; }

        public Queue<QueuedTask> PendingTasks { get; } = new();

        public HashSet<string> RunningTaskIds { get; } = new(StringComparer.Ordinal);

        public int RetryCount { get; set; }
    }

    private sealed record QueuedTask(string Id, Func<CancellationToken, Task> WorkItem);
}

public sealed record QueueSnapshot(
    int ActiveExecutions,
    int MaxConcurrentExecutions,
    int WaitingGroupCount,
    IReadOnlyList<GroupStateSnapshot> Groups);

public sealed record GroupStateSnapshot(
    string ChatJid,
    bool Active,
    bool IsTaskExecution,
    bool PendingMessages,
    int PendingTaskCount,
    bool IdleWaiting,
    int RetryCount,
    IReadOnlyList<string> RunningTaskIds);
