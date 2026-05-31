import type { Project, Session } from "../../types";
import { Button } from "../ui/Button";
import styles from "./Sidebar.module.css";

interface SidebarProps {
  projects: Project[];
  sessions: Session[];
  activeProjectId: string | null;
  activeSessionId: string | null;
  onSelectProject: (id: string) => void;
  onSelectSession: (id: string) => void;
  onNewProject: () => void;
  onNewSession: () => void;
}

export function Sidebar({
  projects,
  sessions,
  activeProjectId,
  activeSessionId,
  onSelectProject,
  onSelectSession,
  onNewProject,
  onNewSession,
}: SidebarProps) {
  const filteredSessions = activeProjectId
    ? sessions.filter((s) => s.projectId === activeProjectId)
    : sessions;

  return (
    <div className={styles.sidebar}>
      {/* Logo */}
      <div className={styles.logo}>
        <svg width="20" height="20" viewBox="0 0 24 24" fill="none" xmlns="http://www.w3.org/2000/svg">
          <path d="M12 2L2 7L12 12L22 7L12 2Z" stroke="var(--color-blue)" strokeWidth="2" strokeLinejoin="round"/>
          <path d="M2 17L12 22L22 17" stroke="var(--color-blue)" strokeWidth="2" strokeLinejoin="round"/>
          <path d="M2 12L12 17L22 12" stroke="var(--color-purple)" strokeWidth="2" strokeLinejoin="round"/>
        </svg>
        <span className={styles.logoText}>Code Agent</span>
      </div>

      {/* Projects section */}
      <div className={styles.section}>
        <div className={styles.sectionHeader}>
          <span className={styles.sectionLabel}>Projects</span>
          <button className={styles.addBtn} onClick={onNewProject} title="New project">+</button>
        </div>
        <div className={styles.list}>
          {projects.map((p) => (
            <button
              key={p.id}
              className={[styles.item, activeProjectId === p.id ? styles.active : ""].join(" ")}
              onClick={() => onSelectProject(p.id)}
            >
              <span className={styles.projectIcon}>◈</span>
              <span className={styles.itemLabel}>{p.name}</span>
            </button>
          ))}
          {projects.length === 0 && (
            <span className={styles.empty}>No projects yet</span>
          )}
        </div>
      </div>

      {/* Sessions section */}
      <div className={[styles.section, styles.sessionsSection].join(" ")}>
        <div className={styles.sectionHeader}>
          <span className={styles.sectionLabel}>Sessions</span>
          <button
            className={styles.addBtn}
            onClick={onNewSession}
            title="New session"
            disabled={!activeProjectId}
          >
            +
          </button>
        </div>
        <div className={styles.list}>
          {filteredSessions.map((s) => (
            <button
              key={s.id}
              className={[styles.item, activeSessionId === s.id ? styles.active : ""].join(" ")}
              onClick={() => onSelectSession(s.id)}
            >
              <span className={styles.sessionIcon}>⬡</span>
              <span className={styles.itemLabel}>{s.title}</span>
            </button>
          ))}
          {filteredSessions.length === 0 && (
            <span className={styles.empty}>
              {activeProjectId ? "No sessions" : "Select a project"}
            </span>
          )}
        </div>
      </div>
    </div>
  );
}
