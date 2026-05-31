import { useState } from "react";
import styles from "./ToolCallCard.module.css";

interface ToolCallCardProps {
  event: string;
  data: unknown;
}

const EVENT_LABELS: Record<string, { label: string; color: string }> = {
  "tool:start": { label: "Tool call", color: "blue" },
  "tool:result": { label: "Result", color: "green" },
  "tool:error": { label: "Error", color: "red" },
  "agent:status": { label: "Agent", color: "purple" },
};

export function ToolCallCard({ event, data }: ToolCallCardProps) {
  const [expanded, setExpanded] = useState(false);
  const meta = EVENT_LABELS[event] ?? { label: event, color: "default" };

  const action = (data as { action?: { type?: string } })?.action;
  const summary =
    event === "tool:start"
      ? `${(data as { type?: string })?.type ?? action?.type ?? event}`
      : event === "agent:status"
        ? `Phase: ${(data as { phase?: string })?.phase ?? "unknown"}`
        : meta.label;

  return (
    <div className={[styles.card, styles[`color-${meta.color}`]].join(" ")}>
      <button className={styles.toggle} onClick={() => setExpanded((v) => !v)}>
        <span className={styles.dot} />
        <span className={styles.label}>{meta.label}</span>
        <span className={styles.summary}>{summary}</span>
        <span className={styles.chevron}>{expanded ? "▲" : "▼"}</span>
      </button>
      {expanded && (
        <pre className={styles.detail}>{JSON.stringify(data, null, 2)}</pre>
      )}
    </div>
  );
}
