import React from "react";
import { PanelButton } from "components/PanelButton";
import { columnStyle, rowStyle } from "styles";

export function MovePad({
  disabled,
  step,
  labels,
  onMove,
}: {
  disabled: boolean;
  step: number;
  labels: {
    north: string;
    south: string;
    west: string;
    east: string;
  };
  onMove: (x: number, y: number) => void;
}): JSX.Element {
  return (
    <div style={{ ...columnStyle, gap: "3rem", width: "92rem" }}>
      <div style={{ ...rowStyle, justifyContent: "center" }}>
        <PanelButton disabled={disabled} style={{ width: "30rem", minHeight: "25rem", padding: 0 }} onSelect={() => onMove(0, step)}>
          {labels.north}
        </PanelButton>
      </div>
      <div style={{ ...rowStyle, justifyContent: "center", gap: "3rem" }}>
        <PanelButton disabled={disabled} style={{ width: "30rem", minHeight: "25rem", padding: 0 }} onSelect={() => onMove(-step, 0)}>
          {labels.west}
        </PanelButton>
        <PanelButton disabled={disabled} style={{ width: "30rem", minHeight: "25rem", padding: 0 }} onSelect={() => onMove(0, -step)}>
          {labels.south}
        </PanelButton>
        <PanelButton disabled={disabled} style={{ width: "30rem", minHeight: "25rem", padding: 0 }} onSelect={() => onMove(step, 0)}>
          {labels.east}
        </PanelButton>
      </div>
    </div>
  );
}
