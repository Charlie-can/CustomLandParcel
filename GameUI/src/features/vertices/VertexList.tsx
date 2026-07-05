import React from "react";
import { send } from "bindings";
import { PanelButton } from "components/PanelButton";
import { SelectedParcel } from "domain";
import { Translator } from "i18n";
import { formatPoint } from "utils/format";
import { colors } from "styles";

export function VertexList({ selected, t }: { selected: SelectedParcel | null; t: Translator }): JSX.Element {
  if (!selected || !selected.points || selected.points.length === 0) {
    return <div style={{ color: colors.muted, fontSize: "11rem" }}>{t("vertices.empty")}</div>;
  }

  return (
    <div style={{ display: "flex", flexWrap: "wrap", alignItems: "flex-start", gap: "3rem" }}>
      {selected.points.map((point, index) => (
        <PanelButton
          key={`${selected.id}-${index}`}
          active={index === selected.selectedVertexIndex}
          style={{
            justifyContent: "flex-start",
            flex: "0 1 auto",
            minHeight: "22rem",
            padding: "0 7rem",
            fontSize: "10rem",
            background: index === selected.selectedVertexIndex ? colors.primary : "rgba(232, 246, 255, 0.07)",
          }}
          tooltipLabel={t("tooltip.selectVertex")}
          onSelect={() => send("selectVertex", index)}
        >
          V{index + 1}: {formatPoint(point)}
        </PanelButton>
      ))}
    </div>
  );
}
