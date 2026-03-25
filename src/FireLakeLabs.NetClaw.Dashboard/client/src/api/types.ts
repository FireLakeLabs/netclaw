export interface AgentActivityEventDto {
  id: number;
  groupFolder: string;
  chatJid: string;
  sessionId: string | null;
  eventKind: string;
  content: string | null;
  toolName: string | null;
  error: string | null;
  isScheduledTask: boolean;
  taskId: string | null;
  observedAt: string;
  capturedAt: string;
}

export interface QueueStateDto {
  activeExecutions: number;
  maxConcurrentExecutions: number;
  waitingGroupCount: number;
  groups: GroupQueueStateDto[];
}

export interface GroupQueueStateDto {
  chatJid: string;
  active: boolean;
  isTaskExecution: boolean;
  pendingMessages: boolean;
  pendingTaskCount: number;
  idleWaiting: boolean;
  retryCount: number;
  runningTaskIds: string[];
}

export interface ChatSummaryDto {
  jid: string;
  name: string;
  lastMessageTime: string;
  channel: string | null;
  isGroup: boolean;
}

export interface MessageDto {
  id: string;
  chatJid: string;
  sender: string;
  senderName: string;
  content: string;
  timestamp: string;
  isFromMe: boolean;
  isBotMessage: boolean;
  attachments: FileAttachmentDto[] | null;
}

export interface FileAttachmentDto {
  fileId: string;
  fileName: string;
  fileSizeBytes: number;
  mimeType: string | null;
}

export interface TaskDto {
  id: string;
  groupFolder: string;
  chatJid: string;
  prompt: string;
  scheduleType: string;
  scheduleValue: string;
  contextMode: string;
  nextRun: string | null;
  lastRun: string | null;
  lastResult: string | null;
  status: string;
  createdAt: string;
}

export interface TaskRunDto {
  taskId: string;
  runAt: string;
  durationMs: number;
  status: string;
  result: string | null;
  error: string | null;
}

export interface GroupDto {
  jid: string;
  name: string;
  folder: string;
  trigger: string;
  requiresTrigger: boolean;
  isMain: boolean;
  addedAt: string;
  sessionId: string | null;
}

export interface SystemHealthDto {
  serverTime: string;
  uptimeSeconds: number;
  channels: ChannelStatusDto[];
  queueState: QueueStateDto;
}

export interface ChannelStatusDto {
  name: string;
  isConnected: boolean;
}

export interface RouterStateDto {
  key: string;
  value: string;
}

export interface WorkerHeartbeatDto {
  serverTime: string;
  channels: ChannelStatusDto[];
  queueState: QueueStateDto;
}

export interface WorkspaceTreeEntryDto {
  name: string;
  relativePath: string;
  isDirectory: boolean;
  sizeBytes: number | null;
  lastModified: string | null;
  children: WorkspaceTreeEntryDto[] | null;
}

export interface WorkspaceFileDto {
  relativePath: string;
  content: string;
  sizeBytes: number;
  lastModified: string;
}
