import type { Project, Session, Skill } from "./types";

const BASE = "/api";

async function request<T>(path: string, options?: RequestInit): Promise<T> {
  const response = await fetch(`${BASE}${path}`, {
    headers: { "Content-Type": "application/json", ...options?.headers },
    ...options,
  });
  if (!response.ok) {
    const text = await response.text().catch(() => response.statusText);
    throw new Error(text);
  }
  return response.json() as Promise<T>;
}

// Projects
export const api = {
  projects: {
    list: () => request<{ projects: Project[] }>("/projects").then((r) => r.projects),
    get: (id: string) => request<{ project: Project }>(`/projects/${id}`).then((r) => r.project),
    create: (data: { name: string; slug?: string; description?: string }) =>
      request<{ project: Project }>("/projects", { method: "POST", body: JSON.stringify(data) }).then((r) => r.project),
    update: (id: string, data: { name?: string; description?: string }) =>
      request<{ project: Project }>(`/projects/${id}`, {
        method: "PATCH",
        body: JSON.stringify(data),
      }).then((r) => r.project),
    delete: (id: string) =>
      request<{ project: Project }>(`/projects/${id}`, { method: "DELETE" }).then((r) => r.project),
    scaffold: (id: string) =>
      request<{ scaffold: { stack: string; created: string[] } }>(`/projects/${id}/scaffold`, {
        method: "POST",
      }),
  },

  sessions: {
    list: () => request<{ sessions: Session[] }>("/sessions").then((r) => r.sessions),
    get: (id: string) => request<{ session: Session }>(`/sessions/${id}`).then((r) => r.session),
    create: (data: { projectId: string; title?: string; model?: string }) =>
      request<{ session: Session }>("/sessions", { method: "POST", body: JSON.stringify(data) }).then((r) => r.session),
    sendMessage: (
      sessionId: string,
      data: { prompt: string; model?: string; skillIds?: string[] },
    ) =>
      request<{ session: Session }>(`/sessions/${sessionId}/messages`, {
        method: "POST",
        body: JSON.stringify(data),
      }).then((r) => r.session),
  },

  skills: {
    list: () => request<{ skills: Skill[] }>("/skills").then((r) => r.skills),
  },

  models: {
    list: () =>
      request<{ models: Array<{ name: string; size?: number; modifiedAt?: string } | string> }>("/models").then((r) =>
        r.models.map((m) => (typeof m === "string" ? m : m.name)),
      ),
  },
};
