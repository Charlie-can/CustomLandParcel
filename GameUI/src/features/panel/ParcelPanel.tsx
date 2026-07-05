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
import { AppearanceControls } from "features/appearance/AppearanceControls";
import { MergeConfirm } from "features/parcels/MergeConfirm";
import { ParcelRow } from "features/parcels/ParcelRow";
import { VertexList } from "features/vertices/VertexList";
import { Translator } from "i18n";
import { colors, columnStyle, inputStyle, rowStyle, toolSurfaceStyle } from "styles";

export function ParcelPanel({ t, onClose }: { t: Translator; onClose: () => void }): JSX.Element {
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
        top: "56rem",
        left: "64rem",
        width: "410rem",
        maxHeight: "624rem",
        overflowY: "auto",
        zIndex: 10000,
        display: "flex",
        flexDirection: "column",
        gap: "6rem",
        padding: "10rem",
        color: colors.text,
        background: colors.panel,
        border: `1rem solid ${colors.border}`,
        borderRadius: "5rem",
        boxShadow: "0 18rem 46rem rgba(0, 0, 0, 0.48)",
        fontSize: "11rem",
      }}
    >
      <div style={{ ...rowStyle, justifyContent: "space-between", minHeight: "30rem" }}>
        <div style={{ ...columnStyle, gap: "2rem", minWidth: 0 }}>
          <span style={{ fontSize: "14.5rem", fontWeight: 900 }}>{t("app.title")}</span>
          <span style={{ color: colors.muted, fontSize: "10rem" }}>
            {selected
              ? t("summary.selected", {
                  count: parcels.length,
                  points: selected.points.length,
                  vertex: selected.selectedVertexIndex + 1,
                })
              : `${parcels.length} ${t("section.parcels")} / ${t("summary.none")}`}
          </span>
        </div>
        <PanelButton tone="subtle" style={{ width: "28rem", minHeight: "28rem", padding: 0, flex: "0 0 auto" }} onSelect={onClose}>
          x
        </PanelButton>
      </div>

      <Section title={t("section.tools")}>
        <div style={{ ...rowStyle, alignItems: "stretch", flexWrap: "wrap" }}>
          <PanelButton
            active={editToolActive}
            tone={editToolActive ? "primary" : "default"}
            style={{ flex: "1 1 0", minHeight: "28rem" }}
            tooltipLabel={t("tooltip.mapTool")}
            onSelect={() => send("setParcelEditToolActive", !editToolActive)}
          >
            {editToolActive ? t("tool.mapOn") : t("tool.mapOff")}
          </PanelButton>
          <PanelButton
            style={{ flex: "1 1 0", minHeight: "28rem" }}
            tooltipLabel={t("tooltip.newRectangle")}
            onSelect={() => send("addRectangle")}
          >
            {t("tool.newRectangle")}
          </PanelButton>
        </div>
      </Section>

      <Section title={t("section.parcels")}>
        <div style={{ ...columnStyle, maxHeight: "118rem", overflowY: "auto", gap: "4rem" }}>
          {parcels.map((parcel) => (
            <ParcelRow
              key={parcel.id}
              parcel={parcel}
              mergeTargetId={mergeTargetId}
              t={t}
              onPickMergeTarget={setMergeTargetId}
            />
          ))}
        </div>
      </Section>

      <Section title={t("section.selected")}>
        <div style={{ ...toolSurfaceStyle, ...columnStyle, gap: "5rem" }}>
          <div style={{ ...rowStyle, alignItems: "stretch", flexWrap: "wrap" }}>
            {selected && (
              <div
                style={{
                  width: "24rem",
                  minHeight: "24rem",
                  flex: "0 0 auto",
                  background: `rgba(${selected.boundaryRed}, ${selected.boundaryGreen}, ${selected.boundaryBlue}, ${Math.max(0.2, selected.boundaryOpacity / 100)})`,
                  border: "1rem solid rgba(235, 248, 255, 0.56)",
                  borderRadius: "4rem",
                  boxShadow: "inset 0 0 0 1rem rgba(0, 0, 0, 0.24)",
                }}
              />
            )}
            <input
              style={{ ...inputStyle, minHeight: "24rem", flex: "1 1 160rem" }}
              value={selected ? selected.name : ""}
              disabled={!selected}
              onChange={(event) => selected && send("renameSelectedParcel", event.currentTarget.value)}
              title={t("selected.rename")}
            />
          </div>
          <MergeConfirm selected={selected} target={mergeTarget} t={t} onCancel={() => setMergeTargetId(null)} />
          <div style={{ ...rowStyle, alignItems: "stretch", flexWrap: "wrap" }}>
            <PanelButton style={{ flex: "1 1 0" }} disabled={!selected} onSelect={() => send("selectNextParcel", -1)}>
              {t("action.prev")}
            </PanelButton>
            <PanelButton style={{ flex: "1 1 0" }} disabled={!selected} onSelect={() => send("selectNextParcel", 1)}>
              {t("action.next")}
            </PanelButton>
            <PanelButton tone="danger" style={{ flex: "1 1 0" }} disabled={!selected} onSelect={() => send("deleteSelectedParcel")}>
              {t("action.delete")}
            </PanelButton>
          </div>
        </div>
      </Section>

      <Section title={t("section.appearance")}>
        <AppearanceControls t={t} />
      </Section>

      <Section title={t("section.move")}>
        <div style={{ ...toolSurfaceStyle, ...rowStyle, alignItems: "center", justifyContent: "space-between", flexWrap: "wrap" }}>
          <MovePad
            disabled={!selected}
            step={step}
            labels={{
              north: t("move.north"),
              south: t("move.south"),
              west: t("move.west"),
              east: t("move.east"),
            }}
            onMove={moveSelectedParcel}
          />
          <label style={{ ...rowStyle, gap: "5rem", color: colors.muted, fontSize: "10rem", flex: "0 0 auto" }}>
            <span style={{ width: "24rem" }}>{t("move.step")}</span>
            <input
              style={{ ...inputStyle, width: "62rem", flex: "0 0 auto" }}
              type="number"
              value={step}
              min={1}
              onChange={(event) => setStep(Math.max(1, Number(event.currentTarget.value) || 1))}
            />
          </label>
        </div>
      </Section>

      <Section title={t("section.vertices")}>
        <div style={{ ...toolSurfaceStyle, ...columnStyle, gap: "6rem" }}>
          <VertexList selected={selected} t={t} />
          <div style={{ ...rowStyle, alignItems: "center", justifyContent: "space-between", flexWrap: "wrap" }}>
            <MovePad
              disabled={!selected}
              step={step}
              labels={{
                north: t("move.north"),
                south: t("move.south"),
                west: t("move.west"),
                east: t("move.east"),
              }}
              onMove={moveSelectedVertex}
            />
            <div style={{ ...rowStyle, flex: "1 1 150rem", flexWrap: "wrap", justifyContent: "flex-end" }}>
              <PanelButton disabled={!selected} style={{ flex: "1 1 74rem" }} onSelect={() => send("insertVertexAfterSelected")}>
                {t("action.insertVertex")}
              </PanelButton>
              <PanelButton
                tone="danger"
                disabled={!selected}
                style={{ flex: "1 1 74rem" }}
                onSelect={() => send("deleteSelectedVertex")}
              >
                {t("action.deleteVertex")}
              </PanelButton>
            </div>
          </div>
        </div>
      </Section>
    </div>
  );
}
