import React, { useMemo, useState } from "react";
import { bindValue, trigger, useValue } from "cs2/api";
import { ModRegistrar } from "cs2/modding";
import { Button } from "cs2/ui";

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
  background: "rgba(45, 55, 66, 0.96)",
  border: "1rem solid rgba(255, 255, 255, 0.18)",
  borderRadius: "4rem",
  fontSize: "13rem",
  fontWeight: 600,
};

const primaryButton: React.CSSProperties = {
  ...buttonBase,
  background: "rgba(31, 111, 154, 0.98)",
};

const dangerButton: React.CSSProperties = {
  ...buttonBase,
  background: "rgba(128, 48, 54, 0.98)",
};

const subtleButton: React.CSSProperties = {
  ...buttonBase,
  background: "rgba(255, 255, 255, 0.08)",
};

const launcherButton: React.CSSProperties = {
  ...primaryButton,
  width: "44rem",
  height: "44rem",
  minHeight: "44rem",
  padding: 0,
  borderRadius: "5rem",
  display: "flex",
  alignItems: "center",
  justifyContent: "center",
  fontSize: "15rem",
  fontWeight: 800,
  letterSpacing: 0,
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
  const selectStyle: React.CSSProperties = {
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
    <div style={{ display: "grid", gridTemplateColumns: "1fr 70rem", gap: "6rem" }}>
      <Button style={selectStyle} onSelect={() => trigger(GROUP, "selectParcel", parcel.id)} tooltipLabel="Select parcel">
        <span style={{ overflow: "hidden", textOverflow: "ellipsis", whiteSpace: "nowrap" }}>{parcel.name || "Parcel"}</span>
        <span style={{ color: isPurchased ? "#8fe0a2" : "#f0c66d" }}>{parcel.state}</span>
        <span style={{ color: "rgba(245, 248, 252, 0.72)" }}>{formatArea(parcel.area)}</span>
        <span style={{ color: "rgba(245, 248, 252, 0.72)", textAlign: "right" }}>${formatMoney(parcel.price)}</span>
      </Button>
      <Button
        style={parcel.selected ? subtleButton : buttonBase}
        disabled={parcel.selected}
        onSelect={() => trigger(GROUP, "mergeSelectedParcelWith", parcel.id)}
        tooltipLabel="Merge this parcel into the selected parcel"
      >
        Merge
      </Button>
    </div>
  );
}

function VertexList({ selected }: { selected: SelectedParcel | null }): JSX.Element {
  if (!selected || !selected.points || selected.points.length === 0) {
    return <div style={{ color: "rgba(245, 248, 252, 0.62)" }}>No selected parcel.</div>;
  }

  return (
    <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: "6rem" }}>
      {selected.points.map((point, index) => (
        <Button
          key={`${selected.id}-${index}`}
          style={{
            ...buttonBase,
            background: index === selected.selectedVertexIndex ? "rgba(37, 109, 151, 0.98)" : buttonBase.background,
            textAlign: "left",
          }}
          onSelect={() => trigger(GROUP, "selectVertex", index)}
          tooltipLabel="Select vertex"
        >
          V{index + 1}: {formatPoint(point)}
        </Button>
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
        left: "24rem",
        width: "438rem",
        maxHeight: "760rem",
        zIndex: 10000,
        display: "flex",
        flexDirection: "column",
        gap: "12rem",
        padding: "12rem",
        color: "rgba(245, 248, 252, 0.96)",
        background: "rgba(18, 24, 31, 0.94)",
        border: "1rem solid rgba(255, 255, 255, 0.18)",
        borderRadius: "5rem",
        boxShadow: "0 12rem 38rem rgba(0, 0, 0, 0.36)",
        fontSize: "14rem",
      }}
    >
      <div style={{ ...rowStyle, justifyContent: "space-between", minHeight: "34rem" }}>
        <div>
          <div style={{ fontSize: "17rem", fontWeight: 800 }}>Custom Land Parcel</div>
          <div style={{ color: "rgba(245, 248, 252, 0.58)", fontSize: "12rem" }}>{summary}</div>
        </div>
        <Button style={{ ...subtleButton, width: "34rem", padding: 0 }} onSelect={onClose} tooltipLabel="Close panel">
          x
        </Button>
      </div>

      <div style={{ ...rowStyle, padding: "8rem", background: "rgba(255, 255, 255, 0.05)", borderRadius: "4rem" }}>
        <Button
          style={editToolActive ? primaryButton : buttonBase}
          onSelect={() => trigger(GROUP, "setParcelEditToolActive", !editToolActive)}
          tooltipLabel="Toggle map parcel edit tool"
        >
          {editToolActive ? "Map Edit On" : "Map Edit Off"}
        </Button>
        <Button style={buttonBase} onSelect={() => trigger(GROUP, "addRectangle")} tooltipLabel="Add a rectangular parcel">
          Rectangle
        </Button>
      </div>

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
          <Button
            style={primaryButton}
            disabled={!selected || selectedPurchased}
            onSelect={() => trigger(GROUP, "purchaseSelectedParcel")}
            tooltipLabel="Purchase selected parcel"
          >
            Buy ${selected ? formatMoney(selected.price) : 0}
          </Button>
        </div>

        <div style={rowStyle}>
          <Button style={buttonBase} disabled={!selected} onSelect={() => trigger(GROUP, "selectNextParcel", -1)}>
            Prev
          </Button>
          <Button style={buttonBase} disabled={!selected} onSelect={() => trigger(GROUP, "selectNextParcel", 1)}>
            Next
          </Button>
          <Button style={dangerButton} disabled={!selected} onSelect={() => trigger(GROUP, "deleteSelectedParcel")}>
            Delete
          </Button>
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
          <Button style={buttonBase} disabled={!selected} onSelect={() => moveSelectedParcel(0, step)}>
            Up
          </Button>
          <span />
          <Button style={buttonBase} disabled={!selected} onSelect={() => moveSelectedParcel(-step, 0)}>
            Left
          </Button>
          <Button style={buttonBase} disabled={!selected} onSelect={() => moveSelectedParcel(0, -step)}>
            Down
          </Button>
          <Button style={buttonBase} disabled={!selected} onSelect={() => moveSelectedParcel(step, 0)}>
            Right
          </Button>
        </div>
      </div>

      <div style={{ display: "flex", flexDirection: "column", gap: "8rem" }}>
        <div style={{ fontWeight: 700 }}>Vertices</div>
        <VertexList selected={selected} />
        <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr 1fr 1fr", gap: "6rem" }}>
          <Button style={buttonBase} disabled={!selected} onSelect={() => moveSelectedVertex(0, step)}>
            V Up
          </Button>
          <Button style={buttonBase} disabled={!selected} onSelect={() => moveSelectedVertex(0, -step)}>
            V Down
          </Button>
          <Button style={buttonBase} disabled={!selected} onSelect={() => moveSelectedVertex(-step, 0)}>
            V Left
          </Button>
          <Button style={buttonBase} disabled={!selected} onSelect={() => moveSelectedVertex(step, 0)}>
            V Right
          </Button>
        </div>
        <div style={rowStyle}>
          <Button style={buttonBase} disabled={!selected} onSelect={() => trigger(GROUP, "insertVertexAfterSelected")}>
            Insert Vertex
          </Button>
          <Button style={dangerButton} disabled={!selected} onSelect={() => trigger(GROUP, "deleteSelectedVertex")}>
            Delete Vertex
          </Button>
        </div>
      </div>
    </div>
  );
}

function CustomLandParcelRoot(): JSX.Element {
  const [open, setOpen] = useState(false);

  return (
    <>
      <Button style={launcherButton} selected={open} onSelect={() => setOpen(!open)} tooltipLabel="Open Custom Land Parcel panel">
        LP
      </Button>
      {open && <ParcelPanel onClose={() => setOpen(false)} />}
    </>
  );
}

const register: ModRegistrar = (moduleRegistry) => {
  console.log("[CustomLandParcelUI] registering top-left game panel launcher.");
  moduleRegistry.append("GameTopLeft", CustomLandParcelRoot);
};

export default register;
