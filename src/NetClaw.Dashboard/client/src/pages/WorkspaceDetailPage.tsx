import { useState } from "react";
import { useParams, Link } from "react-router-dom";
import { ArrowLeft, Folder, FileText, ChevronRight, ChevronDown } from "lucide-react";
import { Card, Spinner, EmptyState, PageHeader } from "@/components/ui/shared";
import { ImageLightbox } from "@/components/ui/ImageLightbox";
import { useWorkspaceTree, useWorkspaceFile } from "@/api/client";
import { usePageState } from "@/hooks/usePageState";
import type { WorkspaceTreeEntryDto } from "@/api/types";

const IMAGE_EXTENSIONS = new Set([".png", ".jpg", ".jpeg", ".gif", ".webp", ".svg", ".bmp", ".ico"]);

function isImageFile(path: string): boolean {
  const ext = path.substring(path.lastIndexOf(".")).toLowerCase();
  return IMAGE_EXTENSIONS.has(ext);
}

function rawFileUrl(folder: string, path: string): string {
  return `/api/workspace/${encodeURIComponent(folder)}/raw?path=${encodeURIComponent(path)}`;
}

export function WorkspaceDetailPage() {
  const { folder } = useParams<{ folder: string }>();
  const { data: tree, isLoading } = useWorkspaceTree(folder ?? "");
  const [selectedFile, setSelectedFile] = usePageState<string | null>(`workspace:${folder}:selectedFile`, null);
  const isImage = selectedFile ? isImageFile(selectedFile) : false;
  const file = useWorkspaceFile(folder ?? "", selectedFile && !isImage ? selectedFile : "");
  const [lightboxSrc, setLightboxSrc] = useState<string | null>(null);

  return (
    <div>
      <PageHeader title={`Workspace: ${folder}`}>
        <Link to="/workspace" className="flex items-center gap-1 text-sm text-gray-400 hover:text-white">
          <ArrowLeft size={16} />
          Back
        </Link>
      </PageHeader>

      <div className="grid grid-cols-3 gap-4">
        <Card title="Files" className="col-span-1">
          {isLoading ? (
            <Spinner />
          ) : !tree || tree.length === 0 ? (
            <EmptyState message="Empty workspace" />
          ) : (
            <div className="text-sm max-h-[70vh] overflow-y-auto">
              {tree.map((entry) => (
                <TreeNode
                  key={entry.relativePath || entry.name}
                  entry={entry}
                  path={entry.relativePath || entry.name}
                  selectedFile={selectedFile}
                  onSelect={setSelectedFile}
                  folder={folder ?? ""}
                />
              ))}
            </div>
          )}
        </Card>

        <Card title={selectedFile ?? "Select a file"} className="col-span-2">
          {!selectedFile ? (
            <EmptyState message="Select a file from the tree to view its contents" />
          ) : isImage ? (
            <div>
              <img
                src={rawFileUrl(folder ?? "", selectedFile)}
                alt={selectedFile}
                className="max-w-full max-h-[60vh] rounded cursor-pointer"
                onClick={() => setLightboxSrc(rawFileUrl(folder ?? "", selectedFile))}
              />
            </div>
          ) : file.isLoading ? (
            <Spinner />
          ) : file.error ? (
            <EmptyState message="Could not load file" />
          ) : file.data ? (
            <div>
              <div className="flex items-center gap-4 text-xs text-gray-500 mb-3">
                <span>{((file.data.sizeBytes ?? 0) / 1024).toFixed(1)} KB</span>
                <span>Modified: {new Date(file.data.lastModified).toLocaleString()}</span>
              </div>
              <pre className="text-sm text-gray-300 whitespace-pre-wrap break-words bg-gray-950 p-3 rounded max-h-[60vh] overflow-auto">
                {file.data.content}
              </pre>
            </div>
          ) : null}
        </Card>
      </div>

      {lightboxSrc && (
        <ImageLightbox
          src={lightboxSrc}
          alt={selectedFile ?? "Image"}
          fileName={selectedFile?.split("/").pop() ?? "image"}
          onClose={() => setLightboxSrc(null)}
        />
      )}
    </div>
  );
}

function TreeNode({
  entry,
  path,
  selectedFile,
  onSelect,
  folder,
  depth = 0,
}: {
  entry: WorkspaceTreeEntryDto;
  path: string;
  selectedFile: string | null;
  onSelect: (path: string) => void;
  folder: string;
  depth?: number;
}) {
  const [expanded, setExpanded] = usePageState(`workspace:${folder}:tree:${path}`, depth < 1);

  if (entry.isDirectory) {
    return (
      <div>
        <button
          onClick={() => setExpanded(!expanded)}
          className="flex items-center gap-1 py-1 w-full text-left text-gray-400 hover:text-white"
          style={{ paddingLeft: depth * 16 }}
        >
          {expanded ? <ChevronDown size={14} /> : <ChevronRight size={14} />}
          <Folder size={14} className="text-blue-400" />
          <span className="ml-1">{entry.name}</span>
        </button>
        {expanded && entry.children?.map((child) => (
          <TreeNode
            key={child.relativePath}
            entry={child}
            path={child.relativePath}
            selectedFile={selectedFile}
            onSelect={onSelect}
            folder={folder}
            depth={depth + 1}
          />
        ))}
      </div>
    );
  }

  const isSelected = selectedFile === path;
  return (
    <button
      onClick={() => onSelect(path)}
      className={`flex items-center gap-1 py-1 w-full text-left ${
        isSelected ? "text-blue-400" : "text-gray-400 hover:text-white"
      }`}
      style={{ paddingLeft: depth * 16 + 18 }}
    >
      <FileText size={14} />
      <span className="ml-1 truncate">{entry.name}</span>
    </button>
  );
}
