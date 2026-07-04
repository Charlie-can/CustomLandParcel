import React, { useMemo, useState } from "react";
import { bindValue, trigger, useValue } from "cs2/api";
import { ModRegistrar } from "cs2/modding";

type Point = {
  x: number;
  y: number;
};

type Parcel = {
  id: string;
  name: string;
  state: string;
  price: number;
  area: number;
  selected: boolean;
  points: Point[];
};

type SelectedParcel = Parcel & {
  selectedVertexIndex: number;
};

const GROUP = "customLandParcel";
const parcelsBinding = bindValue<Parcel[]>(GROUP, "parcels", []);
const selectedParcelIdBinding = bindValue<string>(GROUP, "selectedParcelId", "");
const selectedVertexIndexBinding = bindValue<number>(GROUP, "selectedVertexIndex", -1);
const summaryBinding = bindValue<string>(GROUP, "summary", "");
const editToolActiveBinding = bindValue<boolean>(GROUP, "parcelEditToolActive", false);

const buttonBase: React.CSSProperties = {
  minHeight: "32rem",
  padding: "0 10rem",
  color: "rgba(245, 248, 252, 0.96)",
  background: "rgba(55, 68, 82, 0.96)",
  border: "1rem solid rgba(255, 255, 255, 0.18)",
  borderRadius: "4rem",
  fontSize: "14rem",
};

const primaryButton: React.CSSProperties = {
  ...buttonBase,
  background: "rgba(37, 109, 151, 0.98)",
};

const dangerButton: React.CSSProperties = {
  ...buttonBase,
  background: "rgba(128, 48, 54, 0.98)",
};

const inputStyle: React.CSSProperties = {
  flex: "1 1 auto",
  minWidth: 0,
  height: "32rem",
  padding: "0 8rem",
  color: "rgba(245, 248, 252, 0.96)",
  background: "rgba(255, 255, 255, 0.08)",
  border: "1rem solid rgba(255, 255, 255, 0.16)",
  borderRadius: "4rem",
  fontSize: "14rem",
};

const rowStyle: React.CSSProperties = {
  display: "flex",
  alignItems: "center",
  gap: "8rem",
};

function asPoint(value: unknown): Point {
  if (!value) {
    return { x: 0, y: 0 };
  }

  if (Array.isArray(value)) {
    return { x: Number(value[0]) || 0, y: Number(value[1]) || 0 };
  }

  const point = value as Partial<Point>;
  return { x: Number(point.x) || 0, y: Number(point.y) || 0 };
}

function formatMoney(value: number): string {
  return Math.round(Number(value) || 0).toLocaleString();
}

function formatArea(value: number): string {
  return `${Math.round(Number(value) || 0).toLocaleString()} m2`;
}

function formatPoint(value: unknown): string {
  const point = asPoint(value);
  return `${Math.round(point.x)}, ${Math.round(point.y)}`;
}

function moveSelectedParcel(x: number, y: number): void {
  trigger(GROUP, "moveSelectedParcel", { x, y });
}

function moveSelectedVertex(x: number, y: number): void {
  trigger(GROUP, "moveSelectedVertex", { x, y });
}

function ParcelRow({ parcel }: { parcel: Parcel }): JSX.Element {
  const isPurchased = parcel.state === "Purchased";
  const style: React.CSSProperties = {
    display: "grid",
    gridTemplateColumns: "1fr auto",
    gap: "6rem",
    padding: "8rem",
    background: parcel.selected ? "rgba(67, 132, 177, 0.34)" : "rgba(255, 255, 255, 0.06)",
    border: parcel.selected ? "1rem solid rgba(114, 194, 255, 0.78)" : "1rem solid rgba(255, 255, 255, 0.1)",
    borderRadius: "4rem",
    textAlign: "left",
  };

  return (
    <button type="button" style={style} onClick={() => trigger(GROUP, "selectParcel", parcel.id)} title="Select parcel">
      <span style={{ overflow: "hidden", textOverflow: "ellipsis", whiteSpace: "nowrap" }}>{parcel.name || "Parcel"}</span>
      <span style={{ color: isPurchased ? "#8fe0a2" : "#f0c66d" }}>{parcel.state}</span>
      <span style={{ color: "rgba(245, 248, 252, 0.72)" }}>{formatArea(parcel.area)}</span>
      <span style={{ color: "rgba(245, 248, 252, 0.72)", textAlign: "right" }}>${formatMoney(parcel.price)}</span>
    </button>
  );
}

function VertexList({ selected }: { selected: SelectedParcel | null }): JSX.Element {
  if (!selected || !selected.points || selected.points.length === 0) {
    return <div style={{ color: "rgba(245, 248, 252, 0.62)" }}>No selected parcel.</div>;
  }

  return (
    <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: "6rem" }}>
      {selected.points.map((point, index) => (
        <button
          key={`${selected.id}-${index}`}
          type="button"
          style={{
            ...buttonBase,
            background: index === selected.selectedVertexIndex ? "rgba(37, 109, 151, 0.98)" : buttonBase.background,
            textAlign: "left",
          }}
          onClick={() => trigger(GROUP, "selectVertex", index)}
          title="Select vertex"
        >
          V{index + 1}: {formatPoint(point)}
        </button>
      ))}
    </div>
  );
}

function ParcelPanel({ onClose }: { onClose: () => void }): JSX.Element {
  const parcels = useValue(parcelsBinding) || [];
  const selectedParcelId = useValue(selectedParcelIdBinding);
  const selectedVertexIndex = useValue(selectedVertexIndexBinding);
  const summary = useValue(summaryBinding);
  const editToolActive = useValue(editToolActiveBinding);
  const [step, setStep] = useState(100);

  const selected = useMemo<SelectedParcel | null>(() => {
    const found = parcels.find((parcel) => parcel.id === selectedParcelId) || parcels.find((parcel) => parcel.selected);
    return found ? { ...found, selectedVertexIndex } : null;
  }, [parcels, selectedParcelId, selectedVertexIndex]);

  const selectedPurchased = selected?.state === "Purchased";

  return (
    <div
      style={{
        position: "absolute",
        top: "96rem",
        right: "24rem",
        width: "420rem",
        maxHeight: "760rem",
        zIndex: 10000,
        display: "flex",
        flexDirection: "column",
        gap: "10rem",
        padding: "14rem",
        color: "rgba(245, 248, 252, 0.96)",
        background: "rgba(18, 24, 31, 0.92)",
        border: "1rem solid rgba(255, 255, 255, 0.18)",
        borderRadius: "6rem",
        boxShadow: "0 12rem 38rem rgba(0, 0, 0, 0.36)",
        fontSize: "15rem",
      }}
    >
      <div style={{ ...rowStyle, justifyContent: "space-between" }}>
        <div style={{ fontSize: "18rem", fontWeight: 700 }}>Custom Land Parcel</div>
        <button type="button" style={buttonBase} onClick={onClose} title="Close panel">
          x
        </button>
      </div>

      <div style={rowStyle}>
        <button
          type="button"
          style={editToolActive ? primaryButton : buttonBase}
          onClick={() => trigger(GROUP, "setParcelEditToolActive", !editToolActive)}
          title="Toggle map parcel edit tool"
        >
          {editToolActive ? "Map Tool On" : "Map Tool Off"}
        </button>
        <button type="button" style={buttonBase} onClick={() => trigger(GROUP, "addRectangle")} title="Add a rectangular parcel">
          Add Rectangle
        </button>
      </div>

      <div style={{ color: "rgba(245, 248, 252, 0.68)", lineHeight: "20rem" }}>{summary}</div>

      <div style={{ display: "flex", flexDirection: "column", gap: "8rem" }}>
        <div style={{ fontWeight: 700 }}>Parcels</div>
        <div style={{ display: "flex", flexDirection: "column", gap: "6rem", maxHeight: "220rem", overflowY: "auto" }}>
          {parcels.map((parcel) => (
            <ParcelRow key={parcel.id} parcel={parcel} />
          ))}
        </div>
      </div>

      <div style={{ display: "flex", flexDirection: "column", gap: "8rem" }}>
        <div style={{ fontWeight: 700 }}>Selected Parcel</div>
        <div style={rowStyle}>
          <input
            style={inputStyle}
            value={selected ? selected.name : ""}
            disabled={!selected}
            onChange={(event) => selected && trigger(GROUP, "renameSelectedParcel", event.currentTarget.value)}
            title="Rename selected parcel"
          />
          <button
            type="button"
            style={primaryButton}
            disabled={!selected || selectedPurchased}
            onClick={() => trigger(GROUP, "purchaseSelectedParcel")}
            title="Purchase selected parcel"
          >
            Buy ${selected ? formatMoney(selected.price) : 0}
          </button>
        </div>

        <div style={rowStyle}>
          <button type="button" style={buttonBase} disabled={!selected} onClick={() => trigger(GROUP, "selectNextParcel", -1)}>
            Prev
          </button>
          <button type="button" style={buttonBase} disabled={!selected} onClick={() => trigger(GROUP, "selectNextParcel", 1)}>
            Next
          </button>
          <button type="button" style={dangerButton} disabled={!selected} onClick={() => trigger(GROUP, "deleteSelectedParcel")}>
            Delete
          </button>
        </div>
      </div>

      <div style={{ display: "flex", flexDirection: "column", gap: "8rem" }}>
        <div style={{ ...rowStyle, justifyContent: "space-between" }}>
          <span style={{ fontWeight: 700 }}>Move</span>
          <label style={{ ...rowStyle, color: "rgba(245, 248, 252, 0.72)" }}>
            Step
            <input
              style={{ ...inputStyle, width: "84rem", flex: "0 0 auto" }}
              type="number"
              value={step}
              min={1}
              onChange={(event) => setStep(Math.max(1, Number(event.currentTarget.value) || 1))}
            />
          </label>
        </div>

        <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr 1fr", gap: "6rem" }}>
          <span />
          <button type="button" style={buttonBase} disabled={!selected} onClick={() => moveSelectedParcel(0, step)}>
            Up
          </button>
          <span />
          <button type="button" style={buttonBase} disabled={!selected} onClick={() => moveSelectedParcel(-step, 0)}>
            Left
          </button>
          <button type="button" style={buttonBase} disabled={!selected} onClick={() => moveSelectedParcel(0, -step)}>
            Down
          </button>
          <button type="button" style={buttonBase} disabled={!selected} onClick={() => moveSelectedParcel(step, 0)}>
            Right
          </button>
        </div>
      </div>

      <div style={{ display: "flex", flexDirection: "column", gap: "8rem" }}>
        <div style={{ fontWeight: 700 }}>Vertices</div>
        <VertexList selected={selected} />
        <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr 1fr 1fr", gap: "6rem" }}>
          <button type="button" style={buttonBase} disabled={!selected} onClick={() => moveSelectedVertex(0, step)}>
            V Up
          </button>
          <button type="button" style={buttonBase} disabled={!selected} onClick={() => moveSelectedVertex(0, -step)}>
            V Down
          </button>
          <button type="button" style={buttonBase} disabled={!selected} onClick={() => moveSelectedVertex(-step, 0)}>
            V Left
          </button>
          <button type="button" style={buttonBase} disabled={!selected} onClick={() => moveSelectedVertex(step, 0)}>
            V Right
          </button>
        </div>
        <div style={rowStyle}>
          <button type="button" style={buttonBase} disabled={!selected} onClick={() => trigger(GROUP, "insertVertexAfterSelected")}>
            Insert Vertex
          </button>
          <button type="button" style={dangerButton} disabled={!selected} onClick={() => trigger(GROUP, "deleteSelectedVertex")}>
            Delete Vertex
          </button>
        </div>
      </div>
    </div>
  );
}

function CustomLandParcelRoot(): JSX.Element {
  const [open, setOpen] = useState(false);

  if (!open) {
    return (
      <div style={{ position: "absolute", top: "96rem", right: "24rem", zIndex: 9999 }}>
        <button type="button" style={primaryButton} onClick={() => setOpen(true)} title="Open Custom Land Parcel panel">
          Land Parcels
        </button>
      </div>
    );
  }

  return <ParcelPanel onClose={() => setOpen(false)} />;
}

const register: ModRegistrar = (moduleRegistry) => {
  console.log("[CustomLandParcelUI] registering game panel.");
  moduleRegistry.append("Game", CustomLandParcelRoot);
};

export default register;
