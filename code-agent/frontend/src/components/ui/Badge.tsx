import type { ReactNode } from "react";
import styles from "./Badge.module.css";

interface BadgeProps {
  color?: "blue" | "green" | "purple" | "orange" | "red" | "default";
  children: ReactNode;
}

export function Badge({ color = "default", children }: BadgeProps) {
  return <span className={[styles.badge, styles[color]].join(" ")}>{children}</span>;
}
