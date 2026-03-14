import { useState } from "react";
import { formatDistanceToNow, format } from "date-fns";
import { ChevronDown, ChevronRight } from "lucide-react";
import { Badge, Card, Spinner, EmptyState, PageHeader } from "@/components/ui/shared";
import { useRecentActivity } from "@/api/client";
import type { AgentActivityEventDto } from "@/api/types";

type Variant = "default" | "success" | "warning" | "error" | "info";

export function eventKindVariant(kind: string): Variant {
  switch (kind) {
    case "MessageCompleted":
      return "success";
    case "ToolStarted":
    case "ToolCompleted":
      return "info";
    case "Error":
      return "error";
    case "Idle":
      return "default";
    case "ReasoningDelta":
    case "TextDelta":
      return "warning";
    default:
      return "default";
  }
}

export function eventKindLabel(kind: string): string {
  return kind.replace(/([A-Z])/g, " $1").trim();
}

interface CollapsedGroup {
  kind: "delta-group";
  events: AgentActivityEventDto[];
  accumulatedContent: string;
}

type DisplayItem =
  | { kind: "single"; event: AgentActivityEventDto }
  | CollapsedGroup;

function collapseDeltas(events: AgentActivityEventDto[]): DisplayItem[] {
  const items: DisplayItem[] = [];
  let i = 0;
  while (i < events.length) {
    const event = events[i]!;
    if (event.eventKind === "TextDelta") {
      const group: AgentActivityEventDto[] = [event];
      while (
        i + 1 < events.length &&
        events[i + 1]!.eventKind === "TextDelta" &&
        events[i + 1]!.sessionId === event.sessionId &&
        events[i + 1]!.groupFolder === event.groupFolder
      ) {
        i++;
        group.push(events[i]!);
      }
      if (group.length > 1) {
        // Events are newest-first, so reverse for chronological accumulation
        const chronological = [...group].reverse();
        const accumulated = chronological
          .map((e) => e.content ?? "")
          .join("");
        items.push({ kind: "delta-group", events: group, accumulatedContent: accumulated });
      } else {
        items.push({ kind: "single", event });
      }
    } else {
      items.push({ kind: "single", event });
    }
    i++;
  }
  return items;
}

interface ActivityPageProps {
  recentEvents: AgentActivityEventDto[];
}

export function ActivityPage({ recentEvents }: ActivityPageProps) {
  const historicalActivity = useRecentActivity(200);
  const events = recentEvents.length > 0 ? recentEvents : historicalActivity.data ?? [];
  const displayItems = collapseDeltas(events);

  return (
    <div>
      <PageHeader title="Activity Stream">
        <span className="text-sm text-gray-400">{events.length} events</span>
      </PageHeader>

      {historicalActivity.isLoading && events.length === 0 ? (
        <Spinner />
      ) : events.length === 0 ? (
        <EmptyState message="No activity recorded yet" />
      ) : (
        <div className="space-y-2">
          {displayItems.map((item, idx) =>
            item.kind === "single" ? (
              <EventCard key={item.event.id} event={item.event} />
            ) : (
              <DeltaGroupCard key={`delta-${item.events[0]!.id}`} group={item} index={idx} />
            )
          )}
        </div>
      )}
    </div>
  );
}

function DeltaGroupCard({ group }: { group: CollapsedGroup; index: number }) {
  const [expanded, setExpanded] = useState(false);
  const first = group.events[0]!;
  return (
    <Card>
      <div className="flex items-start gap-3">
        <div className="shrink-0 pt-0.5">
          <Badge variant="warning">Text Delta</Badge>
        </div>
        <div className="flex-1 min-w-0">
          <div className="flex items-center gap-2 text-xs text-gray-400 mb-1">
            <span>{first.groupFolder}</span>
            <span>·</span>
            <span title={format(new Date(first.observedAt), "PPpp")}>
              {formatDistanceToNow(new Date(first.observedAt), { addSuffix: true })}
            </span>
            <span>·</span>
            <span className="text-gray-500">{group.events.length} deltas</span>
            {first.sessionId && (
              <>
                <span>·</span>
                <span className="text-gray-500">session: {first.sessionId.slice(0, 8)}</span>
              </>
            )}
          </div>
          <p className="text-sm text-gray-300 whitespace-pre-wrap break-words">
            {group.accumulatedContent}
          </p>
          <button
            onClick={() => setExpanded(!expanded)}
            className="flex items-center gap-1 mt-2 text-xs text-gray-500 hover:text-gray-300"
          >
            {expanded ? <ChevronDown size={14} /> : <ChevronRight size={14} />}
            {expanded ? "Hide" : "Show"} individual deltas
          </button>
          {expanded && (
            <div className="mt-2 space-y-1 pl-2 border-l border-gray-700">
              {group.events.map((event) => (
                <div key={event.id} className="text-xs text-gray-500 py-0.5">
                  <span className="text-gray-600 mr-2">
                    {format(new Date(event.observedAt), "HH:mm:ss.SSS")}
                  </span>
                  {event.content}
                </div>
              ))}
            </div>
          )}
        </div>
      </div>
    </Card>
  );
}

function EventCard({ event }: { event: AgentActivityEventDto }) {
  return (
    <Card>
      <div className="flex items-start gap-3">
        <div className="shrink-0 pt-0.5">
          <Badge variant={eventKindVariant(event.eventKind)}>
            {eventKindLabel(event.eventKind)}
          </Badge>
        </div>
        <div className="flex-1 min-w-0">
          <div className="flex items-center gap-2 text-xs text-gray-400 mb-1">
            <span>{event.groupFolder}</span>
            <span>·</span>
            <span title={format(new Date(event.observedAt), "PPpp")}>
              {formatDistanceToNow(new Date(event.observedAt), { addSuffix: true })}
            </span>
            {event.sessionId && (
              <>
                <span>·</span>
                <span className="text-gray-500">session: {event.sessionId.slice(0, 8)}</span>
              </>
            )}
          </div>
          {event.toolName && (
            <p className="text-sm text-blue-300 mb-1">Tool: {event.toolName}</p>
          )}
          {event.content && (
            <p className="text-sm text-gray-300 whitespace-pre-wrap break-words">
              {event.content}
            </p>
          )}
          {event.error && (
            <p className="text-sm text-red-300 mt-1">{event.error}</p>
          )}
          {event.isScheduledTask && event.taskId && (
            <Badge variant="info" className="mt-1">Task: {event.taskId}</Badge>
          )}
        </div>
      </div>
    </Card>
  );
}
