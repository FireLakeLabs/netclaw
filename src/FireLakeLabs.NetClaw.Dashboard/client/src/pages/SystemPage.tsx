import { Card, Badge, Spinner, EmptyState, PageHeader } from "@/components/ui/shared";
import { useSystemHealth, useRouterState } from "@/api/client";

function formatUptime(seconds: number): string {
  const d = Math.floor(seconds / 86400);
  const h = Math.floor((seconds % 86400) / 3600);
  const m = Math.floor((seconds % 3600) / 60);
  if (d > 0) return `${d}d ${h}h ${m}m`;
  if (h > 0) return `${h}h ${m}m`;
  return `${m}m`;
}

export function SystemPage() {
  const health = useSystemHealth();
  const routerState = useRouterState();

  return (
    <div>
      <PageHeader title="System Health" />

      {health.isLoading ? (
        <Spinner />
      ) : !health.data ? (
        <EmptyState message="Could not load system health" />
      ) : (
        <div className="space-y-4">
          {/* Overview */}
          <Card title="Overview">
            <dl className="grid grid-cols-3 gap-4 text-sm">
              <div>
                <dt className="text-gray-400">Uptime</dt>
                <dd className="font-medium">{formatUptime(health.data.uptimeSeconds)}</dd>
              </div>
              <div>
                <dt className="text-gray-400">Active Executions</dt>
                <dd className="font-medium">
                  {health.data.queueState.activeExecutions} / {health.data.queueState.maxConcurrentExecutions}
                </dd>
              </div>
              <div>
                <dt className="text-gray-400">Waiting Groups</dt>
                <dd className="font-medium">{health.data.queueState.waitingGroupCount}</dd>
              </div>
            </dl>
          </Card>

          {/* Channels */}
          <Card title="Channels">
            {health.data.channels.length === 0 ? (
              <EmptyState message="No channels configured" />
            ) : (
              <div className="space-y-2">
                {health.data.channels.map((ch) => (
                  <div key={ch.name} className="flex items-center justify-between p-2 bg-gray-800 rounded">
                    <span className="text-sm">{ch.name}</span>
                    <Badge variant={ch.isConnected ? "success" : "error"}>
                      {ch.isConnected ? "Connected" : "Disconnected"}
                    </Badge>
                  </div>
                ))}
              </div>
            )}
          </Card>

          {/* Queue Groups */}
          <Card title="Queue State">
            {health.data.queueState.groups.length === 0 ? (
              <EmptyState message="No groups in queue" />
            ) : (
              <div className="overflow-x-auto">
                <table className="w-full text-sm">
                  <thead>
                    <tr className="border-b border-gray-800 text-left text-gray-400">
                      <th className="pb-2 pr-4">Chat JID</th>
                      <th className="pb-2 pr-4">Active</th>
                      <th className="pb-2 pr-4">Pending Msgs</th>
                      <th className="pb-2 pr-4">Pending Tasks</th>
                      <th className="pb-2 pr-4">Retries</th>
                    </tr>
                  </thead>
                  <tbody>
                    {health.data.queueState.groups.map((g) => (
                      <tr key={g.chatJid} className="border-b border-gray-800/50">
                        <td className="py-2 pr-4">{g.chatJid}</td>
                        <td className="py-2 pr-4">
                          <Badge variant={g.active ? "success" : "default"}>
                            {g.active ? "Yes" : "No"}
                          </Badge>
                        </td>
                        <td className="py-2 pr-4">{g.pendingMessages ? "Yes" : "No"}</td>
                        <td className="py-2 pr-4">{g.pendingTaskCount}</td>
                        <td className="py-2 pr-4">{g.retryCount}</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            )}
          </Card>

          {/* Router State */}
          <Card title="Router State">
            {routerState.isLoading ? (
              <Spinner />
            ) : !routerState.data || routerState.data.length === 0 ? (
              <EmptyState message="No router state entries" />
            ) : (
              <div className="overflow-x-auto">
                <table className="w-full text-sm">
                  <thead>
                    <tr className="border-b border-gray-800 text-left text-gray-400">
                      <th className="pb-2 pr-4">Key</th>
                      <th className="pb-2 pr-4">Value</th>
                    </tr>
                  </thead>
                  <tbody>
                    {routerState.data.map((entry) => (
                      <tr key={entry.key} className="border-b border-gray-800/50">
                        <td className="py-2 pr-4 font-mono text-xs">{entry.key}</td>
                        <td className="py-2 pr-4 text-gray-300 max-w-xs truncate">{entry.value}</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            )}
          </Card>
        </div>
      )}
    </div>
  );
}
