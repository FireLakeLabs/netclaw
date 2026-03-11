using NetClaw.Application.Execution;
using NetClaw.Domain.ValueObjects;

namespace NetClaw.Application.Tests.Execution;

public sealed class GroupExecutionQueueTests
{
    [Fact]
    public async Task EnqueueMessageCheck_InvokesMessageProcessor()
    {
        TaskCompletionSource<bool> completionSource = new(TaskCreationOptions.RunContinuationsAsynchronously);
        GroupExecutionQueue queue = new(1, TimeSpan.FromMilliseconds(5));
        queue.SetMessageProcessor((groupJid, _) =>
        {
            completionSource.TrySetResult(groupJid.Value == "group@jid");
            return Task.FromResult(true);
        });

        queue.EnqueueMessageCheck(new ChatJid("group@jid"));

        Assert.True(await completionSource.Task.WaitAsync(TimeSpan.FromSeconds(1)));
    }

    [Fact]
    public async Task EnqueueTask_DeduplicatesByTaskId()
    {
        int runCount = 0;
        TaskCompletionSource<bool> completionSource = new(TaskCreationOptions.RunContinuationsAsynchronously);
        GroupExecutionQueue queue = new(1);

        queue.EnqueueTask(new ChatJid("group@jid"), new TaskId("task-1"), _ =>
        {
            Interlocked.Increment(ref runCount);
            completionSource.TrySetResult(true);
            return Task.CompletedTask;
        });
        queue.EnqueueTask(new ChatJid("group@jid"), new TaskId("task-1"), _ => Task.CompletedTask);

        await completionSource.Task.WaitAsync(TimeSpan.FromSeconds(1));
        await Task.Delay(50);

        Assert.Equal(1, runCount);
    }

    [Fact]
    public async Task SendMessage_ReturnsTrueOnlyWhenActiveNonTaskExecutionExists()
    {
        TaskCompletionSource<bool> processorStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource<bool> releaseProcessor = new(TaskCreationOptions.RunContinuationsAsynchronously);
        List<string> sentMessages = [];

        GroupExecutionQueue queue = new(1);
        queue.SetInputHandlers((_, text) =>
        {
            sentMessages.Add(text);
            return true;
        }, _ => { });
        queue.SetMessageProcessor(async (_, _) =>
        {
            processorStarted.TrySetResult(true);
            await releaseProcessor.Task;
            return true;
        });

        queue.EnqueueMessageCheck(new ChatJid("group@jid"));
        await processorStarted.Task.WaitAsync(TimeSpan.FromSeconds(1));

        bool sent = queue.SendMessage(new ChatJid("group@jid"), "follow-up");
        releaseProcessor.TrySetResult(true);

        Assert.True(sent);
        Assert.Single(sentMessages);
    }

    [Fact]
    public async Task EnqueueTask_RequestsCloseImmediatelyWhenTaskQueuedDuringActiveMessageRun()
    {
        TaskCompletionSource<bool> processorStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource<bool> releaseProcessor = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource<bool> closeRequested = new(TaskCreationOptions.RunContinuationsAsynchronously);

        GroupExecutionQueue queue = new(1);
        queue.SetInputHandlers((_, _) => true, _ => closeRequested.TrySetResult(true));
        queue.SetMessageProcessor(async (_, _) =>
        {
            processorStarted.TrySetResult(true);
            await releaseProcessor.Task;
            return true;
        });

        queue.EnqueueMessageCheck(new ChatJid("group@jid"));
        await processorStarted.Task.WaitAsync(TimeSpan.FromSeconds(1));

        queue.EnqueueTask(new ChatJid("group@jid"), new TaskId("task-1"), _ => Task.CompletedTask);

        Assert.True(await closeRequested.Task.WaitAsync(TimeSpan.FromSeconds(1)));
        releaseProcessor.TrySetResult(true);
    }

    [Fact]
    public async Task SendMessage_ReturnsFalseWhenTaskIsPendingForActiveSession()
    {
        TaskCompletionSource<bool> processorStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource<bool> releaseProcessor = new(TaskCreationOptions.RunContinuationsAsynchronously);

        GroupExecutionQueue queue = new(1);
        queue.SetInputHandlers((_, _) => true, _ => { });
        queue.SetMessageProcessor(async (_, _) =>
        {
            processorStarted.TrySetResult(true);
            await releaseProcessor.Task;
            return true;
        });

        queue.EnqueueMessageCheck(new ChatJid("group@jid"));
        await processorStarted.Task.WaitAsync(TimeSpan.FromSeconds(1));
        queue.EnqueueTask(new ChatJid("group@jid"), new TaskId("task-1"), _ => Task.CompletedTask);

        bool sent = queue.SendMessage(new ChatJid("group@jid"), "follow-up");
        releaseProcessor.TrySetResult(true);

        Assert.False(sent);
    }
}
