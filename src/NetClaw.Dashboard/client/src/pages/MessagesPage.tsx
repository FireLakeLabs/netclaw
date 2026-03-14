import { Link } from "react-router-dom";
import { formatDistanceToNow } from "date-fns";
import { Badge, Spinner, EmptyState, PageHeader } from "@/components/ui/shared";
import { useChats } from "@/api/client";

export function MessagesPage() {
  const { data: chats, isLoading } = useChats();

  return (
    <div>
      <PageHeader title="Messages" />

      {isLoading ? (
        <Spinner />
      ) : !chats || chats.length === 0 ? (
        <EmptyState message="No conversations found" />
      ) : (
        <div className="space-y-2">
          {chats
            .sort((a, b) => new Date(b.lastMessageTime).getTime() - new Date(a.lastMessageTime).getTime())
            .map((chat) => (
              <Link
                key={chat.jid}
                to={`/messages/${encodeURIComponent(chat.jid)}`}
                className="flex items-center justify-between p-3 bg-gray-900 border border-gray-800 rounded-lg hover:bg-gray-800 transition-colors"
              >
                <div className="flex items-center gap-3">
                  <div className="flex flex-col">
                    <span className="text-sm font-medium">{chat.name}</span>
                    <span className="text-xs text-gray-500">{chat.jid}</span>
                  </div>
                </div>
                <div className="flex items-center gap-2">
                  {chat.channel && <Badge>{chat.channel}</Badge>}
                  {chat.isGroup && <Badge variant="info">Group</Badge>}
                  <span className="text-xs text-gray-500">
                    {formatDistanceToNow(new Date(chat.lastMessageTime), { addSuffix: true })}
                  </span>
                </div>
              </Link>
            ))}
        </div>
      )}
    </div>
  );
}
