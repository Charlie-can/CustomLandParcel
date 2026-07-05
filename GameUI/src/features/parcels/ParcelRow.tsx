import React from "react";
import { Button } from "cs2/ui";
import { send } from "bindings";
import { PanelButton } from "components/PanelButton";
import { Parcel } from "domain";
import { Translator } from "i18n";
import { formatArea } from "utils/format";
import { colors, rowStyle } from "styles";

export function ParcelRow({
  parcel,
  mergeTargetId,
  t,
  onPickMergeTarget,
}: {
  parcel: Parcel;
  mergeTargetId: string | null;
  t: Translator;
  onPickMergeTarget: (id: string) => void;
}): JSX.Element {
  const activeTarget = parcel.id === mergeTargetId;
  const isLocked = parcel.state === "Locked";
  const pointCount = Array.isArray(parcel.points) ? parcel.points.length : 0;
  const parcelColor = `rgba(${parcel.boundaryRed}, ${parcel.boundaryGreen}, ${parcel.boundaryBlue}, ${Math.max(0.18, parcel.boundaryOpacity / 100)})`;

  return (
    <div
      style={{
        ...rowStyle,
        padding: "4rem",
        background: parcel.selected ? colors.primarySoft : "rgba(232, 246, 255, 0.045)",
        border: parcel.selected ? `1rem solid ${colors.borderStrong}` : `1rem solid ${colors.border}`,
        borderRadius: "4rem",
      }}
    >
      <div
        style={{
          width: "5rem",
          alignSelf: "stretch",
          flex: "0 0 auto",
          background: parcelColor,
          borderRadius: "2rem",
          boxShadow: "inset 0 0 0 1rem rgba(0, 0, 0, 0.28)",
        }}
      />
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
        tooltipLabel={t("tooltip.selectParcel")}
      >
        <div style={{ display: "flex", flexDirection: "column", minWidth: 0 }}>
          <div style={{ ...rowStyle, minWidth: 0 }}>
            <span style={{ overflow: "hidden", textOverflow: "ellipsis", whiteSpace: "nowrap", fontWeight: 800 }}>
              {parcel.name || t("parcel.defaultName")}
            </span>
            <span style={{ color: isLocked ? colors.amber : colors.green, fontSize: "10rem", fontWeight: 800 }}>
              {isLocked ? t("state.locked") : t("state.active")}
            </span>
          </div>
          <div style={{ ...rowStyle, color: colors.muted, fontSize: "9rem" }}>
            <span>{formatArea(parcel.area)}</span>
            <span>{pointCount} pts</span>
            <span>{parcel.boundaryWidth}px</span>
          </div>
        </div>
      </Button>
      <PanelButton
        disabled={parcel.selected}
        active={activeTarget}
        tone={activeTarget ? "primary" : "subtle"}
        style={{ width: "58rem", flex: "0 0 auto" }}
        tooltipLabel={t("tooltip.mergeTarget")}
        onSelect={() => onPickMergeTarget(parcel.id)}
      >
        {parcel.selected ? t("parcel.selected") : activeTarget ? t("parcel.target") : t("parcel.merge")}
      </PanelButton>
    </div>
  );
}
