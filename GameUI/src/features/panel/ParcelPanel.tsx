import React, { useEffect, useMemo, useState } from "react";
import { useValue } from "cs2/api";
import {
  editToolActiveBinding,
  moveSelectedParcel,
  moveSelectedVertex,
  parcelsBinding,
  selectedParcelIdBinding,
  selectedVertexIndexBinding,
  send,
} from "bindings";
import { MovePad } from "components/MovePad";
import { PanelButton } from "components/PanelButton";
import { Section } from "components/Section";
import { SelectedParcel } from "domain";
import { MergeConfirm } from "features/parcels/MergeConfirm";
import { ParcelRow } from "features/parcels/ParcelRow";
import { VertexList } from "features/vertices/VertexList";
import { colors, columnStyle, inputStyle, rowStyle } from "styles";

export function ParcelPanel({ onClose }: { onClose: () => void }): JSX.Element {
  const parcels = useValue(parcelsBinding) || [];
  const selectedParcelId = useValue(selectedParcelIdBinding);
  const selectedVertexIndex = useValue(selectedVertexIndexBinding);
  const editToolActive = useValue(editToolActiveBinding);
  const [step, setStep] = useState(100);
  const [mergeTargetId, setMergeTargetId] = useState<string | null>(null);

  const selected = useMemo<SelectedParcel | null>(() => {
    const found = parcels.find((parcel) => parcel.id === selectedParcelId) || parcels.find((parcel) => parcel.selected);
    return found ? { ...found, selectedVertexIndex } : null;
  }, [parcels, selectedParcelId, selectedVertexIndex]);

  const mergeTarget = useMemo(
    () => parcels.find((parcel) => parcel.id === mergeTargetId && parcel.id !== selected?.id) || null,
    [mergeTargetId, parcels, selected?.id],
  );

  useEffect(() => {
    if (mergeTargetId && !mergeTarget) {
      setMergeTargetId(null);
    }
  }, [mergeTargetId, mergeTarget]);

  return (
    <div
      style={{
        position: "absolute",
        top: "64rem",
        left: "72rem",
        width: "430rem",
        maxHeight: "720rem",
        overflowY: "auto",
        zIndex: 10000,
        display: "flex",
        flexDirection: "column",
        gap: "12rem",
        padding: "12rem",
        color: colors.text,
        background: colors.panel,
        border: `1rem solid ${colors.border}`,
        borderRadius: "5rem",
        boxShadow: "0 12rem 38rem rgba(0, 0, 0, 0.34)",
        fontSize: "13rem",
      }}
    >
      <div style={{ ...rowStyle, justifyContent: "space-between", minHeight: "36rem" }}>
        <div style={{ ...columnStyle, gap: "2rem", minWidth: 0 }}>
          <span style={{ fontSize: "17rem", fontWeight: 900 }}>Custom Land Parcel</span>
          <span style={{ color: colors.muted, fontSize: "11rem" }}>
            {parcels.length} parcels
            {selected ? ` / ${selected.points.length} pts / vertex ${selected.selectedVertexIndex + 1}` : " / no selection"}
          </span>
        </div>
        <PanelButton tone="subtle" style={{ width: "34rem", padding: 0, flex: "0 0 auto" }} onSelect={onClose}>
          x
        </PanelButton>
      </div>

      <Section title="Tools">
        <div style={{ ...rowStyle, alignItems: "stretch" }}>
          <PanelButton
            active={editToolActive}
            tone={editToolActive ? "primary" : "default"}
            style={{ flex: "1 1 0", minHeight: "38rem" }}
            tooltipLabel="Toggle map parcel edit tool"
            onSelect={() => send("setParcelEditToolActive", !editToolActive)}
          >
            {editToolActive ? "Map Tool On" : "Map Tool Off"}
          </PanelButton>
          <PanelButton
            style={{ flex: "1 1 0", minHeight: "38rem" }}
            tooltipLabel="Add a rectangular parcel"
            onSelect={() => send("addRectangle")}
          >
            New Rectangle
          </PanelButton>
        </div>
      </Section>

      <Section title="Parcels">
        <div style={{ ...columnStyle, maxHeight: "190rem", overflowY: "auto", gap: "6rem" }}>
          {parcels.map((parcel) => (
            <ParcelRow
              key={parcel.id}
              parcel={parcel}
              mergeTargetId={mergeTargetId}
              onPickMergeTarget={setMergeTargetId}
            />
          ))}
        </div>
      </Section>

      <Section title="Selected">
        <input
          style={inputStyle}
          value={selected ? selected.name : ""}
          disabled={!selected}
          onChange={(event) => selected && send("renameSelectedParcel", event.currentTarget.value)}
          title="Rename selected parcel"
        />
        <MergeConfirm selected={selected} target={mergeTarget} onCancel={() => setMergeTargetId(null)} />
        <div style={{ ...rowStyle, alignItems: "stretch" }}>
          <PanelButton style={{ flex: "1 1 0" }} disabled={!selected} onSelect={() => send("selectNextParcel", -1)}>
            Prev
          </PanelButton>
          <PanelButton style={{ flex: "1 1 0" }} disabled={!selected} onSelect={() => send("selectNextParcel", 1)}>
            Next
          </PanelButton>
          <PanelButton tone="danger" style={{ flex: "1 1 0" }} disabled={!selected} onSelect={() => send("deleteSelectedParcel")}>
            Delete
          </PanelButton>
        </div>
      </Section>

      <Section title="Move">
        <div style={{ ...rowStyle, alignItems: "flex-start", justifyContent: "space-between" }}>
          <MovePad disabled={!selected} step={step} onMove={moveSelectedParcel} />
          <label style={{ ...columnStyle, gap: "4rem", color: colors.muted, fontSize: "11rem", width: "100rem" }}>
            Step
            <input
              style={{ ...inputStyle, width: "100%", flex: "0 0 auto" }}
              type="number"
              value={step}
              min={1}
              onChange={(event) => setStep(Math.max(1, Number(event.currentTarget.value) || 1))}
            />
          </label>
        </div>
      </Section>

      <Section title="Vertices">
        <VertexList selected={selected} />
        <div style={{ ...rowStyle, alignItems: "flex-start", justifyContent: "space-between" }}>
          <MovePad disabled={!selected} step={step} onMove={moveSelectedVertex} />
          <div style={{ ...columnStyle, flex: "1 1 auto" }}>
            <PanelButton disabled={!selected} onSelect={() => send("insertVertexAfterSelected")}>
              Insert Vertex
            </PanelButton>
            <PanelButton tone="danger" disabled={!selected} onSelect={() => send("deleteSelectedVertex")}>
              Delete Vertex
            </PanelButton>
          </div>
        </div>
      </Section>
    </div>
  );
}
