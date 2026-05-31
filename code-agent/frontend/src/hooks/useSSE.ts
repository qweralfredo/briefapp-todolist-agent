import { useEffect, useRef, useCallback } from "react";

type SSEHandler = (event: string, data: unknown) => void;

export function useSSE(sessionId: string | null, onEvent: SSEHandler) {
  const esRef = useRef<EventSource | null>(null);
  const onEventRef = useRef(onEvent);
  onEventRef.current = onEvent;

  const connect = useCallback(() => {
    if (!sessionId) return;

    const es = new EventSource(`/api/sessions/${sessionId}/events`);
    esRef.current = es;

    const handleMessage = (eventName: string) => (e: MessageEvent) => {
      try {
        const data = JSON.parse(e.data);
        onEventRef.current(eventName, data);
      } catch {
        onEventRef.current(eventName, e.data);
      }
    };

    const events = [
      "ready",
      "session:update",
      "agent:status",
      "agent:error",
      "tool:start",
      "tool:result",
      "tool:error",
      "assistant:message",
    ];

    for (const ev of events) {
      es.addEventListener(ev, handleMessage(ev) as EventListener);
    }

    es.onerror = () => {
      es.close();
      // Auto-reconnect after 3 seconds
      setTimeout(() => {
        if (esRef.current === es) {
          connect();
        }
      }, 3000);
    };

    return es;
  }, [sessionId]);

  useEffect(() => {
    const es = connect();
    return () => {
      es?.close();
      esRef.current = null;
    };
  }, [connect]);
}
