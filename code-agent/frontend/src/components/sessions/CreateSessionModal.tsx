import { useState, useEffect } from "react";
import { Modal } from "../ui/Modal";
import { Input } from "../ui/Input";
import { Button } from "../ui/Button";
import { api } from "../../api";
import type { Project, Session } from "../../types";

interface CreateSessionModalProps {
  open: boolean;
  onClose: () => void;
  onCreated: (session: Session) => void;
  projects: Project[];
  defaultProjectId?: string | null;
}

export function CreateSessionModal({
  open,
  onClose,
  onCreated,
  projects,
  defaultProjectId,
}: CreateSessionModalProps) {
  const [title, setTitle] = useState("");
  const [projectId, setProjectId] = useState(defaultProjectId ?? "");
  const [model, setModel] = useState("");
  const [models, setModels] = useState<string[]>([]);
  const [error, setError] = useState("");
  const [loading, setLoading] = useState(false);

  useEffect(() => {
    if (open) {
      setProjectId(defaultProjectId ?? projects[0]?.id ?? "");
      api.models.list().then(setModels).catch(() => setModels([]));
    }
  }, [open, defaultProjectId, projects]);

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault();
    if (!projectId) {
      setError("Please select a project.");
      return;
    }
    setLoading(true);
    setError("");
    try {
      const session = await api.sessions.create({
        projectId,
        title: title.trim() || undefined,
        model: model || undefined,
      });
      onCreated(session);
      setTitle("");
      onClose();
    } catch (err) {
      setError(err instanceof Error ? err.message : "Failed to create session.");
    } finally {
      setLoading(false);
    }
  }

  return (
    <Modal
      open={open}
      onClose={onClose}
      title="New Session"
      footer={
        <>
          <Button variant="ghost" onClick={onClose} disabled={loading}>
            Cancel
          </Button>
          <Button variant="primary" onClick={handleSubmit} disabled={loading || !projectId}>
            {loading ? "Creating…" : "Start Session"}
          </Button>
        </>
      }
    >
      <form onSubmit={handleSubmit} style={{ display: "flex", flexDirection: "column", gap: "16px" }}>
        <div style={{ display: "flex", flexDirection: "column", gap: "4px" }}>
          <label style={{ fontSize: "var(--text-sm)", fontWeight: 500, color: "var(--color-fg-muted)" }} htmlFor="session-project">
            Project <span style={{ color: "var(--color-red)" }}>*</span>
          </label>
          <select
            id="session-project"
            value={projectId}
            onChange={(e) => setProjectId(e.target.value)}
            style={{
              background: "var(--color-bg)",
              border: "1px solid var(--color-border)",
              borderRadius: "var(--radius-md)",
              color: "var(--color-fg)",
              fontSize: "var(--text-base)",
              padding: "6px 10px",
              outline: "none",
            }}
          >
            {projects.map((p) => (
              <option key={p.id} value={p.id}>{p.name}</option>
            ))}
            {projects.length === 0 && <option value="">No projects</option>}
          </select>
        </div>

        <Input
          id="session-title"
          label="Session title (optional)"
          placeholder="New coding session"
          value={title}
          onChange={(e) => setTitle(e.target.value)}
          autoFocus
        />

        {models.length > 0 && (
          <div style={{ display: "flex", flexDirection: "column", gap: "4px" }}>
            <label style={{ fontSize: "var(--text-sm)", fontWeight: 500, color: "var(--color-fg-muted)" }} htmlFor="session-model">
              Model
            </label>
            <select
              id="session-model"
              value={model}
              onChange={(e) => setModel(e.target.value)}
              style={{
                background: "var(--color-bg)",
                border: "1px solid var(--color-border)",
                borderRadius: "var(--radius-md)",
                color: "var(--color-fg)",
                fontSize: "var(--text-base)",
                padding: "6px 10px",
                outline: "none",
              }}
            >
              <option value="">Default model</option>
              {models.map((m) => (
                <option key={m} value={m}>{m}</option>
              ))}
            </select>
          </div>
        )}

        {error && <p style={{ fontSize: "var(--text-sm)", color: "var(--color-red)" }}>{error}</p>}
      </form>
    </Modal>
  );
}
