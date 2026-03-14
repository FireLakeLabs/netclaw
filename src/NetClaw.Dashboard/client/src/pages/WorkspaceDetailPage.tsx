import { useState } from "react";
import { useParams, Link } from "react-router-dom";
import { ArrowLeft, Folder, FileText, ChevronRight, ChevronDown } from "lucide-react";
import { Card, Spinner, EmptyState, PageHeader } from "@/components/ui/shared";
import { useWorkspaceTree, useWorkspaceFile } from "@/api/client";
import type { WorkspaceTreeEntryDto } from "@/api/types";

export function WorkspaceDetailPage() {
  const { folder } = useParams<{ folder: string }>();
  const { data: tree, isLoading } = useWorkspaceTree(folder ?? "");
  const [selectedFile, setSelectedFile] = useState<string | null>(null);
  const file = useWorkspaceFile(folder ?? "", selectedFile ?? "");

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
                />
              ))}
            </div>
          )}
        </Card>

        <Card title={selectedFile ?? "Select a file"} className="col-span-2">
          {!selectedFile ? (
            <EmptyState message="Select a file from the tree to view its contents" />
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
    </div>
  );
}

function TreeNode({
  entry,
  path,
  selectedFile,
  onSelect,
  depth = 0,
}: {
  entry: WorkspaceTreeEntryDto;
  path: string;
  selectedFile: string | null;
  onSelect: (path: string) => void;
  depth?: number;
}) {
  const [expanded, setExpanded] = useState(depth < 1);

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
