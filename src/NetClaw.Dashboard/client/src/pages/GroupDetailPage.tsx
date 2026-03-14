import { useParams, Link } from "react-router-dom";
import { format } from "date-fns";
import { ArrowLeft, FolderOpen } from "lucide-react";
import { Card, Badge, Spinner, EmptyState, PageHeader } from "@/components/ui/shared";
import { useGroup } from "@/api/client";

export function GroupDetailPage() {
  const { jid } = useParams<{ jid: string }>();
  const { data: group, isLoading } = useGroup(jid ?? "");

  if (isLoading) return <Spinner />;
  if (!group) return <EmptyState message="Group not found" />;

  return (
    <div>
      <PageHeader title={group.name}>
        <Link to="/groups" className="flex items-center gap-1 text-sm text-gray-400 hover:text-white">
          <ArrowLeft size={16} />
          Back
        </Link>
      </PageHeader>

      <Card title="Details" className="mb-4">
        <dl className="grid grid-cols-2 gap-x-8 gap-y-2 text-sm">
          <dt className="text-gray-400">JID</dt>
          <dd>{group.jid}</dd>
          <dt className="text-gray-400">Folder</dt>
          <dd>{group.folder}</dd>
          <dt className="text-gray-400">Trigger</dt>
          <dd>{group.trigger}</dd>
          <dt className="text-gray-400">Registered</dt>
          <dd>{format(new Date(group.addedAt), "PPpp")}</dd>
          <dt className="text-gray-400">Session</dt>
          <dd>
            {group.sessionId ? (
              <Badge variant="success">{group.sessionId}</Badge>
            ) : (
              <span className="text-gray-500">None</span>
            )}
          </dd>
        </dl>
      </Card>

      <div className="flex gap-3">
        <Link
          to={`/workspace/${encodeURIComponent(group.folder)}`}
          className="flex items-center gap-2 px-3 py-2 bg-gray-800 rounded-lg text-sm text-gray-300 hover:text-white hover:bg-gray-700 transition-colors"
        >
          <FolderOpen size={16} />
          Browse Workspace
        </Link>
        <Link
          to={`/messages/${encodeURIComponent(group.jid)}`}
          className="flex items-center gap-2 px-3 py-2 bg-gray-800 rounded-lg text-sm text-gray-300 hover:text-white hover:bg-gray-700 transition-colors"
        >
          View Messages
        </Link>
      </div>
    </div>
  );
}
