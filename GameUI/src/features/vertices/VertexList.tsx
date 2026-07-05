import React from "react";
import { send } from "bindings";
import { PanelButton } from "components/PanelButton";
import { SelectedParcel } from "domain";
import { formatPoint } from "utils/format";
import { colors, columnStyle } from "styles";

export function VertexList({ selected }: { selected: SelectedParcel | null }): JSX.Element {
  if (!selected || !selected.points || selected.points.length === 0) {
    return <div style={{ color: colors.muted, fontSize: "11rem" }}>No selected parcel.</div>;
  }

  return (
    <div style={{ ...columnStyle, maxHeight: "96rem", overflowY: "auto", gap: "3rem" }}>
      {selected.points.map((point, index) => (
        <PanelButton
          key={`${selected.id}-${index}`}
          active={index === selected.selectedVertexIndex}
          style={{ justifyContent: "flex-start", width: "100%", minHeight: "24rem" }}
          tooltipLabel="Select vertex"
          onSelect={() => send("selectVertex", index)}
        >
          V{index + 1}: {formatPoint(point)}
        </PanelButton>
      ))}
    </div>
  );
}
