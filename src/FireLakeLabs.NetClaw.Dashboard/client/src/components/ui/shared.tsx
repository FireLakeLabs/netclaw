import type { ReactNode } from "react";

interface BadgeProps {
  children: ReactNode;
  variant?: "default" | "success" | "warning" | "error" | "info";
  className?: string;
}

const variantClasses = {
  default: "bg-gray-700 text-gray-200",
  success: "bg-emerald-900/50 text-emerald-300 border border-emerald-700/50",
  warning: "bg-amber-900/50 text-amber-300 border border-amber-700/50",
  error: "bg-red-900/50 text-red-300 border border-red-700/50",
  info: "bg-blue-900/50 text-blue-300 border border-blue-700/50",
};

export function Badge({ children, variant = "default", className = "" }: BadgeProps) {
  return (
    <span
      className={`inline-flex items-center px-2 py-0.5 rounded text-xs font-medium ${variantClasses[variant]} ${className}`}
    >
      {children}
    </span>
  );
}

interface CardProps {
  children: ReactNode;
  className?: string;
  title?: string;
  action?: ReactNode;
}

export function Card({ children, className = "", title, action }: CardProps) {
  return (
    <div className={`bg-gray-900 border border-gray-800 rounded-lg ${className}`}>
      {title && (
        <div className="flex items-center justify-between px-4 py-3 border-b border-gray-800">
          <h3 className="text-sm font-medium text-gray-300">{title}</h3>
          {action}
        </div>
      )}
      <div className="p-4">{children}</div>
    </div>
  );
}

export function Spinner({ className = "" }: { className?: string }) {
  return (
    <div className={`flex items-center justify-center p-8 ${className}`}>
      <div className="w-6 h-6 border-2 border-gray-600 border-t-blue-500 rounded-full animate-spin" />
    </div>
  );
}

export function ErrorMessage({ message }: { message: string }) {
  return (
    <div className="p-4 bg-red-900/20 border border-red-800 rounded-lg text-red-300 text-sm">
      {message}
    </div>
  );
}

export function EmptyState({ message }: { message: string }) {
  return (
    <div className="p-8 text-center text-gray-500 text-sm">{message}</div>
  );
}

export function PageHeader({ title, children }: { title: string; children?: React.ReactNode }) {
  return (
    <div className="flex items-center justify-between mb-6">
      <h2 className="text-xl font-semibold text-white">{title}</h2>
      {children}
    </div>
  );
}
