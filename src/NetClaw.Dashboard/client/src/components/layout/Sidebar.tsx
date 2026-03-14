import { NavLink } from "react-router-dom";
import {
  Activity,
  LayoutDashboard,
  MessageSquare,
  Clock,
  Users,
  Heart,
  FolderOpen,
} from "lucide-react";
import type { ConnectionStatus } from "@/hooks/useSignalR";

const navItems = [
  { to: "/", icon: LayoutDashboard, label: "Dashboard" },
  { to: "/activity", icon: Activity, label: "Activity" },
  { to: "/messages", icon: MessageSquare, label: "Messages" },
  { to: "/tasks", icon: Clock, label: "Tasks" },
  { to: "/groups", icon: Users, label: "Groups" },
  { to: "/workspace", icon: FolderOpen, label: "Workspace" },
  { to: "/system", icon: Heart, label: "System" },
];

const statusColors: Record<ConnectionStatus, string> = {
  connected: "bg-emerald-500",
  connecting: "bg-amber-500 animate-pulse",
  reconnecting: "bg-amber-500 animate-pulse",
  disconnected: "bg-red-500",
};

export function Sidebar({ connectionStatus }: { connectionStatus: ConnectionStatus }) {
  return (
    <aside className="w-56 bg-gray-900 border-r border-gray-800 flex flex-col h-screen sticky top-0">
      <div className="px-4 py-4 border-b border-gray-800">
        <h1 className="text-lg font-bold text-white tracking-tight">NetClaw</h1>
        <div className="flex items-center gap-2 mt-1">
          <span className={`w-2 h-2 rounded-full ${statusColors[connectionStatus]}`} />
          <span className="text-xs text-gray-400 capitalize">{connectionStatus}</span>
        </div>
      </div>
      <nav className="flex-1 py-2 overflow-y-auto">
        {navItems.map((item) => (
          <NavLink
            key={item.to}
            to={item.to}
            end={item.to === "/"}
            className={({ isActive }) =>
              `flex items-center gap-3 px-4 py-2 text-sm transition-colors ${
                isActive
                  ? "bg-gray-800 text-white border-r-2 border-blue-500"
                  : "text-gray-400 hover:text-white hover:bg-gray-800/50"
              }`
            }
          >
            <item.icon size={18} />
            {item.label}
          </NavLink>
        ))}
      </nav>
    </aside>
  );
}
