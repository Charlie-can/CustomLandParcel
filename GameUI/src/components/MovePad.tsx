import React from "react";
import { PanelButton } from "components/PanelButton";
import { columnStyle, rowStyle } from "styles";

export function MovePad({
  disabled,
  step,
  onMove,
}: {
  disabled: boolean;
  step: number;
  onMove: (x: number, y: number) => void;
}): JSX.Element {
  return (
    <div style={{ ...columnStyle, gap: "4rem", width: "106rem" }}>
      <div style={{ ...rowStyle, justifyContent: "center" }}>
        <PanelButton disabled={disabled} style={{ width: "34rem" }} onSelect={() => onMove(0, step)}>
          N
        </PanelButton>
      </div>
      <div style={{ ...rowStyle, justifyContent: "center", gap: "3rem" }}>
        <PanelButton disabled={disabled} style={{ width: "34rem" }} onSelect={() => onMove(-step, 0)}>
          W
        </PanelButton>
        <PanelButton disabled={disabled} style={{ width: "34rem" }} onSelect={() => onMove(0, -step)}>
          S
        </PanelButton>
        <PanelButton disabled={disabled} style={{ width: "34rem" }} onSelect={() => onMove(step, 0)}>
          E
        </PanelButton>
      </div>
    </div>
  );
}
