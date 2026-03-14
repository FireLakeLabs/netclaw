import { useQuery } from "@tanstack/react-query";
import type {
  AgentActivityEventDto,
  ChatSummaryDto,
  MessageDto,
  TaskDto,
  TaskRunDto,
  GroupDto,
  SystemHealthDto,
  RouterStateDto,
  QueueStateDto,
  WorkspaceTreeEntryDto,
  WorkspaceFileDto,
} from "./types";

async function fetchJson<T>(url: string): Promise<T> {
  const res = await fetch(url);
  if (!res.ok) throw new Error(`${res.status} ${res.statusText}`);
  return res.json() as Promise<T>;
}

export function useRecentActivity(limit = 100) {
  return useQuery({
    queryKey: ["activity", "recent", limit],
    queryFn: () =>
      fetchJson<AgentActivityEventDto[]>(
        `/api/activity/recent?limit=${limit}`
      ),
    refetchInterval: 10_000,
  });
}

export function useSessionActivity(sessionId: string) {
  return useQuery({
    queryKey: ["activity", "session", sessionId],
    queryFn: () =>
      fetchJson<AgentActivityEventDto[]>(
        `/api/activity/session/${encodeURIComponent(sessionId)}`
      ),
    enabled: !!sessionId,
  });
}

export function useLiveState() {
  return useQuery({
    queryKey: ["activity", "liveState"],
    queryFn: () => fetchJson<QueueStateDto>("/api/activity/live-state"),
    refetchInterval: 3_000,
  });
}

export function useChats() {
  return useQuery({
    queryKey: ["messages", "chats"],
    queryFn: () => fetchJson<ChatSummaryDto[]>("/api/messages/chats"),
    refetchInterval: 15_000,
  });
}

export function useChatMessages(jid: string, limit = 100) {
  return useQuery({
    queryKey: ["messages", "chat", jid, limit],
    queryFn: () =>
      fetchJson<MessageDto[]>(
        `/api/messages/chats/${encodeURIComponent(jid)}?limit=${limit}`
      ),
    enabled: !!jid,
  });
}

export function useTasks() {
  return useQuery({
    queryKey: ["tasks"],
    queryFn: () => fetchJson<TaskDto[]>("/api/tasks"),
    refetchInterval: 15_000,
  });
}

export function useTask(id: string) {
  return useQuery({
    queryKey: ["tasks", id],
    queryFn: () =>
      fetchJson<TaskDto>(`/api/tasks/${encodeURIComponent(id)}`),
    enabled: !!id,
  });
}

export function useTaskRuns(id: string) {
  return useQuery({
    queryKey: ["tasks", id, "runs"],
    queryFn: () =>
      fetchJson<TaskRunDto[]>(
        `/api/tasks/${encodeURIComponent(id)}/runs`
      ),
    enabled: !!id,
  });
}

export function useGroups() {
  return useQuery({
    queryKey: ["groups"],
    queryFn: () => fetchJson<GroupDto[]>("/api/groups"),
    refetchInterval: 15_000,
  });
}

export function useGroup(jid: string) {
  return useQuery({
    queryKey: ["groups", jid],
    queryFn: () =>
      fetchJson<GroupDto>(`/api/groups/${encodeURIComponent(jid)}`),
    enabled: !!jid,
  });
}

export function useSystemHealth() {
  return useQuery({
    queryKey: ["system", "health"],
    queryFn: () => fetchJson<SystemHealthDto>("/api/system/health"),
    refetchInterval: 5_000,
  });
}

export function useRouterState() {
  return useQuery({
    queryKey: ["system", "routerState"],
    queryFn: () => fetchJson<RouterStateDto[]>("/api/system/router-state"),
    refetchInterval: 10_000,
  });
}

export function useWorkspaceTree(groupFolder: string) {
  return useQuery({
    queryKey: ["workspace", groupFolder, "tree"],
    queryFn: () =>
      fetchJson<WorkspaceTreeEntryDto[]>(
        `/api/workspace/${encodeURIComponent(groupFolder)}/tree`
      ),
    enabled: !!groupFolder,
  });
}

export function useWorkspaceFile(groupFolder: string, path: string) {
  return useQuery({
    queryKey: ["workspace", groupFolder, "file", path],
    queryFn: () =>
      fetchJson<WorkspaceFileDto>(
        `/api/workspace/${encodeURIComponent(groupFolder)}/file?path=${encodeURIComponent(path)}`
      ),
    enabled: !!groupFolder && !!path,
  });
}
