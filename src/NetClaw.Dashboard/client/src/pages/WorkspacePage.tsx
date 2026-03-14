import { Link } from "react-router-dom";
import { FolderOpen } from "lucide-react";
import { Spinner, EmptyState, PageHeader } from "@/components/ui/shared";
import { useGroups } from "@/api/client";

export function WorkspacePage() {
  const { data: groups, isLoading } = useGroups();

  return (
    <div>
      <PageHeader title="Workspace Browser" />

      {isLoading ? (
        <Spinner />
      ) : !groups || groups.length === 0 ? (
        <EmptyState message="No groups — no workspaces to browse" />
      ) : (
        <div className="grid grid-cols-3 gap-4">
          {groups.map((group) => (
            <Link
              key={group.jid}
              to={`/workspace/${encodeURIComponent(group.folder)}`}
              className="flex items-center gap-3 p-4 bg-gray-900 border border-gray-800 rounded-lg hover:bg-gray-800 transition-colors"
            >
              <FolderOpen size={24} className="text-blue-400 shrink-0" />
              <div>
                <p className="text-sm font-medium">{group.name}</p>
                <p className="text-xs text-gray-500">{group.folder}</p>
              </div>
            </Link>
          ))}
        </div>
      )}
    </div>
  );
}
