import React from "react";
import { Button } from "cs2/ui";
import { send } from "bindings";
import { PanelButton } from "components/PanelButton";
import { Parcel } from "domain";
import { formatArea } from "utils/format";
import { colors, rowStyle } from "styles";

export function ParcelRow({
  parcel,
  mergeTargetId,
  onPickMergeTarget,
}: {
  parcel: Parcel;
  mergeTargetId: string | null;
  onPickMergeTarget: (id: string) => void;
}): JSX.Element {
  const activeTarget = parcel.id === mergeTargetId;
  const isLocked = parcel.state === "Locked";

  return (
    <div
      style={{
        ...rowStyle,
        padding: "5rem",
        background: parcel.selected ? colors.primarySoft : colors.panelSofter,
        border: parcel.selected ? "1rem solid rgba(116, 202, 255, 0.76)" : `1rem solid ${colors.border}`,
        borderRadius: "4rem",
      }}
    >
      <Button
        style={{
          flex: "1 1 auto",
          minWidth: 0,
          padding: 0,
          color: colors.text,
          background: "rgba(0, 0, 0, 0)",
          border: "0rem solid rgba(0, 0, 0, 0)",
          textAlign: "left",
        }}
        selected={parcel.selected}
        onSelect={() => send("selectParcel", parcel.id)}
        tooltipLabel="Select parcel"
      >
        <div style={{ display: "flex", flexDirection: "column", gap: "1rem", minWidth: 0 }}>
          <div style={{ ...rowStyle, minWidth: 0 }}>
            <span style={{ overflow: "hidden", textOverflow: "ellipsis", whiteSpace: "nowrap", fontWeight: 800 }}>
              {parcel.name || "Parcel"}
            </span>
            <span style={{ color: isLocked ? colors.amber : colors.green, fontSize: "10rem", fontWeight: 800 }}>
              {isLocked ? "Locked" : "Active"}
            </span>
          </div>
          <div style={{ ...rowStyle, color: colors.muted, fontSize: "10rem" }}>
            <span>{formatArea(parcel.area)}</span>
            <span>{parcel.points.length} pts</span>
          </div>
        </div>
      </Button>
      <PanelButton
        disabled={parcel.selected}
        active={activeTarget}
        tone={activeTarget ? "primary" : "subtle"}
        style={{ width: "58rem", flex: "0 0 auto" }}
        tooltipLabel="Use as merge target"
        onSelect={() => onPickMergeTarget(parcel.id)}
      >
        {parcel.selected ? "Selected" : activeTarget ? "Target" : "Merge"}
      </PanelButton>
    </div>
  );
}
