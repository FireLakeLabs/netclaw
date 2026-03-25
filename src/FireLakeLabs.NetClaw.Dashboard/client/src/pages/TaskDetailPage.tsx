import { useParams, Link } from "react-router-dom";
import { format } from "date-fns";
import { ArrowLeft } from "lucide-react";
import { Card, Badge, Spinner, EmptyState, PageHeader } from "@/components/ui/shared";
import { useTask, useTaskRuns } from "@/api/client";

export function TaskDetailPage() {
  const { id } = useParams<{ id: string }>();
  const { data: task, isLoading: taskLoading } = useTask(id ?? "");
  const { data: runs, isLoading: runsLoading } = useTaskRuns(id ?? "");

  if (taskLoading) return <Spinner />;
  if (!task) return <EmptyState message="Task not found" />;

  return (
    <div>
      <PageHeader title={`Task: ${task.id.slice(0, 12)}`}>
        <Link to="/tasks" className="flex items-center gap-1 text-sm text-gray-400 hover:text-white">
          <ArrowLeft size={16} />
          Back
        </Link>
      </PageHeader>

      <Card title="Configuration" className="mb-4">
        <dl className="grid grid-cols-2 gap-x-8 gap-y-2 text-sm">
          <dt className="text-gray-400">Group</dt>
          <dd>{task.groupFolder}</dd>
          <dt className="text-gray-400">Chat</dt>
          <dd>{task.chatJid}</dd>
          <dt className="text-gray-400">Schedule</dt>
          <dd>{task.scheduleValue}</dd>
          <dt className="text-gray-400">Status</dt>
          <dd><Badge variant={task.status === "Active" ? "success" : "warning"}>{task.status}</Badge></dd>
          <dt className="text-gray-400">Next Run</dt>
          <dd>{task.nextRun ? format(new Date(task.nextRun), "PPpp") : "—"}</dd>
          <dt className="text-gray-400">Created</dt>
          <dd>{format(new Date(task.createdAt), "PPpp")}</dd>
        </dl>
      </Card>

      <Card title="Prompt" className="mb-4">
        <pre className="text-sm text-gray-300 whitespace-pre-wrap">{task.prompt}</pre>
      </Card>

      <Card title="Run History">
        {runsLoading ? (
          <Spinner />
        ) : !runs || runs.length === 0 ? (
          <EmptyState message="No runs recorded" />
        ) : (
          <div className="space-y-2">
            {runs.map((run, i) => (
              <div key={i} className="p-3 bg-gray-800 rounded flex items-start justify-between">
                <div>
                  <div className="flex items-center gap-2 mb-1">
                    <Badge variant={run.status === "Success" ? "success" : run.status === "Error" ? "error" : "warning"}>
                      {run.status}
                    </Badge>
                    <span className="text-xs text-gray-400">
                      {format(new Date(run.runAt), "MMM d, HH:mm:ss")}
                    </span>
                  </div>
                  {run.result && (
                    <p className="text-sm text-gray-300 mt-1 whitespace-pre-wrap">{run.result.slice(0, 200)}</p>
                  )}
                  {run.error && (
                    <p className="text-sm text-red-300 mt-1">{run.error}</p>
                  )}
                </div>
              </div>
            ))}
          </div>
        )}
      </Card>
    </div>
  );
}
