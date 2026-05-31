import { useState, useEffect, useCallback } from "react";
import type { Project, Session } from "./types";
import { api } from "./api";
import { AppShell } from "./components/layout/AppShell";
import { Sidebar } from "./components/layout/Sidebar";
import { ChatPanel } from "./components/chat/ChatPanel";
import { WorkspacePanels } from "./components/panels/WorkspacePanels";
import { CreateProjectModal } from "./components/projects/CreateProjectModal";
import { CreateSessionModal } from "./components/sessions/CreateSessionModal";
import { useSSE } from "./hooks/useSSE";
import styles from "./App.module.css";

export function App() {
  const [projects, setProjects] = useState<Project[]>([]);
  const [sessions, setSessions] = useState<Session[]>([]);
  const [activeProjectId, setActiveProjectId] = useState<string | null>(null);
  const [activeSessionId, setActiveSessionId] = useState<string | null>(null);
  const [showCreateProject, setShowCreateProject] = useState(false);
  const [showCreateSession, setShowCreateSession] = useState(false);
  const [loading, setLoading] = useState(true);

  const activeSession = sessions.find((s) => s.id === activeSessionId) ?? null;
  const activeProject = projects.find((p) => p.id === activeProjectId) ?? null;

  // Load initial data
  useEffect(() => {
    Promise.all([api.projects.list(), api.sessions.list()])
      .then(([projs, sess]) => {
        setProjects(projs);
        setSessions(sess);
        if (projs.length > 0) setActiveProjectId(projs[0].id);
      })
      .catch(console.error)
      .finally(() => setLoading(false));
  }, []);

  // SSE for active session
  const handleSSEEvent = useCallback(
    (event: string, data: unknown) => {
      if (event === "session:update" || event === "assistant:message") {
        const sessionData = event === "assistant:message"
          ? (data as { session?: Session })?.session
          : (data as Session);
        if (sessionData?.id) {
          setSessions((prev) =>
            prev.map((s) => (s.id === sessionData.id ? sessionData : s)),
          );
        }
      }
    },
    [],
  );

  useSSE(activeSessionId, handleSSEEvent);

  function handleProjectCreated(project: Project) {
    setProjects((prev) => [project, ...prev]);
    setActiveProjectId(project.id);
  }

  function handleSessionCreated(session: Session) {
    setSessions((prev) => [session, ...prev]);
    setActiveSessionId(session.id);
  }

  function handleSessionUpdate(updated: Session) {
    setSessions((prev) =>
      prev.map((s) => (s.id === updated.id ? updated : s)),
    );
  }

  function handleSelectProject(id: string) {
    setActiveProjectId(id);
    // Clear session selection when changing project
    const projectSessions = sessions.filter((s) => s.projectId === id);
    setActiveSessionId(projectSessions[0]?.id ?? null);
  }

  // Keyboard shortcuts
  useEffect(() => {
    function handler(e: KeyboardEvent) {
      if ((e.metaKey || e.ctrlKey) && e.key === "k") {
        e.preventDefault();
        if (activeProjectId) setShowCreateSession(true);
      }
    }
    document.addEventListener("keydown", handler);
    return () => document.removeEventListener("keydown", handler);
  }, [activeProjectId]);

  if (loading) {
    return (
      <div className={styles.loading}>
        <div className={styles.loadingSpinner} />
        <span>Loading Code Agent…</span>
      </div>
    );
  }

  const header = (
    <div className={styles.header}>
      {activeProject && (
        <>
          <span className={styles.breadcrumb}>
            <span className={styles.breadcrumbProject}>{activeProject.name}</span>
            {activeSession && (
              <>
                <span className={styles.breadcrumbSep}>/</span>
                <span className={styles.breadcrumbSession}>{activeSession.title}</span>
              </>
            )}
          </span>
          {activeSession && (
            <span className={styles.modelBadge}>{activeSession.model}</span>
          )}
        </>
      )}
      {!activeProject && (
        <span className={styles.breadcrumbMuted}>No project selected</span>
      )}
      <div className={styles.headerSpacer} />
      <kbd className={styles.shortcutHint} title="New session">⌘K</kbd>
    </div>
  );

  return (
    <>
      <AppShell
        sidebar={
          <Sidebar
            projects={projects}
            sessions={sessions}
            activeProjectId={activeProjectId}
            activeSessionId={activeSessionId}
            onSelectProject={handleSelectProject}
            onSelectSession={setActiveSessionId}
            onNewProject={() => setShowCreateProject(true)}
            onNewSession={() => {
              if (activeProjectId) setShowCreateSession(true);
            }}
          />
        }
        header={header}
      >
        {activeSession ? (
          <div className={styles.workspace}>
            <ChatPanel
              session={activeSession}
              onSessionUpdate={handleSessionUpdate}
            />
            <WorkspacePanels session={activeSession} />
          </div>
        ) : (
          <div className={styles.welcome}>
            <div className={styles.welcomeIcon}>⬡</div>
            <h2>Welcome to Code Agent</h2>
            <p>
              {activeProjectId
                ? "Create a new session to start coding with the AI agent."
                : "Select or create a project to get started."}
            </p>
            {activeProjectId ? (
              <button
                className={styles.welcomeBtn}
                onClick={() => setShowCreateSession(true)}
              >
                New Session
              </button>
            ) : (
              <button
                className={styles.welcomeBtn}
                onClick={() => setShowCreateProject(true)}
              >
                New Project
              </button>
            )}
          </div>
        )}
      </AppShell>

      <CreateProjectModal
        open={showCreateProject}
        onClose={() => setShowCreateProject(false)}
        onCreated={handleProjectCreated}
      />

      <CreateSessionModal
        open={showCreateSession}
        onClose={() => setShowCreateSession(false)}
        onCreated={handleSessionCreated}
        projects={projects}
        defaultProjectId={activeProjectId}
      />
    </>
  );
}
