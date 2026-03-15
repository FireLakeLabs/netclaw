import { useRef, useEffect } from "react";
import { useParams, Link } from "react-router-dom";
import { format } from "date-fns";
import { ArrowLeft, Bot, User } from "lucide-react";
import { Spinner, EmptyState, PageHeader } from "@/components/ui/shared";
import { useChatMessages, useChats } from "@/api/client";

const SLACK_USER_ID_RE = /^U[A-Z0-9]{8,}$/;

function resolveSenderName(senderName: string): string {
  if (!SLACK_USER_ID_RE.test(senderName)) return senderName;
  return "User";
}

export function ChatDetailPage() {
  const { jid } = useParams<{ jid: string }>();
  const { data: messages, isLoading } = useChatMessages(jid ?? "");
  const { data: chats } = useChats();
  const chat = chats?.find((c) => c.jid === jid);
  const title = chat?.name || "Chat";
  const bottomRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    bottomRef.current?.scrollIntoView({ behavior: "smooth" });
  }, [messages]);

  return (
    <div>
      <PageHeader title={title}>
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
          {messages.map((msg) => {
            const displayName = msg.isFromMe ? "Agent" : resolveSenderName(msg.senderName);
            const showRawId = !msg.isFromMe && displayName !== msg.sender && !SLACK_USER_ID_RE.test(msg.sender);
            return (
              <div
                key={msg.id}
                className={`p-3 rounded-lg max-w-2xl ${
                  msg.isFromMe
                    ? "bg-blue-900/30 border border-blue-800/50 ml-auto"
                    : "bg-gray-900 border border-gray-800"
                }`}
              >
                <div className="flex items-center gap-2 mb-1">
                  {msg.isFromMe ? (
                    <Bot size={14} className="text-blue-400" />
                  ) : (
                    <User size={14} className="text-gray-400" />
                  )}
                  <span className={`text-xs font-medium ${msg.isFromMe ? "text-blue-300" : "text-gray-300"}`}>
                    {displayName}
                  </span>
                  {showRawId && (
                    <span className="text-xs text-gray-600">{msg.sender}</span>
                  )}
                  <span className="text-xs text-gray-500">
                    {format(new Date(msg.timestamp), "MMM d, HH:mm:ss")}
                  </span>
                </div>
                <p className="text-sm text-gray-200 whitespace-pre-wrap break-words">
                  {msg.content}
                </p>
              </div>
            );
          })}
          <div ref={bottomRef} />
        </div>
      )}
    </div>
  );
}
