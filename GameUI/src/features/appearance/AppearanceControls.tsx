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
import { colors, columnStyle, rowStyle, swatchStyle } from "styles";

const appearanceFields = [
  { key: "ParcelBoundaryRed", label: "appearance.red", min: 0, max: 255, binding: parcelBoundaryRedBinding },
  { key: "ParcelBoundaryGreen", label: "appearance.green", min: 0, max: 255, binding: parcelBoundaryGreenBinding },
  { key: "ParcelBoundaryBlue", label: "appearance.blue", min: 0, max: 255, binding: parcelBoundaryBlueBinding },
  { key: "ParcelBoundaryOpacity", label: "appearance.borderOpacity", min: 0, max: 100, binding: parcelBoundaryOpacityBinding },
  { key: "ParcelFillOpacity", label: "appearance.fillOpacity", min: 0, max: 100, binding: parcelFillOpacityBinding },
  { key: "ParcelBoundaryWidth", label: "appearance.width", min: 2, max: 14, binding: parcelBoundaryWidthBinding },
] as const;

type AppearanceFieldConfig = (typeof appearanceFields)[number];

const colorPresets = [
  { label: "Mint", red: 51, green: 255, blue: 148 },
  { label: "Sky", red: 74, green: 180, blue: 255 },
  { label: "Amber", red: 255, green: 196, blue: 72 },
  { label: "Rose", red: 255, green: 112, blue: 128 },
  { label: "White", red: 240, green: 245, blue: 245 },
] as const;

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
      <div style={{ ...rowStyle, flexWrap: "wrap", alignItems: "center", gap: "4rem" }}>
        {colorPresets.map((preset) => (
          <button
            key={preset.label}
            type="button"
            title={preset.label}
            onClick={() => applyColorPreset(preset)}
            style={{
              width: "18rem",
              height: "18rem",
              minWidth: "18rem",
              minHeight: "18rem",
              padding: "0rem",
              border: isActivePreset(preset, red, green, blue)
                ? "2rem solid rgba(240, 250, 255, 0.96)"
                : "1rem solid rgba(150, 182, 197, 0.58)",
              background: `rgb(${preset.red}, ${preset.green}, ${preset.blue})`,
              borderRadius: "3rem",
            }}
          />
        ))}
      </div>
      <div style={{ ...columnStyle, gap: "4rem" }}>
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
    <label style={{ ...rowStyle, alignItems: "center", color: colors.muted, fontSize: "10rem" }}>
      <span style={{ width: "58rem" }}>{t(field.label as TranslationKey)}</span>
      <input
        type="range"
        min={field.min}
        max={field.max}
        value={value}
        step={1}
        onChange={(event) =>
          setParcelAppearanceValue(field.key, clamp(Number(event.currentTarget.value) || 0, field.min, field.max))
        }
        style={{
          flex: "1 1 auto",
          minWidth: "92rem",
          height: "12rem",
        }}
      />
      <span style={{ width: "28rem", textAlign: "right", color: colors.text }}>{value}</span>
    </label>
  );
}

function applyColorPreset(preset: (typeof colorPresets)[number]): void {
  setParcelAppearanceValue("ParcelBoundaryRed", preset.red);
  setParcelAppearanceValue("ParcelBoundaryGreen", preset.green);
  setParcelAppearanceValue("ParcelBoundaryBlue", preset.blue);
}

function isActivePreset(preset: (typeof colorPresets)[number], red: number, green: number, blue: number): boolean {
  return preset.red === red && preset.green === green && preset.blue === blue;
}

function clamp(value: number, min: number, max: number): number {
  return Math.max(min, Math.min(max, value));
}
