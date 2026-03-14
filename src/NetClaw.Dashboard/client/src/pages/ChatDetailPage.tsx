import { useParams, Link } from "react-router-dom";
import { format } from "date-fns";
import { ArrowLeft } from "lucide-react";
import { Spinner, EmptyState, PageHeader } from "@/components/ui/shared";
import { useChatMessages } from "@/api/client";

export function ChatDetailPage() {
  const { jid } = useParams<{ jid: string }>();
  const { data: messages, isLoading } = useChatMessages(jid ?? "");

  return (
    <div>
      <PageHeader title="Chat">
        <Link to="/messages" className="flex items-center gap-1 text-sm text-gray-400 hover:text-white">
          <ArrowLeft size={16} />
          Back
        </Link>
      </PageHeader>

      <p className="text-xs text-gray-500 mb-4 -mt-4">{jid}</p>

      {isLoading ? (
        <Spinner />
      ) : !messages || messages.length === 0 ? (
        <EmptyState message="No messages" />
      ) : (
        <div className="space-y-3">
          {messages.map((msg) => (
            <div
              key={msg.id}
              className={`p-3 rounded-lg max-w-2xl ${
                msg.isFromMe
                  ? "bg-blue-900/30 border border-blue-800/50 ml-auto"
                  : "bg-gray-900 border border-gray-800"
              }`}
            >
              <div className="flex items-center gap-2 mb-1">
                <span className="text-xs font-medium text-gray-300">
                  {msg.isFromMe ? "Agent" : msg.senderName}
                </span>
                <span className="text-xs text-gray-500">
                  {format(new Date(msg.timestamp), "MMM d, HH:mm:ss")}
                </span>
              </div>
              <p className="text-sm text-gray-200 whitespace-pre-wrap break-words">
                {msg.content}
              </p>
            </div>
          ))}
        </div>
      )}
    </div>
  );
}
