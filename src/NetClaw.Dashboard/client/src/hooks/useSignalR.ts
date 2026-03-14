import { useEffect, useRef, useState, useCallback } from "react";
import {
  HubConnectionBuilder,
  HubConnection,
  HubConnectionState,
  LogLevel,
} from "@microsoft/signalr";
import type {
  AgentActivityEventDto,
  QueueStateDto,
  WorkerHeartbeatDto,
} from "@/api/types";

export type ConnectionStatus = "connecting" | "connected" | "reconnecting" | "disconnected";

interface SignalRCallbacks {
  onAgentEvent?: (event: AgentActivityEventDto) => void;
  onQueueStateChanged?: (state: QueueStateDto) => void;
  onWorkerHeartbeat?: (heartbeat: WorkerHeartbeatDto) => void;
}

export function useSignalR(callbacks: SignalRCallbacks) {
  const [status, setStatus] = useState<ConnectionStatus>("disconnected");
  const connectionRef = useRef<HubConnection | null>(null);
  const callbacksRef = useRef(callbacks);
  callbacksRef.current = callbacks;

  useEffect(() => {
    const connection = new HubConnectionBuilder()
      .withUrl("/hubs/dashboard")
      .withAutomaticReconnect([0, 1000, 2000, 5000, 10000, 30000])
      .configureLogging(LogLevel.Warning)
      .build();

    connectionRef.current = connection;

    connection.on("OnAgentEvent", (event: AgentActivityEventDto) => {
      callbacksRef.current.onAgentEvent?.(event);
    });

    connection.on("OnQueueStateChanged", (state: QueueStateDto) => {
      callbacksRef.current.onQueueStateChanged?.(state);
    });

    connection.on("OnWorkerHeartbeat", (heartbeat: WorkerHeartbeatDto) => {
      callbacksRef.current.onWorkerHeartbeat?.(heartbeat);
    });

    connection.onreconnecting(() => setStatus("reconnecting"));
    connection.onreconnected(() => setStatus("connected"));
    connection.onclose(() => setStatus("disconnected"));

    setStatus("connecting");
    connection
      .start()
      .then(() => setStatus("connected"))
      .catch(() => setStatus("disconnected"));

    return () => {
      connection.stop();
    };
  }, []);

  const subscribeToGroup = useCallback((groupFolder: string) => {
    connectionRef.current?.invoke("SubscribeToGroup", groupFolder);
  }, []);

  const unsubscribeFromGroup = useCallback((groupFolder: string) => {
    connectionRef.current?.invoke("UnsubscribeFromGroup", groupFolder);
  }, []);

  return {
    status,
    isConnected: status === "connected",
    state: connectionRef.current?.state ?? HubConnectionState.Disconnected,
    subscribeToGroup,
    unsubscribeFromGroup,
  };
}
