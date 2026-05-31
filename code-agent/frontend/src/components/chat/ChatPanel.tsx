import { useRef, useEffect, useState } from "react";
import type { Session } from "../../types";
import { MarkdownRenderer } from "./MarkdownRenderer";
import { ToolCallCard } from "./ToolCallCard";
import { Spinner } from "../ui/Spinner";
import { api } from "../../api";
import styles from "./ChatPanel.module.css";

interface AgentEvent {
  id: string;
  event: string;
  data: unknown;
}

interface ChatPanelProps {
  session: Session;
  onSessionUpdate: (session: Session) => void;
}

export function ChatPanel({ session, onSessionUpdate }: ChatPanelProps) {
  const [prompt, setPrompt] = useState("");
  const [agentEvents, setAgentEvents] = useState<AgentEvent[]>([]);
  const [thinking, setThinking] = useState(false);
  const [error, setError] = useState("");
  const messagesEndRef = useRef<HTMLDivElement>(null);
  const textareaRef = useRef<HTMLTextAreaElement>(null);

  useEffect(() => {
    messagesEndRef.current?.scrollIntoView({ behavior: "smooth" });
  }, [session.messages, agentEvents, thinking]);

  function handleAgentEvent(event: string, data: unknown) {
    const id = `${event}-${Date.now()}-${Math.random()}`;
    if (event === "session:update") {
      onSessionUpdate(data as Session);
      return;
    }
    if (event === "assistant:message") {
      setThinking(false);
      // Keep non-error events cleared, but preserve agent:error cards
      setAgentEvents((prev) => prev.filter((ev) => ev.event === "agent:error"));
      const updated = (data as { session?: Session })?.session;
      if (updated) onSessionUpdate(updated);
      return;
    }
    if (event === "agent:status") {
      setThinking(true);
    }
    setAgentEvents((prev) => [...prev, { id, event, data }]);
  }

  async function sendMessage() {
    const text = prompt.trim();
    if (!text || thinking) return;

    setPrompt("");
    setError("");
    setThinking(true);
    setAgentEvents([]);

    // Optimistically add user message
    onSessionUpdate({
      ...session,
      messages: [
        ...session.messages,
        { role: "user", content: text, createdAt: new Date().toISOString() },
      ],
    });

    try {
      const updated = await api.sessions.sendMessage(session.id, { prompt: text });
      onSessionUpdate(updated);
    } catch (err) {
      setError(err instanceof Error ? err.message : "Failed to send message.");
    } finally {
      setThinking(false);
      setAgentEvents([]);
    }
  }

  function handleKeyDown(e: React.KeyboardEvent<HTMLTextAreaElement>) {
    if (e.key === "Enter" && (e.metaKey || e.ctrlKey)) {
      e.preventDefault();
      sendMessage();
    }
  }

  // Auto-resize textarea
  function handleTextareaChange(e: React.ChangeEvent<HTMLTextAreaElement>) {
    setPrompt(e.target.value);
    const el = e.target;
    el.style.height = "auto";
    el.style.height = Math.min(el.scrollHeight, 200) + "px";
  }

  return (
    <div className={styles.panel}>
      {/* Messages */}
      <div className={styles.messages}>
        {session.messages.length === 0 && !thinking && (
          <div className={styles.empty}>
            <div className={styles.emptyIcon}>⬡</div>
            <p>Send a message to start coding</p>
            <p className={styles.emptyHint}>The agent will inspect your workspace and take action</p>
          </div>
        )}

        {session.messages.map((msg, i) => (
          <div key={i} className={[styles.message, styles[msg.role]].join(" ")}>
            <div className={styles.messageAvatar}>
              {msg.role === "user" ? "U" : "A"}
            </div>
            <div className={styles.messageContent}>
              {msg.role === "user" ? (
                <p>{msg.content}</p>
              ) : (
                <MarkdownRenderer content={msg.content} />
              )}
            </div>
          </div>
        ))}

        {/* Live agent events */}
        {agentEvents.length > 0 && (
          <div className={styles.events}>
            {agentEvents.map((ev) => (
              <ToolCallCard key={ev.id} event={ev.event} data={ev.data} />
            ))}
          </div>
        )}

        {thinking && (
          <div className={styles.thinking}>
            <Spinner size="sm" />
            <span>Agent is thinking…</span>
          </div>
        )}

        {error && <div className={styles.error}>{error}</div>}

        <div ref={messagesEndRef} />
      </div>

      {/* Prompt composer */}
      <div className={styles.composer}>
        <textarea
          ref={textareaRef}
          className={styles.textarea}
          value={prompt}
          onChange={handleTextareaChange}
          onKeyDown={handleKeyDown}
          placeholder="Ask the agent to write code, fix bugs, or explore the workspace…"
          rows={1}
          disabled={thinking}
        />
        <button
          className={styles.sendBtn}
          onClick={sendMessage}
          disabled={!prompt.trim() || thinking}
          title="Send (Ctrl+Enter)"
        >
          ↑
        </button>
      </div>
      <div className={styles.composerHint}>
        <kbd>Ctrl</kbd>+<kbd>Enter</kbd> to send
      </div>
    </div>
  );
}
