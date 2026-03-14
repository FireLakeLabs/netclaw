import { Link } from "react-router-dom";
import { formatDistanceToNow } from "date-fns";
import { Badge, Spinner, EmptyState, PageHeader } from "@/components/ui/shared";
import { useTasks } from "@/api/client";

function statusVariant(status: string) {
  switch (status) {
    case "Active": return "success" as const;
    case "Paused": return "warning" as const;
    case "Disabled": return "error" as const;
    default: return "default" as const;
  }
}

export function TasksPage() {
  const { data: tasks, isLoading } = useTasks();

  return (
    <div>
      <PageHeader title="Scheduled Tasks" />

      {isLoading ? (
        <Spinner />
      ) : !tasks || tasks.length === 0 ? (
        <EmptyState message="No scheduled tasks" />
      ) : (
        <div className="overflow-x-auto">
          <table className="w-full text-sm">
            <thead>
              <tr className="border-b border-gray-800 text-left text-gray-400">
                <th className="pb-2 pr-4">Group</th>
                <th className="pb-2 pr-4">Prompt</th>
                <th className="pb-2 pr-4">Schedule</th>
                <th className="pb-2 pr-4">Next Run</th>
                <th className="pb-2 pr-4">Status</th>
              </tr>
            </thead>
            <tbody>
              {tasks.map((task) => (
                <tr key={task.id} className="border-b border-gray-800/50 hover:bg-gray-900/50">
                  <td className="py-2 pr-4 text-gray-300">{task.groupFolder}</td>
                  <td className="py-2 pr-4">
                    <Link
                      to={`/tasks/${encodeURIComponent(task.id)}`}
                      className="text-blue-400 hover:text-blue-300 truncate block max-w-xs"
                    >
                      {task.prompt.slice(0, 60)}
                    </Link>
                  </td>
                  <td className="py-2 pr-4 text-gray-400">
                    {task.scheduleValue}
                  </td>
                  <td className="py-2 pr-4 text-gray-400">
                    {task.nextRun
                      ? formatDistanceToNow(new Date(task.nextRun), { addSuffix: true })
                      : "—"}
                  </td>
                  <td className="py-2 pr-4">
                    <Badge variant={statusVariant(task.status)}>{task.status}</Badge>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </div>
  );
}
