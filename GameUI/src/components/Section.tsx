import React from "react";
import { colors, columnStyle, rowStyle } from "styles";

export function Section({ title, children }: { title: string; children: React.ReactNode }): JSX.Element {
  return (
    <div style={columnStyle}>
      <div style={{ ...rowStyle, minHeight: "20rem", justifyContent: "space-between" }}>
        <span style={{ fontSize: "12rem", fontWeight: 800, color: colors.text }}>{title}</span>
      </div>
      {children}
    </div>
  );
}
