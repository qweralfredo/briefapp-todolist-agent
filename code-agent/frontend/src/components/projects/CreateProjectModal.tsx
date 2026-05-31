import { useState } from "react";
import { Modal } from "../ui/Modal";
import { Input } from "../ui/Input";
import { Button } from "../ui/Button";
import { api } from "../../api";
import type { Project } from "../../types";

interface CreateProjectModalProps {
  open: boolean;
  onClose: () => void;
  onCreated: (project: Project) => void;
}

export function CreateProjectModal({ open, onClose, onCreated }: CreateProjectModalProps) {
  const [name, setName] = useState("");
  const [description, setDescription] = useState("");
  const [error, setError] = useState("");
  const [loading, setLoading] = useState(false);

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault();
    if (!name.trim()) {
      setError("Project name is required.");
      return;
    }
    setLoading(true);
    setError("");
    try {
      const project = await api.projects.create({ name, description });
      onCreated(project);
      setName("");
      setDescription("");
      onClose();
    } catch (err) {
      setError(err instanceof Error ? err.message : "Failed to create project.");
    } finally {
      setLoading(false);
    }
  }

  return (
    <Modal
      open={open}
      onClose={onClose}
      title="New Project"
      footer={
        <>
          <Button variant="ghost" onClick={onClose} disabled={loading}>
            Cancel
          </Button>
          <Button variant="primary" onClick={handleSubmit} disabled={loading}>
            {loading ? "Creating…" : "Create Project"}
          </Button>
        </>
      }
    >
      <form onSubmit={handleSubmit} style={{ display: "flex", flexDirection: "column", gap: "16px" }}>
        <Input
          id="project-name"
          label="Project name"
          placeholder="my-project"
          value={name}
          onChange={(e) => setName(e.target.value)}
          error={error}
          autoFocus
        />
        <Input
          id="project-desc"
          label="Description (optional)"
          placeholder="What are you building?"
          value={description}
          onChange={(e) => setDescription(e.target.value)}
        />
        <p style={{ fontSize: "var(--text-xs)", color: "var(--color-fg-muted)" }}>
          A workspace folder will be created at <code>runtime/workspace/{name.toLowerCase().replace(/[^a-z0-9]+/g, "-") || "slug"}/</code>
        </p>
      </form>
    </Modal>
  );
}
