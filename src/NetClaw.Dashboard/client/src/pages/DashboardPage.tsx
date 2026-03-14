import { formatDistanceToNow } from "date-fns";
import { Activity, Clock, Users, Wifi } from "lucide-react";
import { Card, Badge, Spinner, EmptyState, PageHeader } from "@/components/ui/shared";
import { useSystemHealth, useRecentActivity, useTasks, useGroups } from "@/api/client";
import type { AgentActivityEventDto, QueueStateDto, WorkerHeartbeatDto } from "@/api/types";
import { eventKindVariant, eventKindLabel } from "@/pages/ActivityPage";

function formatUptime(seconds: number): string {
  const d = Math.floor(seconds / 86400);
  const h = Math.floor((seconds % 86400) / 3600);
  const m = Math.floor((seconds % 3600) / 60);
  if (d > 0) return `${d}d ${h}h ${m}m`;
  if (h > 0) return `${h}h ${m}m`;
  return `${m}m`;
}

interface DashboardPageProps {
  recentEvents: AgentActivityEventDto[];
  queueState: QueueStateDto | null;
  heartbeat: WorkerHeartbeatDto | null;
}

export function DashboardPage({ recentEvents, queueState, heartbeat }: DashboardPageProps) {
  const health = useSystemHealth();
  const activity = useRecentActivity(20);
  const tasks = useTasks();
  const groups = useGroups();

  const liveQueue = queueState ?? health.data?.queueState;
  const channels = heartbeat?.channels ?? health.data?.channels ?? [];

  const uptime = health.data?.uptimeSeconds;
  const activeCount = liveQueue?.activeExecutions ?? 0;
  const overallStatus = activeCount > 0 ? "active" : "idle";

  return (
    <div>
      <PageHeader title="Dashboard" />

      {/* Status header */}
      <div className="grid grid-cols-4 gap-4 mb-6">
        <Card>
          <div className="flex items-center gap-3">
            <div className={`w-3 h-3 rounded-full ${overallStatus === "active" ? "bg-emerald-500 animate-pulse" : "bg-gray-500"}`} />
            <div>
              <p className="text-xs text-gray-400">Status</p>
              <p className="text-sm font-medium capitalize">{overallStatus}</p>
            </div>
          </div>
        </Card>
        <Card>
          <div className="flex items-center gap-3">
            <Activity size={18} className="text-blue-400" />
            <div>
              <p className="text-xs text-gray-400">Active Sessions</p>
              <p className="text-sm font-medium">{activeCount} / {liveQueue?.maxConcurrentExecutions ?? "?"}</p>
            </div>
          </div>
        </Card>
        <Card>
          <div className="flex items-center gap-3">
            <Users size={18} className="text-purple-400" />
            <div>
              <p className="text-xs text-gray-400">Groups</p>
              <p className="text-sm font-medium">{groups.data?.length ?? "—"}</p>
            </div>
          </div>
        </Card>
        <Card>
          <div className="flex items-center gap-3">
            <Clock size={18} className="text-amber-400" />
            <div>
              <p className="text-xs text-gray-400">Uptime</p>
              <p className="text-sm font-medium">{uptime != null ? formatUptime(uptime) : "—"}</p>
            </div>
          </div>
        </Card>
      </div>

      <div className="grid grid-cols-3 gap-4">
        {/* Active sessions */}
        <Card title="Active Groups" className="col-span-2">
          {liveQueue && liveQueue.groups.filter((g) => g.active).length > 0 ? (
            <div className="space-y-2">
              {liveQueue.groups
                .filter((g) => g.active)
                .map((g) => (
                  <div key={g.chatJid} className="flex items-center justify-between p-2 bg-gray-800 rounded">
                    <span className="text-sm">{g.chatJid}</span>
                    <div className="flex gap-2">
                      {g.isTaskExecution && <Badge variant="info">Task</Badge>}
                      <Badge variant="success">Active</Badge>
                    </div>
                  </div>
                ))}
            </div>
          ) : (
            <EmptyState message="No active executions" />
          )}
        </Card>

        {/* Channel health */}
        <Card title="Channels">
          {channels.length > 0 ? (
            <div className="space-y-2">
              {channels.map((ch) => (
                <div key={ch.name} className="flex items-center justify-between">
                  <div className="flex items-center gap-2">
                    <Wifi size={14} className={ch.isConnected ? "text-emerald-400" : "text-red-400"} />
                    <span className="text-sm">{ch.name}</span>
                  </div>
                  <Badge variant={ch.isConnected ? "success" : "error"}>
                    {ch.isConnected ? "Connected" : "Disconnected"}
                  </Badge>
                </div>
              ))}
            </div>
          ) : (
            <EmptyState message="No channels configured" />
          )}
        </Card>

        {/* Recent activity ticker */}
        <Card title="Recent Activity" className="col-span-2">
          {(recentEvents.length > 0 || (activity.data && activity.data.length > 0)) ? (
            <div className="space-y-1 max-h-64 overflow-y-auto">
              {(recentEvents.length > 0 ? recentEvents : activity.data ?? []).slice(0, 20).map((event) => (
                <div key={event.id} className="flex items-center gap-2 py-1 text-xs">
                  <span className="text-gray-500 w-16 shrink-0">
                    {formatDistanceToNow(new Date(event.observedAt), { addSuffix: true })}
                  </span>
                  <Badge variant={eventKindVariant(event.eventKind)} className="w-24 justify-center">
                    {eventKindLabel(event.eventKind)}
                  </Badge>
                  <span className="text-gray-400 truncate">{event.groupFolder}</span>
                  {event.content && (
                    <span className="text-gray-500 truncate flex-1">{event.content.slice(0, 60)}</span>
                  )}
                </div>
              ))}
            </div>
          ) : activity.isLoading ? (
            <Spinner />
          ) : (
            <EmptyState message="No recent activity" />
          )}
        </Card>

        {/* Upcoming tasks */}
        <Card title="Upcoming Tasks">
          {tasks.data && tasks.data.filter((t) => t.nextRun && t.status === "Active").length > 0 ? (
            <div className="space-y-2">
              {tasks.data
                .filter((t) => t.nextRun && t.status === "Active")
                .sort((a, b) => new Date(a.nextRun!).getTime() - new Date(b.nextRun!).getTime())
                .slice(0, 5)
                .map((task) => (
                  <div key={task.id} className="flex items-center justify-between text-sm">
                    <span className="truncate flex-1">{task.prompt.slice(0, 40)}</span>
                    <span className="text-xs text-gray-400">
                      {formatDistanceToNow(new Date(task.nextRun!), { addSuffix: true })}
                    </span>
                  </div>
                ))}
            </div>
          ) : tasks.isLoading ? (
            <Spinner />
          ) : (
            <EmptyState message="No upcoming tasks" />
          )}
        </Card>
      </div>
    </div>
  );
}
