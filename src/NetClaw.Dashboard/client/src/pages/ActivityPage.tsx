import { formatDistanceToNow, format } from "date-fns";
import { Badge, Card, Spinner, EmptyState, PageHeader } from "@/components/ui/shared";
import { useRecentActivity } from "@/api/client";
import type { AgentActivityEventDto } from "@/api/types";

type Variant = "default" | "success" | "warning" | "error" | "info";

export function eventKindVariant(kind: string): Variant {
  switch (kind) {
    case "MessageCompleted":
      return "success";
    case "ToolCallStarted":
    case "ToolCallCompleted":
      return "info";
    case "Error":
      return "error";
    case "Idle":
      return "default";
    case "Thinking":
    case "MessageDelta":
      return "warning";
    default:
      return "default";
  }
}

export function eventKindLabel(kind: string): string {
  return kind.replace(/([A-Z])/g, " $1").trim();
}

interface ActivityPageProps {
  recentEvents: AgentActivityEventDto[];
}

export function ActivityPage({ recentEvents }: ActivityPageProps) {
  const historicalActivity = useRecentActivity(200);
  const events = recentEvents.length > 0 ? recentEvents : historicalActivity.data ?? [];

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
          {events.map((event) => (
            <EventCard key={event.id} event={event} />
          ))}
        </div>
      )}
    </div>
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
