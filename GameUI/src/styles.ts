import React from "react";

export type ButtonTone = "default" | "primary" | "danger" | "subtle";

export const colors = {
  panel: "rgba(17, 24, 30, 0.95)",
  panelSofter: "rgba(255, 255, 255, 0.07)",
  border: "rgba(255, 255, 255, 0.14)",
  text: "rgba(245, 248, 252, 0.96)",
  muted: "rgba(245, 248, 252, 0.62)",
  primary: "rgba(33, 127, 184, 0.98)",
  primarySoft: "rgba(57, 144, 190, 0.28)",
  green: "rgba(123, 224, 151, 0.98)",
  amber: "rgba(238, 188, 82, 0.98)",
  danger: "rgba(143, 48, 57, 0.98)",
};

export const rowStyle: React.CSSProperties = {
  display: "flex",
  alignItems: "center",
  gap: "8rem",
};

export const columnStyle: React.CSSProperties = {
  display: "flex",
  flexDirection: "column",
  gap: "8rem",
};

export const inputStyle: React.CSSProperties = {
  flex: "1 1 auto",
  minWidth: 0,
  height: "30rem",
  padding: "0 8rem",
  color: colors.text,
  background: colors.panelSofter,
  border: `1rem solid ${colors.border}`,
  borderRadius: "4rem",
  fontSize: "13rem",
};

export const launcherButtonStyle: React.CSSProperties = {
  width: "44rem",
  height: "44rem",
  minHeight: "44rem",
  padding: 0,
  display: "flex",
  alignItems: "center",
  justifyContent: "center",
  color: colors.text,
  background: colors.primary,
  border: "1rem solid rgba(255, 255, 255, 0.22)",
  borderRadius: "5rem",
  fontSize: "14rem",
  fontWeight: 800,
  letterSpacing: 0,
};

export function getButtonStyle(
  tone: ButtonTone = "default",
  active = false,
  extra?: React.CSSProperties,
): React.CSSProperties {
  const backgroundByTone: Record<ButtonTone, string> = {
    default: "rgba(44, 56, 67, 0.96)",
    primary: colors.primary,
    danger: colors.danger,
    subtle: "rgba(255, 255, 255, 0.08)",
  };

  return {
    minHeight: "30rem",
    padding: "0 10rem",
    display: "flex",
    alignItems: "center",
    justifyContent: "center",
    color: colors.text,
    background: active ? colors.primary : backgroundByTone[tone],
    border: active ? "1rem solid rgba(134, 212, 255, 0.82)" : `1rem solid ${colors.border}`,
    borderRadius: "4rem",
    fontSize: "12rem",
    fontWeight: 700,
    letterSpacing: 0,
    ...extra,
  };
}
