import { useRef, useEffect, useState } from "react";
import { useParams, Link } from "react-router-dom";
import { format } from "date-fns";
import { ArrowLeft, Bot, User, Paperclip } from "lucide-react";
import { Spinner, EmptyState, PageHeader } from "@/components/ui/shared";
import { ImageLightbox } from "@/components/ui/ImageLightbox";
import { useChatMessages, useChats } from "@/api/client";
import type { FileAttachmentDto } from "@/api/types";

const SLACK_USER_ID_RE = /^U[A-Z0-9]{8,}$/;

function resolveSenderName(senderName: string): string {
  if (!SLACK_USER_ID_RE.test(senderName)) return senderName;
  return "User";
}

function formatFileSize(bytes: number): string {
  if (bytes < 1024) return `${bytes} B`;
  if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`;
  return `${(bytes / (1024 * 1024)).toFixed(1)} MB`;
}

export function ChatDetailPage() {
  const { jid } = useParams<{ jid: string }>();
  const { data: messages, isLoading } = useChatMessages(jid ?? "");
  const { data: chats } = useChats();
  const chat = chats?.find((c) => c.jid === jid);
  const title = chat?.name || "Chat";
  const bottomRef = useRef<HTMLDivElement>(null);
  const [lightbox, setLightbox] = useState<{ src: string; alt: string; fileName: string } | null>(null);

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
                {msg.attachments && msg.attachments.length > 0 && (
                  <div className="mt-2 space-y-2">
                    {msg.attachments.map((file: FileAttachmentDto) => {
                      const isImage = file.mimeType?.startsWith("image/") ?? false;
                      const fileUrl = `/api/files/${encodeURIComponent(file.fileId)}`;
                      return (
                        <div key={file.fileId}>
                          {isImage ? (
                            <button
                              type="button"
                              onClick={() => setLightbox({ src: fileUrl, alt: file.fileName, fileName: file.fileName })}
                              className="cursor-pointer"
                            >
                              <img
                                src={fileUrl}
                                alt={file.fileName}
                                className="max-w-xs max-h-48 rounded border border-gray-700 hover:border-gray-500 transition-colors"
                              />
                            </button>
                          ) : (
                            <a
                              href={`${fileUrl}?download=true`}
                              download={file.fileName}
                              className="inline-flex items-center gap-1.5 text-xs text-blue-400 hover:text-blue-300"
                            >
                              <Paperclip size={12} />
                              {file.fileName}
                              <span className="text-gray-500">
                                ({formatFileSize(file.fileSizeBytes)})
                              </span>
                            </a>
                          )}
                        </div>
                      );
                    })}
                  </div>
                )}
              </div>
            );
          })}
          <div ref={bottomRef} />
        </div>
      )}

      {lightbox && (
        <ImageLightbox
          src={lightbox.src}
          alt={lightbox.alt}
          fileName={lightbox.fileName}
          onClose={() => setLightbox(null)}
        />
      )}
    </div>
  );
}
