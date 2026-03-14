import { useState, useCallback } from "react";
import { BrowserRouter, Routes, Route, Navigate } from "react-router-dom";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { Sidebar } from "@/components/layout/Sidebar";
import { useSignalR } from "@/hooks/useSignalR";
import type { AgentActivityEventDto, QueueStateDto, WorkerHeartbeatDto } from "@/api/types";
import { DashboardPage } from "@/pages/DashboardPage";
import { ActivityPage } from "@/pages/ActivityPage";
import { MessagesPage } from "@/pages/MessagesPage";
import { ChatDetailPage } from "@/pages/ChatDetailPage";
import { TasksPage } from "@/pages/TasksPage";
import { TaskDetailPage } from "@/pages/TaskDetailPage";
import { GroupsPage } from "@/pages/GroupsPage";
import { GroupDetailPage } from "@/pages/GroupDetailPage";
import { WorkspacePage } from "@/pages/WorkspacePage";
import { WorkspaceDetailPage } from "@/pages/WorkspaceDetailPage";
import { SystemPage } from "@/pages/SystemPage";

const queryClient = new QueryClient({
  defaultOptions: {
    queries: { retry: 1, staleTime: 5_000 },
  },
});

function AppShell() {
  const [recentEvents, setRecentEvents] = useState<AgentActivityEventDto[]>([]);
  const [queueState, setQueueState] = useState<QueueStateDto | null>(null);
  const [heartbeat, setHeartbeat] = useState<WorkerHeartbeatDto | null>(null);

  const onAgentEvent = useCallback((event: AgentActivityEventDto) => {
    setRecentEvents((prev) => [event, ...prev].slice(0, 200));
  }, []);

  const onQueueStateChanged = useCallback((state: QueueStateDto) => {
    setQueueState(state);
  }, []);

  const onWorkerHeartbeat = useCallback((hb: WorkerHeartbeatDto) => {
    setHeartbeat(hb);
  }, []);

  const { status } = useSignalR({
    onAgentEvent,
    onQueueStateChanged,
    onWorkerHeartbeat,
  });

  return (
    <div className="flex min-h-screen bg-gray-950 text-gray-100">
      <Sidebar connectionStatus={status} />
      <main className="flex-1 overflow-auto">
        <div className="p-6 max-w-7xl">
          <Routes>
            <Route
              path="/"
              element={
                <DashboardPage
                  recentEvents={recentEvents}
                  queueState={queueState}
                  heartbeat={heartbeat}
                />
              }
            />
            <Route path="/activity" element={<ActivityPage recentEvents={recentEvents} />} />
            <Route path="/messages" element={<MessagesPage />} />
            <Route path="/messages/:jid" element={<ChatDetailPage />} />
            <Route path="/tasks" element={<TasksPage />} />
            <Route path="/tasks/:id" element={<TaskDetailPage />} />
            <Route path="/groups" element={<GroupsPage />} />
            <Route path="/groups/:jid" element={<GroupDetailPage />} />
            <Route path="/workspace" element={<WorkspacePage />} />
            <Route path="/workspace/:folder" element={<WorkspaceDetailPage />} />
            <Route path="/system" element={<SystemPage />} />
            <Route path="*" element={<Navigate to="/" replace />} />
          </Routes>
        </div>
      </main>
    </div>
  );
}

export default function App() {
  return (
    <QueryClientProvider client={queryClient}>
      <BrowserRouter>
        <AppShell />
      </BrowserRouter>
    </QueryClientProvider>
  );
}
