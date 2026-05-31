export interface Project {
  id: string;
  name: string;
  slug: string;
  description: string;
  createdAt: string;
  workspacePath: string;
}

export interface Session {
  id: string;
  title: string;
  model: string;
  projectId: string | null;
  projectName: string;
  workspacePath: string;
  createdAt: string;
  updatedAt: string;
  messages: Message[];
  plan: string[];
  touchedFiles: string[];
  commands: CommandRecord[];
  selectedSkillIds: string[];
}

export interface Message {
  role: "user" | "assistant";
  content: string;
  createdAt: string;
}

export interface CommandRecord {
  command: string;
  code: number;
  timedOut: boolean;
  stdout: string;
  stderr: string;
  createdAt: string;
}

export interface Skill {
  id: string;
  name: string;
  description: string;
}

export interface SSEEvent {
  type: string;
  payload: unknown;
}

export type AgentPhase = "idle" | "thinking" | "acting" | "done" | "error";
