# File-Based Persistence: Storage Topology Refactor

> **Status:** Proposal (revised)  
> **Date:** 2026-03-25  
> **Scope:** Replace SQLite with file-system persistence for all 9 tables; refactor runtime path layout, dashboard serving, host initialization, and setup CLI to match  
> **Backward compatibility:** Not required — no production users

---

## 1. Executive Summary

NetClaw currently stores all persistent state in a single SQLite database (`data/netclaw.db`) across 9 tables: messages, chats, sessions, registered_groups, scheduled_tasks, task_run_logs, agent_events, router_state, and file_attachments.

This plan replaces SQLite entirely with a file-based persistence layer. Every table gets a purpose-built file storage strategy matched to its access patterns. **This is a storage topology refactor, not a narrow repository swap.** Beyond the 7 repository implementations, the following components have storage-layout assumptions that must be updated:

- **`NetClawAgentWorkspaceBuilder`** — creates session and workspace directories under `data/sessions/` and `data/agent-workspaces/`
- **`GroupPathResolver`** — resolves 4 distinct path types per group (`groups/`, `data/ipc/`, `data/sessions/`, `data/agent-workspaces/`)
- **`WorkspaceFileService`** (dashboard) — exposes a virtual tree with three roots (`groups/`, `workspace/`, `sessions/`) and serves raw files from those paths
- **`DashboardEndpoints`** — serves attachment binaries from `FileAttachment.LocalPath` and maps agent event IDs to DTOs
- **`HostInitializationService`** — creates the directory skeleton and initializes the SQLite schema
- **`HostPathOptions`** — defines `DatabasePath` and related paths

The repository interfaces (`IMessageRepository`, `ISessionRepository`, etc.) remain unchanged — they are the migration's stability contract for the application layer. But the broader runtime, dashboard, and setup components require coordinated changes.

### Motivation

1. **Observability** — Conversations, events, and state become directly visible via `cat`, `grep`, `tail -f`, `ls -lt`, and any file manager. No SQLite tooling required.
2. **Agent self-awareness** — Conversation files in group workspace directories are readable by the agent itself, enabling it to reference past interactions without them being injected into the prompt.
3. **Ecosystem alignment** — Claude Code, Copilot, and other agent frameworks use file-based conversation storage. This is the expected pattern.
4. **Operational simplicity** — Backup is `cp -r`. Restore is `cp -r`. No locked database files, no WAL journals, no corruption recovery.
5. **Dependency reduction** — Removes `Microsoft.Data.Sqlite` and its native SQLite binaries from the build.

### What We're Giving Up

- **Indexed queries** — SQL filtering by multiple predicates becomes in-memory filtering after file reads.
- **Implicit write serialization** — We must handle concurrency ourselves (solved via `ConcurrentDictionary` caches and atomic file writes).
- **Relational integrity** — Foreign key enforcement moves to application code.
- **Query optimizer** — Complex queries become explicit code. Acceptable at NetClaw's scale (tens of chats, not thousands).

### Architectural Decision: Two Questions Settled Up Front

Before detailing the design, the two foundational questions from the review:

> 1. **Do chats remain globally indexed, or move under group-owned trees?**

**Chats remain globally indexed.** A central `data/chats/` directory holds message files keyed by `ChatJid`. Group ownership is tracked in a separate mapping file (`data/chat-groups.json`). This matches the current reality: `IMessageRepository` is chat-global (keyed by `ChatJid`, not `GroupFolder`), `ChannelIngressService` stores messages before group association exists, and `SqliteMessageRepository.StoreMessageAsync` auto-creates chat records from a message alone with no group context. Putting messages under group trees would create an unresolvable bootstrapping problem for unregistered chats and early metadata.

> 2. **Do sessions/workspaces/files stay in their current `data/` locations, or get redesigned?**

**The existing `data/` layout for sessions, agent-workspaces, and IPC is preserved.** `GroupPathResolver` continues to resolve `data/sessions/{group}`, `data/agent-workspaces/{group}`, and `data/ipc/{group}`. The dashboard's `WorkspaceFileService` virtual tree (`groups/`, `workspace/`, `sessions/`) remains valid. What changes is: the SQLite database is removed, and the new file stores slot into the existing `data/` tree alongside the existing runtime directories.

---

## 2. Current Architecture

### SQLite Tables (9 total)

| Table | Rows at Scale | Access Pattern | Migration Difficulty |
|-------|--------------|----------------|---------------------|
| `router_state` | ~20 keys | Pure key-value | Trivial |
| `sessions` | 1 per group (~5-20) | Pure key-value | Trivial |
| `registered_groups` | ~5-20 | Pure key-value | Trivial |
| `chats` | ~5-50 | Key-value + list all | Easy |
| `scheduled_tasks` | ~5-50 | Key-value + filtered scan | Easy |
| `task_run_logs` | ~100-1000 | Append-only per task | Easy |
| `messages` | ~1K-50K | Temporal range + multi-predicate filter | Moderate |
| `file_attachments` | ~10-500 | Composite key + bulk lookup by message | Moderate |
| `agent_events` | ~5K-100K | Temporal + grouping by session/group | Moderate |

### Repository Interfaces (7 interfaces, 31 methods)

All defined in `NetClaw.Domain/Contracts/Persistence/`. These interfaces remain unchanged — they are the migration's stability contract for the application layer.

### Non-Repository Components With Storage Layout Assumptions

| Component | Current Assumption | Impact |
|-----------|-------------------|--------|
| `GroupPathResolver` | Resolves `data/sessions/{group}`, `data/agent-workspaces/{group}`, `data/ipc/{group}` | **No change** — these paths remain valid |
| `NetClawAgentWorkspaceBuilder` | Creates dirs via `GroupPathResolver`, writes `AGENTS.md` to group + workspace dirs | **Minor change** — conversation symlinks or path additions for agent readability |
| `WorkspaceFileService` | Virtual tree: `groups/`, `workspace/`, `sessions/` | **Minor change** — add `conversations/` tree root |
| `DashboardEndpoints` | Serves File attachments from `FileAttachment.LocalPath`; maps `AgentActivityEvent.Id` (long) to DTOs; exposes task run logs up to 500 | **Changes required** — see sections below |
| `HostInitializationService` | Creates dir skeleton + initializes SQLite schema | **Change** — remove schema init, add new directories |
| `HostPathOptions` | Defines `DatabasePath` | **Change** — remove `DatabasePath` |

### Dependency Graph

```
registered_groups (ROOT)
├── sessions (1:1 by group_folder)
├── scheduled_tasks (references group_folder, chat_jid)
│   └── task_run_logs (FK: task_id)
└── agent_events (references group_folder, session_id, task_id)

chats (ROOT — globally indexed, independent of groups)
├── messages (FK: chat_jid → chats.jid)
│   └── file_attachments (references message_id, chat_jid)

chat_groups (MAPPING — ChatJid → GroupFolder, populated by InboundMessagePollingService's routing logic)

router_state (STANDALONE — pure key-value)
```

---

## 3. Target Directory Structure

All paths relative to `{ProjectRoot}` (default `~/.netclaw`).

```
{ProjectRoot}/
├── data/
│   ├── state.json                          # router_state (key-value pairs)
│   ├── groups.json                         # registered_groups (all groups in one file)
│   ├── chat-groups.json                    # ChatJid → GroupFolder mapping (derived from registered_groups)
│   │
│   ├── chats/                              # CENTRAL chat store (globally indexed by ChatJid)
│   │   └── {chat_jid}/                     # One directory per ChatJid
│   │       ├── metadata.json               # ChatInfo (name, channel, is_group, last_message_time)
│   │       ├── messages.jsonl              # All messages, one per line, append-only
│   │       └── attachments/
│   │           └── {file_id}.json          # Attachment metadata record (one per attachment)
│   │
│   ├── files/                              # BINARY attachment storage (EXISTING — unchanged)
│   │   └── {conversation_id}/
│   │       └── {file_id}/
│   │           └── {filename}              # Actual file bytes (downloaded by SlackChannel)
│   │
│   ├── tasks/
│   │   └── {task_id}/
│   │       ├── config.json                 # ScheduledTask definition
│   │       └── runs.jsonl                  # TaskRunLog entries, append-only
│   │
│   ├── events/
│   │   └── {group_folder}/
│   │       └── {YYYY-MM-DD}.jsonl          # Agent events, one line per event
│   │
│   ├── ipc/                                # (EXISTING — unchanged)
│   │   └── {group_folder}/
│   ├── sessions/                           # (EXISTING — runtime session data, unchanged)
│   │   └── {group_folder}/
│   └── agent-workspaces/                   # (EXISTING — unchanged)
│       └── {group_folder}/
│           └── AGENTS.md
│
├── groups/                                 # (EXISTING — group source directories)
│   ├── main/
│   │   ├── session.json                    # Session ID mapping (new)
│   │   └── .uploads/                       # Staged files (existing)
│   ├── global/                             # Shared instructions (existing)
│   └── {other_group}/
│       ├── session.json
│       └── .uploads/
│
├── mount-allowlist.json                    # (EXISTING — unchanged)
├── sender-allowlist.json                   # (EXISTING — unchanged)
└── logs/                                   # (EXISTING — unchanged)
```

### Design Rationale

**Chats live under `data/chats/{chat_jid}/`** (centrally, not under group trees) because:
- `IMessageRepository` is globally indexed by `ChatJid` — every method takes `ChatJid` as a parameter, not `GroupFolder`
- `ChannelIngressService.HandleMessageAsync` stores messages before any group routing happens — the group association doesn't exist yet at storage time
- `SqliteMessageRepository.StoreMessageAsync` auto-creates chat records from a bare message with no group context (chat name defaults to the JID value, `is_group` defaults to `false`)
- Unregistered chats (messages that arrive for JIDs not yet associated with any group) are stored and silently skipped during polling — they need a stable home regardless of group registration state
- If a chat's group association changes later (via re-registration), messages stay in place — no file moves needed

**Chat-to-group mapping is explicit in `data/chat-groups.json`** because:
- The current system derives this mapping at runtime by calling `IGroupRepository.GetAllAsync()` and matching `ChatJid` → `RegisteredGroup` (1:1 mapping via the JID)
- This mapping is needed by the dashboard and workspace builder to know which group's conversations to expose
- A dedicated file makes this relationship observable and debuggable

**Attachment binaries stay in `data/files/`** (the existing Slack download location) because:
- `SlackChannel.DownloadFileAsync` already stores files at `{DataDirectory}/files/{conversation_id}/{file_id}/{sanitized_filename}`
- `GroupMessageProcessorService` copies from `FileAttachment.LocalPath` to `groups/{folder}/.uploads/` for agent access
- `DashboardEndpoints` serves the binary directly from `FileAttachment.LocalPath`
- Moving binaries would break all three consumers for no benefit — the metadata records in `data/chats/{chat_jid}/attachments/` point to the binary via `localPath`

**Sessions live in `groups/{folder}/session.json`** (in the group directory, not `data/sessions/`) because:
- Session state is a simple key-value (group_folder → session_id), naturally co-located with the group
- `data/sessions/{group}` is the *runtime* session directory (agent logs, transient state), which is a different concept
- This avoids confusion between the session ID file and the session runtime directory

**Events live under `data/events/{group_folder}/`** because:
- Events are operational telemetry, not agent context
- Daily rotation via `{YYYY-MM-DD}.jsonl` prevents unbounded file growth
- Separating from group workspace keeps the agent's view clean

**Tasks use separate config and run-log files** (`config.json` + `runs.jsonl`) because:
- Run logs are append-only and can grow large — the dashboard exposes up to 500 runs per task
- Embedding run logs in the config file would require read-modify-write on every run, risking corruption
- Separate `runs.jsonl` allows pure append semantics while `config.json` is atomically rewritten only on task updates

---

## 4. File Formats

### 4.1 Messages (`messages.jsonl`)

One JSON object per line, append-only. Fields match `StoredMessage`:

```jsonl
{"id":"msg_001","chatJid":"alice@s.whatsapp.net","sender":"alice","senderName":"Alice","content":"Hello","timestamp":"2026-03-25T10:00:00.000+00:00","isFromMe":false,"isBotMessage":false}
{"id":"msg_002","chatJid":"alice@s.whatsapp.net","sender":"netclaw","senderName":"NetClaw","content":"Hi Alice!","timestamp":"2026-03-25T10:00:05.000+00:00","isFromMe":true,"isBotMessage":true}
```

**Filtering strategy**: Read the file, deserialize each line, filter in memory. At NetClaw's scale (hundreds to low thousands of messages per chat), this is fast. A 10,000-message JSONL file is ~2-5MB — parsed in <50ms.

**Write strategy**: True append-only via `File.AppendAllTextAsync` under a per-chat `SemaphoreSlim`. This is *not* atomic — a crash mid-append can produce a partial final line. The reader tolerates this: on load, if the last line fails JSON deserialization, it is silently discarded. This is an accepted loss semantic — at most one message from a crash event. The alternative (full-file rewrite via temp+rename on every message) is prohibitively expensive for high-volume chats.

**Deduplication**: Before appending, check the in-memory last-N-message-IDs set (see §5.3). On read, the reader also deduplicates by `id` as a safety net for the restart-during-append scenario.

### 4.2 Chat Metadata (`metadata.json`)

```json
{
  "jid": "alice@s.whatsapp.net",
  "name": "Alice",
  "lastMessageTime": "2026-03-25T10:00:05.000+00:00",
  "channel": "slack",
  "isGroup": false
}
```

Updated atomically (temp file + rename) on every message store. This file is small and rewritten in full — atomic rename is appropriate here.

### 4.3 Chat-to-Group Mapping (`chat-groups.json`)

```json
{
  "alice@s.whatsapp.net": "main",
  "team-channel@slack": "engineering"
}
```

Derived from `IGroupRepository.GetAllAsync()` — each `RegisteredGroup` has a JID and a folder. This file is rebuilt whenever a group is registered or updated. It provides the reverse lookup (`ChatJid → GroupFolder`) needed by the dashboard and workspace builder.

Note: chats can exist without a group mapping (messages arrive before registration). Such chats are stored in `data/chats/{jid}/` normally but are not routed to any agent.

### 4.4 Session (`session.json`)

```json
{
  "groupFolder": "main",
  "sessionId": "sess_abc123"
}
```

One file per group, in the group's root directory (`groups/{folder}/session.json`).

### 4.5 Registered Groups (`groups.json`)

```json
[
  {
    "jid": "main@netclaw",
    "name": "Main",
    "folder": "main",
    "trigger": "__disabled__",
    "addedAt": "2026-03-25T00:00:00.000+00:00",
    "containerConfig": null,
    "requiresTrigger": false,
    "isMain": true
  }
]
```

Single file with all groups. Loaded into memory at startup, rewritten on change. At ~5-20 groups, this is always small.

**Domain alignment note**: The field is named `trigger` (not `triggerPattern`) to match `RegisteredGroup.Trigger`. The `RegisteredGroup` constructor requires a non-empty trigger string (throws `ArgumentException` on `IsNullOrWhiteSpace`) before any `isMain` or `requiresTrigger` logic is applied. That means the serialized value must always be non-empty. For groups that do not require trigger matching, persist either the configured trigger or a sentinel value such as `"__disabled__"` so the object round-trips through the current domain model without a constructor change.

### 4.6 Router State (`state.json`)

```json
{
  "last_timestamp": "2026-03-25T10:00:05.000+00:00",
  "last_agent_timestamp:alice@s.whatsapp.net": "2026-03-25T10:00:05.000+00:00",
  "last_agent_timestamp:team@s.whatsapp.net": "2026-03-25T09:55:00.000+00:00"
}
```

Single flat key-value file. Loaded at startup, kept in memory, flushed on write via atomic temp+rename.

### 4.7 Scheduled Tasks (`data/tasks/{task_id}/`)

**Config** (`config.json`):
```json
{
  "id": "task_001",
  "groupFolder": "main",
  "chatJid": "alice@s.whatsapp.net",
  "prompt": "Check for new emails",
  "scheduleType": "interval",
  "scheduleValue": "30m",
  "contextMode": "isolated",
  "nextRun": "2026-03-25T10:30:00.000+00:00",
  "lastRun": "2026-03-25T10:00:00.000+00:00",
  "lastResult": "No new emails",
  "status": "active",
  "createdAt": "2026-03-25T09:00:00.000+00:00"
}
```

**Run logs** (`runs.jsonl`) — append-only, one entry per line:
```jsonl
{"runAt":"2026-03-25T10:00:00.000+00:00","durationMs":5200,"status":"completed","result":"No new emails","error":null}
```

Run logs are stored separately (not embedded in config) for two reasons:
1. **No cap on run history.** The current `SqliteTaskRepository` stores logs without truncation, and the dashboard exposes up to 500 runs via `Math.Clamp(limit ?? 50, 1, 500)`. Capping at 50 would be a behavior regression. The append-only `runs.jsonl` grows unbounded, matching the current SQLite behavior.
2. **Append-only semantics.** `AppendRunLogAsync` is a pure append — no read-modify-write cycle, no risk of corrupting the task config during a run log write.

`GetRunLogsAsync(taskId, limit)` reads the JSONL in reverse (last N lines) to serve the most recent runs efficiently.

### 4.8 File Attachments (`data/chats/{chat_jid}/attachments/{file_id}.json`)

**Metadata record** (one per attachment):
```json
{
  "fileId": "F123ABC",
  "messageId": "msg_001",
  "chatJid": "alice@s.whatsapp.net",
  "fileName": "photo.jpg",
  "mimeType": "image/jpeg",
  "fileSize": 245000,
  "localPath": "/home/user/.netclaw/data/files/C123/F123ABC/photo.jpg",
  "downloadedAt": "2026-03-25T10:00:01.000+00:00"
}
```

**Binary file lifecycle** (unchanged from current behavior):

| Stage | Location | Owner |
|-------|----------|-------|
| Download | `data/files/{conversation_id}/{file_id}/{filename}` | `SlackChannel.DownloadFileAsync` |
| Metadata | `data/chats/{chat_jid}/attachments/{file_id}.json` | `FileAttachmentRepository.StoreAsync` |
| Staging | `groups/{folder}/.uploads/{filename}` | `GroupMessageProcessorService.StageAttachmentFiles` (copies from `LocalPath`) |
| Dashboard serving | Served from `FileAttachment.LocalPath` (= `data/files/...`) | `DashboardEndpoints` |

The `localPath` field in the metadata record always points to the canonical binary location under `data/files/`. This path is:
- Written by `SlackChannel.DownloadFileAsync` when a file is downloaded
- Read by `GroupMessageProcessorService` to copy into `.uploads/` for agent access
- Read by `DashboardEndpoints` to serve the file via HTTP

**Retention policy**: Binary files in `data/files/` are retained indefinitely (matching current behavior). A future cleanup job could prune files older than N days, but that is out of scope for this migration.

### 4.9 Agent Events (`data/events/{group_folder}/{YYYY-MM-DD}.jsonl`)

```jsonl
{"id":1,"groupFolder":"main","chatJid":"alice@s.whatsapp.net","sessionId":"sess_abc","eventKind":"TextDelta","content":"Hello","toolName":null,"error":null,"isScheduledTask":false,"taskId":null,"observedAt":"2026-03-25T10:00:05.000+00:00","capturedAt":"2026-03-25T10:00:05.001+00:00"}
```

Daily JSONL files. Append-only. Queries filter in memory after reading the relevant day file(s).

**Event ID generation**: `AgentActivityEvent` requires a `long Id` property, used by the domain model and mapped to dashboard DTOs (`AgentEventDto.Id`, `AgentEventDto.ObservedAt`). SQLite currently auto-assigns this via `INTEGER PRIMARY KEY AUTOINCREMENT`. In the file-based system:

- A persisted global counter file (`data/events/next-id.txt`) is the authority for the next event ID.
- On startup, if `next-id.txt` exists, its value is loaded. If it does not exist, the repository performs a one-time scan across all retained event files to find the maximum existing `id`, initializes the counter to `max + 1`, and writes `next-id.txt`.
- On every allocation, the repository reserves the next ID from the in-memory counter and periodically flushes the high-water mark back to `next-id.txt` via atomic temp-file + rename. This guarantees IDs remain globally unique across process restarts and across multi-day dashboard queries.
- IDs remain monotonic for the lifetime of the installation, preserving the current dashboard assumption that `id` is a stable unique key.

---

## 5. Solving the Hard Problems

### 5.1 Cross-Chat Polling (`GetNewMessagesAsync`)

**Problem**: The message loop calls `GetNewMessagesAsync(since)` every 1 second to find new messages across ALL chats. In SQLite this is one indexed query. With files, we'd need to scan every chat's `messages.jsonl`.

**Solution: In-memory write-through cache**

The `FileMessageRepository` maintains a `ConcurrentDictionary<ChatJid, DateTimeOffset>` tracking the latest message timestamp per chat. On `StoreMessageAsync`, the cache is updated. On `GetNewMessagesAsync`, only chats whose cached timestamp > `since` are read from disk.

```
StoreMessage(msg) →
  1. Acquire per-chat SemaphoreSlim
  2. Append JSON line to data/chats/{chatJid}/messages.jsonl
  3. Atomic-write data/chats/{chatJid}/metadata.json (temp + rename)
  4. Update in-memory cache: latestTimestamp[chatJid] = msg.Timestamp
  5. Release lock

GetNewMessages(since) →
  1. Filter cache: chats where latestTimestamp > since
  2. For each matching chat: read + filter messages.jsonl from disk
  3. Merge, sort by timestamp, return
```

**On startup**, the cache is populated by scanning all `metadata.json` files in `data/chats/*/` (tens of directories, fast).

This gives us O(1) for the common case (no new messages) and O(affected chats) when there are new messages — comparable to the indexed SQL query.

### 5.2 Concurrent Write Safety

**Problem**: Multiple channels (Slack, Terminal, ReferenceFile) can call `StoreMessageAsync` concurrently for the same or different chats.

**Solution: Per-chat write lock + two write strategies**

```csharp
private readonly ConcurrentDictionary<string, SemaphoreSlim> _chatLocks = new();

async Task StoreMessageAsync(StoredMessage message, CancellationToken ct)
{
    var chatLock = _chatLocks.GetOrAdd(message.ChatJid.Value, _ => new SemaphoreSlim(1, 1));
    await chatLock.WaitAsync(ct);
    try
    {
        // 1. Append message line to messages.jsonl (File.AppendAllTextAsync)
        // 2. Atomic-write metadata.json (temp file + File.Move)
        // 3. Update in-memory caches
    }
    finally
    {
        chatLock.Release();
    }
}
```

Different chats write to different files — no contention. Same-chat writes are serialized by the `SemaphoreSlim`.

**Two distinct write strategies, chosen per file type:**

| File | Strategy | Rationale |
|------|----------|-----------|
| `messages.jsonl` | `File.AppendAllTextAsync` under lock | Performance — rewriting a growing file on every message is O(N). Accepted loss: at most one partial line on crash, detected and skipped by reader. |
| `metadata.json` | Temp file + `File.Move` (atomic rename) | Correctness — small file, always fully rewritten, must never be partially written. |
| `state.json`, `groups.json`, `config.json` | Temp file + `File.Move` (atomic rename) | Same — small files, full rewrite, atomicity required. |
| `runs.jsonl`, `events/*.jsonl` | `File.AppendAllTextAsync` under lock | Same append-only pattern as messages. Accepted partial-line loss on crash. |

### 5.3 Message Deduplication

**Problem**: The SQLite composite PK `(id, chat_jid)` prevents duplicate inserts automatically.

**Solution**: Before appending, check against the in-memory last-N-message-IDs set (a `HashSet<string>` per chat, populated from the tail of the JSONL on startup). For the restart-during-append scenario: the reader deduplicates by `id` when loading (first occurrence wins).

### 5.4 Chat Ownership and the Bootstrapping Problem

**Problem**: Messages are stored globally by `ChatJid`, but the system needs to know which group owns a chat for workspace building, dashboard exposure, and agent context. Currently, `ChannelIngressService` stores a message *before* group routing — the chat may not be registered to any group yet.

**Solution**: Three-layer approach:

1. **Message storage is group-agnostic.** `FileMessageRepository` stores messages in `data/chats/{chat_jid}/` unconditionally. No group knowledge required. This matches the current `IMessageRepository` interface which takes `ChatJid`, not `GroupFolder`.

2. **Group registration builds the mapping.** When `IGroupRepository.UpsertAsync` is called (registering a group with its JID), the `FileGroupRepository` also updates `data/chat-groups.json` with the `ChatJid → GroupFolder` entry. This is a derived index — the authoritative source is the group registration itself.

3. **Consumers resolve chat→group on demand.** `WorkspaceFileService`, workspace builder, and dashboard call into a `IChatGroupResolver` (or query the group repository) to find which group owns a chat. Unregistered chats return null — they exist in `data/chats/` but aren't exposed through any group view.

**Edge cases:**
- *Unregistered chat*: Messages stored, silently skipped during polling (matches current behavior exactly).
- *Chat re-assigned to different group*: `chat-groups.json` updated, messages stay in `data/chats/`. No file moves.
- *Early metadata*: Chat metadata created from bare message (name = JID value, is_group = false). Updated with richer data when channel provides it (e.g., Slack chat name, group flag).

### 5.5 File Attachment Bulk Lookup

**Problem**: `GetByMessagesAsync(messageIds, chatJid)` does a bulk load of attachments for multiple messages. In SQLite this is a single `WHERE message_id IN (...)` query.

**Solution**: Attachments live under `data/chats/{chat_jid}/attachments/`. Load all `*.json` files from that directory, deserialize, and filter by `messageId in messageIds`. At typical scale (~5-50 attachment files per chat), this is a single directory read + small parse.

For `GetByFileIdAsync(fileId)` (cross-chat lookup, used only for dedup on re-download): maintain an in-memory `fileId → (chatJid, path)` index populated on startup.

### 5.6 Agent Event ID Generation

**Problem**: `AgentActivityEvent.Id` is a `long`, currently auto-assigned by SQLite `AUTOINCREMENT`. The dashboard maps this to `AgentEventDto.Id`. File-based storage has no built-in auto-increment.

**Solution**: Per-process monotonic counter via `Interlocked.Increment` on a `long` field. Initialized on startup by reading the max `id` from today's event files across all groups. IDs are monotonically increasing within a process lifetime and within a day. The dashboard uses `id` for ordering within query results (alongside `observed_at`) — this usage is preserved. Cross-restart ID gaps are acceptable since no external consumer depends on contiguous IDs.

---

## 6. Implementation Plan

### Phase 1: Infrastructure Foundation

**New files to create:**

| File | Purpose |
|------|---------|
| `Persistence/FileSystem/FileAtomicWriter.cs` | Shared utility: temp file + rename pattern for small files; JSON serialization helpers |
| `Persistence/FileSystem/JsonlFileReader.cs` | Shared utility: read JSONL, deserialize line by line, with optional predicate filter; tolerant of partial last line |
| `Persistence/FileSystem/JsonlFileAppender.cs` | Shared utility: append a JSON line to a JSONL file under a provided lock |
| `Persistence/FileSystem/FileStoragePaths.cs` | Centralized path resolution (chats dir, events dir, tasks dir, etc.) |

**Key patterns** (borrowed from `ReferenceFileChannel`):
- Atomic writes (small files): `File.WriteAllTextAsync(tempPath)` → `File.Move(tempPath, targetPath, overwrite: true)`
- JSONL append (large files): `File.AppendAllTextAsync(path, jsonLine + "\n")` under per-resource `SemaphoreSlim`
- JSON options: `JsonSerializerDefaults.Web` with `WriteIndented = false` for JSONL, `WriteIndented = true` for config files

### Phase 2: Simple Key-Value Repositories (4 repos)

Replace in any order — no dependencies between them.

#### 2a. `FileRouterStateRepository` → replaces `SqliteRouterStateRepository`
- **Storage**: `data/state.json` (flat key-value object)
- **Behavior**: Load into `ConcurrentDictionary` at construction. Flush to disk on every `SetAsync` via atomic write.
- **Complexity**: Trivial. 3 methods, all key-value.

#### 2b. `FileSessionRepository` → replaces `SqliteSessionRepository`
- **Storage**: `groups/{folder}/session.json`
- **Behavior**: Read file on `GetByGroupFolderAsync`, write file on `UpsertAsync`. `GetAllAsync` scans all group directories.
- **Complexity**: Trivial. 3 methods.

#### 2c. `FileGroupRepository` → replaces `SqliteGroupRepository`
- **Storage**: `data/groups.json` (array of all groups)
- **Side effect**: On `UpsertAsync`, also rebuilds `data/chat-groups.json` (the `ChatJid → GroupFolder` mapping derived from all groups).
- **Behavior**: Load into memory on first access. Rewrite file on `UpsertAsync`. `GetAllAsync` and `GetByJidAsync` serve from cache.
- **Domain validation**: Serialize `trigger` (not `triggerPattern`) to match `RegisteredGroup.Trigger`. Ensure round-trip through the `RegisteredGroup` constructor succeeds — validate field names against the actual domain type, including the non-empty trigger constraint.
- **Complexity**: Trivial. 3 methods + the chat-groups.json side effect.

#### 2d. `FileTaskRepository` → replaces `SqliteTaskRepository`
- **Storage**: `data/tasks/{taskId}/config.json` + `data/tasks/{taskId}/runs.jsonl`
- **Behavior**: Load all tasks into `ConcurrentDictionary` at startup. Write `config.json` via atomic write on create/update. `AppendRunLogAsync` appends to `runs.jsonl`. `GetDueTasksAsync` filters in-memory. `GetRunLogsAsync` reads `runs.jsonl` in reverse (last N lines from file tail).
- **No run log cap.** The `runs.jsonl` file is unbounded, matching the current SQLite behavior where the dashboard can request up to 500 runs.
- **Complexity**: Easy. 7 methods.

### Phase 3: Message Repository (the big one)

#### 3a. `FileMessageRepository` → replaces `SqliteMessageRepository`

**Storage** (centrally indexed under `data/chats/`):
- `data/chats/{chat_jid}/messages.jsonl`
- `data/chats/{chat_jid}/metadata.json`

**In-memory state**:
- `ConcurrentDictionary<string, DateTimeOffset> _latestTimestamps` — per-chat latest message time
- `ConcurrentDictionary<string, SemaphoreSlim> _chatLocks` — per-chat write serialization
- `ConcurrentDictionary<string, HashSet<string>> _recentMessageIds` — per-chat last-100 IDs for dedup

**Method implementations**:

| Method | Strategy |
|--------|----------|
| `StoreMessageAsync` | Acquire chat lock → append JSONL line → atomic-write metadata.json → update caches |
| `GetNewMessagesAsync(since)` | Check `_latestTimestamps` for chats with activity > since → read + filter only those JSONL files → merge + sort |
| `GetMessagesSinceAsync(chatJid, since, assistantName)` | Read `messages.jsonl` for that chat → filter by timestamp, `!isBotMessage`, `!content.StartsWith(assistantName + ":")` → deduplicate by id |
| `GetChatHistoryAsync(chatJid, limit, since)` | Read `messages.jsonl` → filter by since → take last N |
| `GetAllChatsAsync` | Scan all `metadata.json` files under `data/chats/*/` |
| `StoreChatMetadataAsync` | Atomic-write `metadata.json` |

**Initialization**: On startup, scan all `data/chats/*/metadata.json` to populate `_latestTimestamps`. Scan tail of each `messages.jsonl` (last 100 lines) to populate `_recentMessageIds`.

**No group knowledge required.** The message repository is purely `ChatJid`-indexed. It does not need to know which group owns which chat. This matches the interface and the current SQLite implementation.

### Phase 4: File Attachment Repository

#### 4a. `FileAttachmentRepository` → replaces `SqliteFileAttachmentRepository`

**Storage**: `data/chats/{chat_jid}/attachments/{file_id}.json` (metadata only — binary files remain at their existing `data/files/` locations)

**Method implementations**:

| Method | Strategy |
|--------|----------|
| `StoreAsync` | Atomic-write `{file_id}.json` to the chat's attachments dir |
| `GetByFileIdAsync(fileId)` | Look up in-memory `fileId → path` index (populated at startup from all attachment dirs). Avoid cross-chat directory scan at runtime. |
| `GetByMessageAsync(messageId, chatJid)` | List `data/chats/{chatJid}/attachments/*.json` → filter by `messageId` |
| `GetByMessagesAsync(messageIds, chatJid)` | Same directory scan → filter by `messageId IN set` → group by messageId |

**Binary files are not moved.** The `localPath` in each attachment metadata record continues to point to `data/files/{conversation_id}/{file_id}/{filename}` — the location where `SlackChannel` originally downloads them. The dashboard serves from this path. `GroupMessageProcessorService` copies from this path to `.uploads/`. No change to the binary lifecycle.

### Phase 5: Agent Event Repository

#### 5a. `FileAgentEventRepository` → replaces `SqliteAgentEventRepository`

**Storage**: `data/events/{group_folder}/{YYYY-MM-DD}.jsonl`

**ID generation**: Persisted monotonic `long` counter backed by `data/events/next-id.txt`. On first startup after migration, initialize it from the max `id` across all retained event files. Thereafter, allocate IDs from the counter and flush the high-water mark atomically.

**Method implementations**:

| Method | Strategy |
|--------|----------|
| `StoreAsync` / `StoreBatchAsync` | Assign ID(s) from monotonic counter → append line(s) to today's JSONL file |
| `GetRecentAsync(limit, since?, groupFolder?)` | If groupFolder specified: read that group's JSONL files from `since` date forward. If not: read all groups' files. Filter + sort + limit in memory. |
| `GetBySessionAsync(sessionId)` | Scan relevant group's JSONL files → filter by sessionId. If group_folder unknown, scan all. |
| `GetByTaskRunAsync(taskId, runAt)` | Scan from `runAt` date file forward → filter by taskId + observedAt >= runAt |

### Phase 6: Non-Repository Component Updates

This phase addresses the runtime, dashboard, and host components that have storage-layout assumptions beyond the repository interfaces.

#### 6a. `HostInitializationService`
- **Remove**: SQLite schema initialization call
- **Add**: Create `data/chats/` directory
- **Add**: Create `data/tasks/` directory  
- **Add**: Create `data/events/` directory
- **Keep**: Existing directory creation for `data/ipc/`, `data/sessions/`, `groups/`, etc.
- **Keep**: Permission restriction logic (apply to new directories too)

#### 6b. `HostPathOptions`
- **Remove**: `DatabasePath` property
- **Add**: `ChatsDirectory` → `{DataDirectory}/chats` (or derive in `StorageOptions`)

#### 6c. `NetClawAgentWorkspaceBuilder`
- **Minor change**: When building the workspace for a group, optionally symlink or copy conversation summaries into the agent workspace so the agent can read its own history.
- **Decision point**: Do we symlink `data/chats/{group's chat jid}/` into `data/agent-workspaces/{group}/conversations/`? Or generate a summary file? This depends on whether we want the agent to see raw JSONL (machine-readable but verbose) or formatted markdown (human-readable). For now: **no change to workspace builder**. The agent already receives conversation context in its prompt. Making conversation files agent-readable is a future enhancement.

#### 6d. `WorkspaceFileService` (Dashboard)
- **Add**: A new virtual tree root `conversations/` that resolves to `data/chats/{chat_jid}/` — scoped to chats belonging to the selected group (via `chat-groups.json` mapping).
- **Keep**: Existing `groups/`, `workspace/`, `sessions/` roots unchanged.
- **Security**: Same `ValidateWithinBase` checks applied to the conversations root.
- **Constructor/DI change required**: `WorkspaceFileService` currently only receives `groupsDirectory` and `dataDirectory`. To scope conversations by group, it must also receive either an `IChatGroupResolver` service or `IGroupRepository`. `DashboardServiceExtensions` must be updated to register and inject that dependency.

#### 6e. `DashboardEndpoints`
- **Attachment serving**: No change — continues to serve from `FileAttachment.LocalPath` (which points to `data/files/...`).
- **Agent event DTOs**: No change — `AgentEventDto.Id` continues to map from `AgentActivityEvent.Id`, which is now assigned by the monotonic counter rather than SQLite autoincrement. The DTO structure is unchanged.
- **Task run logs**: No change — `GetRunLogsAsync` continues to accept a `limit` parameter. The file-based implementation reads from `runs.jsonl` tail.

#### 6f. `ServiceCollectionExtensions.cs`
- **Replace** all 7 `Sqlite*Repository` registrations with `File*Repository` registrations
- **Remove** `SqliteConnectionFactory` and `SqliteSchemaInitializer` registrations
- **Add** `FileStoragePaths` registration
- **Add** `IChatGroupResolver` registration if the dashboard/workspace layer uses a dedicated resolver instead of querying `IGroupRepository` directly

#### 6g. Dependency cleanup
- **Delete** all `Persistence/Sqlite/` files
- **Remove** `Microsoft.Data.Sqlite` from `.csproj`

#### 6h. `NetClaw.Setup` CLI
- **Remove**: `SetupPaths.DatabasePath`
- **Remove**: `SetupRunner.EnsureSchemaAsync`, `CreateConnectionFactory`, and SQLite-backed `CreateGroupRepository`
- **Replace**: `DATABASE_EXISTS` / `DATABASE_PATH` status outputs with file-store equivalents such as `STATE_EXISTS`, `GROUPS_FILE_EXISTS`, and `CHATS_DIRECTORY_EXISTS`
- **Update**: `init` step to create the new persistence directories (`data/chats/`, `data/tasks/`, `data/events/`) instead of initializing a schema
- **Update**: `register` step to persist groups through `FileGroupRepository` rather than direct SQLite access
- **Update**: `verify` step to validate file-based store presence and count registered groups from `groups.json`

---

## 7. Mapping: Old vs New

| Component | SQLite (current) | File-based (target) |
|-----------|-------------------|---------------------|
| Connection | `SqliteConnectionFactory` | N/A (direct file I/O) |
| Schema init | `SqliteSchemaInitializer` | Directory creation in `HostInitializationService` |
| Messages | `SqliteMessageRepository` | `FileMessageRepository` + in-memory cache (stores in `data/chats/`) |
| Sessions | `SqliteSessionRepository` | `FileSessionRepository` (stores in `groups/{folder}/session.json`) |
| Groups | `SqliteGroupRepository` | `FileGroupRepository` + in-memory cache + `chat-groups.json` side effect |
| Tasks | `SqliteTaskRepository` | `FileTaskRepository` + in-memory cache (`config.json` + `runs.jsonl` per task) |
| Router state | `SqliteRouterStateRepository` | `FileRouterStateRepository` + in-memory cache |
| Agent events | `SqliteAgentEventRepository` | `FileAgentEventRepository` + monotonic ID counter |
| File attachments | `SqliteFileAttachmentRepository` | `FileAttachmentRepository` + in-memory fileId index |
| Serialization | `SqliteSerialization` (JSON↔string) | Direct `System.Text.Json` |

---

## 8. Risk Mitigation

| Risk | Mitigation |
|------|------------|
| **Cross-chat polling perf** | In-memory timestamp cache makes `GetNewMessagesAsync` O(1) in the common case. Only reads disk for chats with actual new messages. |
| **Concurrent writes** | Per-chat `SemaphoreSlim` locks. Different chats → different files → no contention. Append-only for JSONL. Atomic rename for config files. |
| **Growing JSONL files** | At NetClaw's scale (hundreds/low-thousands of messages per chat), JSONL files stay under 5MB. If needed later: add daily rotation (`messages-2026-03-25.jsonl`) as a future optimization. |
| **Startup time** | Metadata scan on startup is O(number of chats). At 50 chats, this is <100ms. Message-ID dedup set is populated from the tail of each JSONL (last 100 lines). |
| **Crash during JSONL append** | Partial last line detected by JSON parse failure — silently discarded. At most one message lost per crash event. Metadata.json uses atomic rename — never partially written. |
| **File-ID cross-chat lookup** | In-memory index populated on startup. O(total attachments) startup cost, O(1) lookup at runtime. |
| **Unregistered chats** | Stored in `data/chats/` without group association. Silently skipped during polling (matches current behavior). No orphaned files — they're just unrouted. |
| **Agent event ID uniqueness** | Persisted global counter in `data/events/next-id.txt`. On first migration startup, initialize from the max ID across all retained event files. Guarantees uniqueness across restarts and multi-day dashboard queries. |
| **Task run log size** | No cap imposed. `runs.jsonl` grows unbounded. `GetRunLogsAsync(limit)` reads from file tail. If a task accumulates thousands of runs over months, the file stays manageable (each JSON line is ~200 bytes → 1000 runs ≈ 200KB). |

---

## 9. Execution Order and Dependencies

```
Phase 1: Infrastructure Foundation
  └── FileAtomicWriter, JsonlFileReader, JsonlFileAppender, FileStoragePaths
       │
       ├── Phase 2a: FileRouterStateRepository     (no deps)
       ├── Phase 2b: FileSessionRepository          (no deps)
       ├── Phase 2c: FileGroupRepository            (no deps, produces chat-groups.json)
       └── Phase 2d: FileTaskRepository             (no deps)
            │
            ├── Phase 3: FileMessageRepository      (depends on Phase 1, no group knowledge needed)
            │    │
            │    └── Phase 4: FileAttachmentRepository  (lives alongside messages in data/chats/)
            │
            └── Phase 5: FileAgentEventRepository   (depends on Phase 1 only)
                 │
                 └── Phase 6: Non-Repository Components
                      ├── 6a: HostInitializationService (new dirs, remove schema init)
                      ├── 6b: HostPathOptions (remove DatabasePath)
                      ├── 6c: NetClawAgentWorkspaceBuilder (no change for now)
                      ├── 6d: WorkspaceFileService (add conversations/ root + resolver dependency)
                      ├── 6e: DashboardEndpoints (no change — attachment serving path unchanged)
                      ├── 6f: ServiceCollectionExtensions (swap DI registrations)
                      ├── 6g: Dependency cleanup (delete SQLite files, remove package)
                      └── 6h: NetClaw.Setup (remove SQLite setup flow, validate file store)
```

Phases 2a-2d can be done in parallel. Phases 3-5 can be done in parallel after Phase 2. Phase 6 is the final integration step.

---

## 10. Test Strategy

### Unit Tests (per repository)

Each `File*Repository` gets a test class that:
1. Creates a temp directory (`Path.Combine(Path.GetTempPath(), $"netclaw-test-{Guid.NewGuid()}")`)
2. Exercises every method on the interface
3. Verifies file contents on disk match expectations
4. Tests concurrent access (parallel `StoreMessageAsync` calls on the same chat)
5. Tests crash recovery (partial JSONL write, missing metadata file, empty directory)
6. Cleans up temp directory in `Dispose`

### Integration Tests

The existing in-memory fakes (`InMemoryMessageRepository`, etc.) in test classes remain valid since they implement the same interfaces. Application-layer tests don't change.

### Domain round-trip validation

Add a test that serializes and deserializes each domain type through the file format, verifying that the round-trip produces an identical object. Specifically:
- `RegisteredGroup` with `Trigger` (always non-empty, including disabled-trigger sentinel cases), `RequiresTrigger`, `ContainerConfig` (nullable JSON)
- `ScheduledTask` with all schedule types
- `AgentActivityEvent` with persisted counter-assigned `Id`

### Setup validation

Add tests for `NetClaw.Setup` covering:
- `init` creates the file-store directory skeleton without any SQLite schema initialization
- `register` writes `groups.json` and `chat-groups.json`
- `verify` reports file-store health instead of database health

### Validation

Run `dotnet test` after each phase to ensure no regressions. The existing test suite covers the application-layer behavior (message processing, polling, scheduling) through the repository interfaces.

---

## 11. Files to Create

| File | Purpose |
|------|---------|
| `Infrastructure/Persistence/FileSystem/FileAtomicWriter.cs` | Atomic write utility (temp + rename) |
| `Infrastructure/Persistence/FileSystem/JsonlFileReader.cs` | JSONL read + filter utility (tolerant of partial last line) |
| `Infrastructure/Persistence/FileSystem/JsonlFileAppender.cs` | JSONL append utility |
| `Infrastructure/Persistence/FileSystem/FileStoragePaths.cs` | Path resolution for all file locations |
| `Infrastructure/Persistence/FileSystem/PersistentCounter.cs` | Persisted monotonic counter for agent event IDs |
| `Infrastructure/Persistence/FileSystem/FileRouterStateRepository.cs` | Router state persistence |
| `Infrastructure/Persistence/FileSystem/FileSessionRepository.cs` | Session persistence |
| `Infrastructure/Persistence/FileSystem/FileGroupRepository.cs` | Group registration persistence + chat-groups.json |
| `Infrastructure/Persistence/FileSystem/FileTaskRepository.cs` | Task config + run log persistence |
| `Infrastructure/Persistence/FileSystem/FileMessageRepository.cs` | Message + chat metadata persistence |
| `Infrastructure/Persistence/FileSystem/FileAttachmentRepository.cs` | File attachment metadata persistence |
| `Infrastructure/Persistence/FileSystem/FileAgentEventRepository.cs` | Agent event persistence + ID generation |

## 12. Files to Delete

| File | Reason |
|------|--------|
| `Infrastructure/Persistence/Sqlite/SqliteConnectionFactory.cs` | No longer needed |
| `Infrastructure/Persistence/Sqlite/SqliteSchemaInitializer.cs` | No longer needed |
| `Infrastructure/Persistence/Sqlite/SqliteMessageRepository.cs` | Replaced by FileMessageRepository |
| `Infrastructure/Persistence/Sqlite/SqliteSessionRepository.cs` | Replaced by FileSessionRepository |
| `Infrastructure/Persistence/Sqlite/SqliteGroupRepository.cs` | Replaced by FileGroupRepository |
| `Infrastructure/Persistence/Sqlite/SqliteTaskRepository.cs` | Replaced by FileTaskRepository |
| `Infrastructure/Persistence/Sqlite/SqliteRouterStateRepository.cs` | Replaced by FileRouterStateRepository |
| `Infrastructure/Persistence/Sqlite/SqliteAgentEventRepository.cs` | Replaced by FileAgentEventRepository |
| `Infrastructure/Persistence/Sqlite/SqliteFileAttachmentRepository.cs` | Replaced by FileAttachmentRepository |
| `Infrastructure/Persistence/Sqlite/SqliteSerialization.cs` | No longer needed |
| `Infrastructure.Tests/Persistence/Sqlite/*` | Replaced by file-system tests |

## 13. Files to Modify

| File | Change |
|------|--------|
| `Host/DependencyInjection/ServiceCollectionExtensions.cs` | Swap all 7 Sqlite registrations → File registrations; add FileStoragePaths |
| `Host/Services/HostInitializationService.cs` | Remove schema init; add `data/chats/`, `data/tasks/`, `data/events/` directory creation |
| `Host/Configuration/HostPathOptions.cs` | Remove `DatabasePath` property |
| `Infrastructure/Infrastructure.csproj` | Remove `Microsoft.Data.Sqlite` package reference |
| `Infrastructure.Tests/Infrastructure.Tests.csproj` | Remove SQLite test dependencies if any |
| `Dashboard/Services/WorkspaceFileService.cs` | Add `conversations/` virtual tree root resolving to `data/chats/{jid}/` scoped by group; inject resolver dependency |
| `Dashboard/DashboardServiceExtensions.cs` | Register resolver dependency for `WorkspaceFileService` |
| `Setup/SetupRunner.cs` | Remove SQLite schema flow; create and verify file-based store |
| `Setup/SetupPaths.cs` | Remove `DatabasePath`; add any file-store status paths needed by setup |

---

## 14. Estimated Scope

- **~12 new files** (7 repositories + 5 utilities)
- **~11 deleted files** (all SQLite implementations + tests)
- **~10 modified files** (DI wiring, host init, config, dashboard, setup, csproj)
- **Repository interfaces**: Unchanged (0 modifications)
- **Application layer**: Unchanged (0 modifications)
- **Domain layer**: Unchanged (0 modifications)
- **Runtime components**: Minor — workspace builder unchanged for now, dashboard gets one new virtual tree root plus resolver wiring
- **Setup CLI**: Moderate — replaces SQLite-oriented init/register/verify behavior with file-store checks
- **Binary attachment lifecycle**: Unchanged — `data/files/` stays as-is

The repository interfaces remain the stability contract for the application layer. The broader changes (host init, dashboard, path config) are coordinated in Phase 6 but are small and well-scoped.

---

## Appendix: Addressing the Review Feedback

This section maps each critique point to how it was resolved in this revision.

### HIGH: Storage topology conflicts with runtime model

**Critique**: The plan treated this as a repository swap, but `NetClawAgentWorkspaceBuilder`, `GroupPathResolver`, `WorkspaceFileService`, and `DashboardEndpoints` have storage-layout assumptions.

**Resolution**: Reframed as a "storage topology refactor" (§1). Explicit component impact table added (§2). Phase 6 added to cover all non-repository changes. Key decision: the existing `data/` layout for sessions, workspaces, and IPC is preserved — `GroupPathResolver` paths remain valid.

### HIGH: Chat ownership — global vs group-scoped

**Critique**: `IMessageRepository` is chat-global, not group-scoped. Storing messages under group trees creates a bootstrapping problem for unregistered chats.

**Resolution**: Chats are centrally stored in `data/chats/{chat_jid}/` (§3). Explicit `chat-groups.json` mapping file (§4.3). Three-layer ownership design (§5.4) covering unregistered chats, re-assignment, and early metadata. The foundational decision is documented up front (§1, before section 2).

### MEDIUM: Write-safety inconsistency

**Critique**: The plan proposed atomic rename, plain append, and crash safety via atomic rename — three different strategies conflated.

**Resolution**: Explicit two-strategy table (§5.2): atomic temp+rename for small config files vs. append-only for JSONL with accepted partial-line loss. Reader tolerance documented (§4.1). The inconsistency is eliminated — each file type has one clearly stated strategy.

### MEDIUM: Task run history cap at 50

**Critique**: Capping at 50 is a behavior regression — the dashboard exposes up to 500 runs, and the current repository stores logs without truncation.

**Resolution**: No cap. Run logs moved to a separate `runs.jsonl` file (§4.7) to support unbounded append-only growth. Dashboard's `Math.Clamp(limit ?? 50, 1, 500)` continues to work as-is.

### MEDIUM: Attachment binary-file lifecycle

**Critique**: The plan covered metadata placement but not the binary download/staging/serving lifecycle.

**Resolution**: Complete binary lifecycle table added (§4.8) covering all four stages: Slack download → metadata storage → staging to `.uploads/` → dashboard serving. `data/files/` directory is explicitly preserved unchanged. `localPath` continues to point to the canonical binary location.

### MEDIUM: Agent event ID generation

**Critique**: `AgentActivityEvent.Id` is a `long` auto-assigned by SQLite. File-based storage needs an equivalent.

**Resolution**: Persisted global monotonic counter backed by `data/events/next-id.txt` (§4.9, §5.6). On first migration startup, initialize it from the max ID across all retained event files. This preserves dashboard DTO identity assumptions across restarts and multi-day queries.

### LOW: groups.json format vs RegisteredGroup model

**Critique**: The sample used `triggerPattern` with an empty string, but `RegisteredGroup` requires `Trigger` (non-empty via constructor validation).

**Resolution**: Field renamed to `trigger` (§4.5). The sample now uses a non-empty disabled-trigger sentinel, and the domain validation note explicitly states that serialized triggers must always be non-empty to satisfy the current constructor. Domain round-trip test added to test strategy (§10).

### HIGH: Setup CLI still assumes SQLite

**Critique**: The document scope included setup CLI changes, but the execution plan did not cover `SetupRunner`, `SetupPaths`, or SQLite-based status output and schema initialization.

**Resolution**: Phase 6h added for `NetClaw.Setup`, covering removal of `DatabasePath`, removal of schema initialization, replacement of database-health outputs with file-store checks, and migration of `init`, `register`, and `verify` to the file-based persistence model.

### MEDIUM: WorkspaceFileService resolver wiring

**Critique**: Adding a group-scoped `conversations/` root requires more than a new tree node; `WorkspaceFileService` and its DI registration need a way to resolve chat ownership.

**Resolution**: Phase 6d now explicitly calls for a resolver dependency, and Phase 6f plus the modified-files list include the required `DashboardServiceExtensions` registration updates.
