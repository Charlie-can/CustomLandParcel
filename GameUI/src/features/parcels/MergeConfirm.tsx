import React from "react";
import { send } from "bindings";
import { PanelButton } from "components/PanelButton";
import { Parcel, SelectedParcel } from "domain";
import { Translator } from "i18n";
import { colors, rowStyle } from "styles";

export function MergeConfirm({
  selected,
  target,
  t,
  onCancel,
}: {
  selected: SelectedParcel | null;
  target: Parcel | null;
  t: Translator;
  onCancel: () => void;
}): JSX.Element | null {
  if (!selected || !target) {
    return null;
  }

  return (
    <div
      style={{
        ...rowStyle,
        padding: "8rem",
        background: "rgba(238, 188, 82, 0.14)",
        border: "1rem solid rgba(238, 188, 82, 0.42)",
        borderRadius: "4rem",
      }}
    >
      <div style={{ flex: "1 1 auto", minWidth: 0, color: colors.text, fontSize: "12rem" }}>
        {t("merge.message", { selected: selected.name, target: target.name })}
      </div>
      <PanelButton
        tone="primary"
        style={{ width: "62rem", flex: "0 0 auto" }}
        onSelect={() => {
          send("mergeSelectedParcelWith", target.id);
          onCancel();
        }}
      >
        {t("action.confirm")}
      </PanelButton>
      <PanelButton tone="subtle" style={{ width: "62rem", flex: "0 0 auto" }} onSelect={onCancel}>
        {t("action.cancel")}
      </PanelButton>
    </div>
  );
}
