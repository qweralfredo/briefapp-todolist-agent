import type { ReactNode } from "react";
import styles from "./AppShell.module.css";

interface AppShellProps {
  sidebar: ReactNode;
  header: ReactNode;
  children: ReactNode;
}

export function AppShell({ sidebar, header, children }: AppShellProps) {
  return (
    <div className={styles.shell}>
      <aside className={styles.sidebar}>{sidebar}</aside>
      <div className={styles.main}>
        <header className={styles.header}>{header}</header>
        <main className={styles.content}>{children}</main>
      </div>
    </div>
  );
}
