import React from "react";
import { useValue } from "cs2/api";
import {
  parcelBoundaryBlueBinding,
  parcelBoundaryGreenBinding,
  parcelBoundaryOpacityBinding,
  parcelBoundaryRedBinding,
  parcelBoundaryWidthBinding,
  parcelFillOpacityBinding,
  send,
  setParcelAppearanceValue,
  showVanillaUnlockedMapTileBordersBinding,
} from "bindings";
import { Translator } from "i18n";
import { TranslationKey } from "i18n/translations";
import { colors, compactInputStyle, columnStyle, rowStyle, swatchStyle } from "styles";

const appearanceFields = [
  { key: "ParcelBoundaryRed", label: "appearance.red", min: 0, max: 255, binding: parcelBoundaryRedBinding },
  { key: "ParcelBoundaryGreen", label: "appearance.green", min: 0, max: 255, binding: parcelBoundaryGreenBinding },
  { key: "ParcelBoundaryBlue", label: "appearance.blue", min: 0, max: 255, binding: parcelBoundaryBlueBinding },
  { key: "ParcelBoundaryOpacity", label: "appearance.borderOpacity", min: 0, max: 100, binding: parcelBoundaryOpacityBinding },
  { key: "ParcelFillOpacity", label: "appearance.fillOpacity", min: 0, max: 100, binding: parcelFillOpacityBinding },
  { key: "ParcelBoundaryWidth", label: "appearance.width", min: 2, max: 14, binding: parcelBoundaryWidthBinding },
] as const;

type AppearanceFieldConfig = (typeof appearanceFields)[number];

export function AppearanceControls({ t }: { t: Translator }): JSX.Element {
  const red = useValue(parcelBoundaryRedBinding);
  const green = useValue(parcelBoundaryGreenBinding);
  const blue = useValue(parcelBoundaryBlueBinding);
  const opacity = useValue(parcelBoundaryOpacityBinding);
  const showVanilla = useValue(showVanillaUnlockedMapTileBordersBinding);
  const swatchColor = `rgba(${red}, ${green}, ${blue}, ${Math.max(0.1, opacity / 100)})`;

  return (
    <div style={{ ...columnStyle, gap: "6rem" }}>
      <div style={{ ...rowStyle, justifyContent: "space-between", alignItems: "center" }}>
        <label style={{ ...rowStyle, gap: "5rem", color: colors.text, fontSize: "10rem" }}>
          <input
            type="checkbox"
            checked={showVanilla}
            onChange={(event) => send("setShowVanillaUnlockedMapTileBorders", event.currentTarget.checked)}
          />
          {t("appearance.showVanilla")}
        </label>
        <div style={swatchStyle(swatchColor)} title={t("appearance.color")} />
      </div>
      <div style={{ ...rowStyle, flexWrap: "wrap", alignItems: "center", gap: "5rem" }}>
        {appearanceFields.map((field) => (
          <AppearanceField key={field.key} field={field} t={t} />
        ))}
      </div>
    </div>
  );
}

function AppearanceField({ field, t }: { field: AppearanceFieldConfig; t: Translator }): JSX.Element {
  const value = useValue(field.binding);
  return (
    <label style={{ ...rowStyle, flex: "0 0 auto", gap: "3rem", color: colors.muted, fontSize: "10rem" }}>
      <span>{t(field.label as TranslationKey)}</span>
      <input
        style={compactInputStyle}
        type="number"
        min={field.min}
        max={field.max}
        value={value}
        onChange={(event) =>
          setParcelAppearanceValue(field.key, clamp(Number(event.currentTarget.value) || 0, field.min, field.max))
        }
      />
    </label>
  );
}

function clamp(value: number, min: number, max: number): number {
  return Math.max(min, Math.min(max, value));
}
