import { useState } from "react";
import type { Session } from "../../types";
import styles from "./WorkspacePanels.module.css";

interface WorkspacePanelsProps {
  session: Session;
}

type PanelTab = "plan" | "files" | "commands" | "workspace";

export function WorkspacePanels({ session }: WorkspacePanelsProps) {
  const [activeTab, setActiveTab] = useState<PanelTab>("plan");

  const tabs: { id: PanelTab; label: string; count?: number }[] = [
    { id: "plan", label: "Plan", count: session.plan.length },
    { id: "files", label: "Files", count: session.touchedFiles.length },
    { id: "commands", label: "Commands", count: session.commands.length },
    { id: "workspace", label: "Workspace" },
  ];

  return (
    <div className={styles.panels}>
      <div className={styles.tabs}>
        {tabs.map((tab) => (
          <button
            key={tab.id}
            className={[styles.tab, activeTab === tab.id ? styles.active : ""].join(" ")}
            onClick={() => setActiveTab(tab.id)}
          >
            {tab.label}
            {tab.count !== undefined && tab.count > 0 && (
              <span className={styles.count}>{tab.count}</span>
            )}
          </button>
        ))}
      </div>

      <div className={styles.content}>
        {activeTab === "plan" && <PlanPanel steps={session.plan} />}
        {activeTab === "files" && <FilesPanel files={session.touchedFiles} />}
        {activeTab === "commands" && <CommandsPanel commands={session.commands} />}
        {activeTab === "workspace" && <WorkspaceInfoPanel session={session} />}
      </div>
    </div>
  );
}

function PlanPanel({ steps }: { steps: string[] }) {
  if (steps.length === 0) {
    return <EmptyState text="No plan generated yet" />;
  }
  return (
    <ol className={styles.planList}>
      {steps.map((step, i) => (
        <li key={i} className={styles.planStep}>
          <span className={styles.stepNum}>{i + 1}</span>
          <span>{step}</span>
        </li>
      ))}
    </ol>
  );
}

function FilesPanel({ files }: { files: string[] }) {
  if (files.length === 0) {
    return <EmptyState text="No files touched yet" />;
  }
  return (
    <ul className={styles.fileList}>
      {files.map((f) => (
        <li key={f} className={styles.fileItem}>
          <span className={styles.fileIcon}>📄</span>
          <code>{f}</code>
        </li>
      ))}
    </ul>
  );
}

function CommandsPanel({ commands }: { commands: Session["commands"] }) {
  if (commands.length === 0) {
    return <EmptyState text="No commands executed yet" />;
  }
  return (
    <div className={styles.commandList}>
      {commands.map((cmd, i) => (
        <div key={i} className={styles.commandEntry}>
          <div className={styles.commandHeader}>
            <code className={styles.commandText}>$ {cmd.command}</code>
            <span className={[styles.exitCode, cmd.code === 0 ? styles.ok : styles.fail].join(" ")}>
              {cmd.timedOut ? "timeout" : `exit ${cmd.code}`}
            </span>
          </div>
          {cmd.stdout && <pre className={styles.commandOutput}>{cmd.stdout}</pre>}
          {cmd.stderr && <pre className={[styles.commandOutput, styles.stderr].join(" ")}>{cmd.stderr}</pre>}
        </div>
      ))}
    </div>
  );
}

function WorkspaceInfoPanel({ session }: { session: Session }) {
  return (
    <div className={styles.workspaceInfo}>
      <div className={styles.infoRow}>
        <span className={styles.infoLabel}>Project</span>
        <span className={styles.infoValue}>{session.projectName}</span>
      </div>
      <div className={styles.infoRow}>
        <span className={styles.infoLabel}>Workspace</span>
        <code className={styles.infoValue}>{session.workspacePath}</code>
      </div>
      <div className={styles.infoRow}>
        <span className={styles.infoLabel}>Model</span>
        <code className={styles.infoValue}>{session.model}</code>
      </div>
      <div className={styles.infoRow}>
        <span className={styles.infoLabel}>Session ID</span>
        <code className={styles.infoValue} style={{ fontSize: "var(--text-xs)" }}>{session.id}</code>
      </div>
    </div>
  );
}

function EmptyState({ text }: { text: string }) {
  return (
    <div className={styles.empty}>
      <span>{text}</span>
    </div>
  );
}
