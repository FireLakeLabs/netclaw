import { Link } from "react-router-dom";
import { formatDistanceToNow } from "date-fns";
import { Badge, Card, Spinner, EmptyState, PageHeader } from "@/components/ui/shared";
import { useGroups } from "@/api/client";

export function GroupsPage() {
  const { data: groups, isLoading } = useGroups();

  return (
    <div>
      <PageHeader title="Groups" />

      {isLoading ? (
        <Spinner />
      ) : !groups || groups.length === 0 ? (
        <EmptyState message="No groups registered" />
      ) : (
        <div className="grid grid-cols-2 gap-4">
          {groups.map((group) => (
            <Link key={group.jid} to={`/groups/${encodeURIComponent(group.jid)}`}>
              <Card className="hover:border-gray-700 transition-colors">
                <div className="flex items-start justify-between">
                  <div>
                    <p className="font-medium text-white">{group.name}</p>
                    <p className="text-xs text-gray-500 mt-0.5">{group.jid}</p>
                  </div>
                  {group.sessionId && <Badge variant="success">Active Session</Badge>}
                </div>
                <div className="mt-3 flex items-center gap-4 text-xs text-gray-400">
                  <span>Folder: {group.folder}</span>
                  <span>Trigger: {group.trigger}</span>
                  <span>
                    Registered{" "}
                    {formatDistanceToNow(new Date(group.addedAt), { addSuffix: true })}
                  </span>
                </div>
              </Card>
            </Link>
          ))}
        </div>
      )}
    </div>
  );
}
