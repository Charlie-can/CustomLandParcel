import React from "react";
import { colors, columnStyle, rowStyle } from "styles";

export function Section({ title, children }: { title: string; children: React.ReactNode }): JSX.Element {
  return (
    <div style={{ ...columnStyle, gap: "4rem" }}>
      <div style={{ ...rowStyle, minHeight: "16rem", justifyContent: "space-between" }}>
        <span style={{ fontSize: "10.5rem", fontWeight: 900, color: colors.text }}>{title}</span>
        <div style={{ flex: "1 1 auto", height: "1rem", background: "rgba(215, 235, 246, 0.11)" }} />
      </div>
      {children}
    </div>
  );
}
