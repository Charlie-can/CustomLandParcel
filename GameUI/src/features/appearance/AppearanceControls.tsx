import React, { useCallback, useEffect, useRef, useState } from "react";
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
import { colors, columnStyle, rowStyle } from "styles";

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
  const fillOpacity = useValue(parcelFillOpacityBinding);
  const width = useValue(parcelBoundaryWidthBinding);
  const showVanilla = useValue(showVanillaUnlockedMapTileBordersBinding);
  const swatchColor = `rgba(${red}, ${green}, ${blue}, ${Math.max(0.1, opacity / 100)})`;
  const alphaValue = Math.round(opacity * 2.55);

  return (
    <div style={{ ...columnStyle, gap: "5rem" }}>
      <div
        style={{
          ...rowStyle,
          justifyContent: "space-between",
          alignItems: "center",
          minHeight: "24rem",
          padding: "0 2rem",
        }}
      >
        <label style={{ ...rowStyle, gap: "5rem", color: colors.text, fontSize: "10rem", minWidth: 0 }}>
          <input
            type="checkbox"
            checked={showVanilla}
            onChange={(event) => send("setShowVanillaUnlockedMapTileBorders", event.currentTarget.checked)}
          />
          {t("appearance.showVanilla")}
        </label>
      </div>
      <div
        style={{
          display: "flex",
          alignItems: "stretch",
          gap: "7rem",
          padding: "7rem",
          background: "rgba(8, 17, 22, 0.42)",
          border: "1rem solid rgba(170, 205, 218, 0.2)",
          borderRadius: "4rem",
        }}
      >
        <div style={{ ...columnStyle, gap: "5rem", flex: "0 0 58rem", minWidth: 0 }}>
          <div
            style={{
              width: "48rem",
              height: "48rem",
              background: swatchColor,
              border: "2rem solid rgba(235, 248, 255, 0.82)",
              borderRadius: "4rem",
              boxShadow: "inset 0 0 0 1rem rgba(0, 0, 0, 0.26), 0 4rem 12rem rgba(0, 0, 0, 0.28)",
            }}
          />
          <div style={{ ...rowStyle, flexWrap: "wrap", gap: "3rem" }}>
            {colorPresets.map((preset) => (
              <button
                key={preset.label}
                type="button"
                title={preset.label}
                onClick={() => applyColorPreset(preset)}
                style={{
                  width: "16rem",
                  height: "16rem",
                  minWidth: "16rem",
                  minHeight: "16rem",
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
        </div>
        <div style={{ ...columnStyle, gap: "4rem", flex: "1 1 auto", minWidth: 0 }}>
          <div style={{ ...rowStyle, justifyContent: "space-between", minHeight: "13rem" }}>
            <span style={{ color: colors.text, fontSize: "10rem", fontWeight: 900 }}>{t("appearance.color")}</span>
            <span style={{ color: colors.muted, fontSize: "8.5rem" }}>
              rgba({red}, {green}, {blue}, {alphaValue})
            </span>
          </div>
          <DragSlider
            label="R"
            labelWidth="12rem"
            value={red}
            min={0}
            max={255}
            trackBackground={`linear-gradient(90deg, rgb(0, ${green}, ${blue}), rgb(255, ${green}, ${blue}))`}
            onChange={(value) => setParcelAppearanceValue("ParcelBoundaryRed", value)}
          />
          <DragSlider
            label="G"
            labelWidth="12rem"
            value={green}
            min={0}
            max={255}
            trackBackground={`linear-gradient(90deg, rgb(${red}, 0, ${blue}), rgb(${red}, 255, ${blue}))`}
            onChange={(value) => setParcelAppearanceValue("ParcelBoundaryGreen", value)}
          />
          <DragSlider
            label="B"
            labelWidth="12rem"
            value={blue}
            min={0}
            max={255}
            trackBackground={`linear-gradient(90deg, rgb(${red}, ${green}, 0), rgb(${red}, ${green}, 255))`}
            onChange={(value) => setParcelAppearanceValue("ParcelBoundaryBlue", value)}
          />
          <DragSlider
            label="A"
            labelWidth="12rem"
            value={opacity}
            min={0}
            max={100}
            suffix="%"
            trackBackground={`linear-gradient(90deg, rgba(${red}, ${green}, ${blue}, 0), rgba(${red}, ${green}, ${blue}, 1))`}
            onChange={(value) => setParcelAppearanceValue("ParcelBoundaryOpacity", value)}
          />
        </div>
      </div>
      <div
        style={{
          ...columnStyle,
          gap: "4rem",
          padding: "6rem 7rem",
          background: "rgba(232, 246, 255, 0.045)",
          border: `1rem solid ${colors.border}`,
          borderRadius: "4rem",
        }}
      >
        <DragSlider
          label={t("appearance.fillOpacity")}
          value={fillOpacity}
          min={0}
          max={100}
          suffix="%"
          trackBackground={`linear-gradient(90deg, rgba(${red}, ${green}, ${blue}, 0), rgba(${red}, ${green}, ${blue}, 0.72))`}
          onChange={(value) => setParcelAppearanceValue("ParcelFillOpacity", value)}
        />
        <DragSlider
          label={t("appearance.width")}
          value={width}
          min={2}
          max={14}
          trackBackground="linear-gradient(90deg, rgba(120, 160, 176, 0.5), rgba(240, 250, 255, 0.96))"
          onChange={(value) => setParcelAppearanceValue("ParcelBoundaryWidth", value)}
        />
      </div>
    </div>
  );
}

function DragSlider({
  label,
  labelWidth = "58rem",
  value,
  min,
  max,
  suffix = "",
  trackBackground,
  onChange,
}: {
  label: string;
  labelWidth?: string;
  value: number;
  min: number;
  max: number;
  suffix?: string;
  trackBackground: string;
  onChange: (value: number) => void;
}): JSX.Element {
  const [dragging, setDragging] = useState(false);
  const trackRef = useRef<HTMLDivElement | null>(null);
  const percent = ((value - min) / Math.max(1, max - min)) * 100;

  const updateFromClientX = useCallback(
    (clientX: number, element: HTMLElement | null) => {
      if (element == null) {
        return;
      }

      const rect = element.getBoundingClientRect();
      const ratio = clamp((clientX - rect.left) / Math.max(1, rect.width), 0, 1);
      onChange(clamp(Math.round(min + ratio * (max - min)), min, max));
    },
    [max, min, onChange],
  );

  useEffect(() => {
    if (!dragging) {
      return undefined;
    }

    const handleMouseUp = () => setDragging(false);
    const handleMouseMove = (event: MouseEvent) => updateFromClientX(event.clientX, trackRef.current);
    window.addEventListener("mousemove", handleMouseMove);
    window.addEventListener("mouseup", handleMouseUp);
    return () => {
      window.removeEventListener("mousemove", handleMouseMove);
      window.removeEventListener("mouseup", handleMouseUp);
    };
  }, [dragging, updateFromClientX]);

  return (
    <div style={{ ...rowStyle, alignItems: "center", color: colors.muted, fontSize: "9rem" }}>
      <span style={{ width: labelWidth, color: colors.text, fontWeight: 800 }}>{label}</span>
      <div
        ref={trackRef}
        onMouseDown={(event) => {
          setDragging(true);
          updateFromClientX(event.clientX, event.currentTarget);
        }}
        onMouseMove={(event) => {
          if (dragging) {
            updateFromClientX(event.clientX, event.currentTarget);
          }
        }}
        style={{
          position: "relative",
          flex: "1 1 auto",
          minWidth: "92rem",
          height: "11rem",
          background: trackBackground,
          border: "1rem solid rgba(215, 232, 240, 0.42)",
          borderRadius: "3rem",
          boxShadow: "inset 0 0 0 1rem rgba(0, 0, 0, 0.22)",
        }}
      >
        <div
          style={{
            position: "absolute",
            left: `${percent}%`,
            top: "-3rem",
            width: "7rem",
            height: "16rem",
            marginLeft: "-4rem",
            background: "rgba(245, 250, 255, 0.96)",
            border: "1rem solid rgba(0, 0, 0, 0.45)",
            borderRadius: "2rem",
            boxShadow: "0 1rem 4rem rgba(0, 0, 0, 0.45)",
          }}
        />
      </div>
      <span style={{ width: "34rem", textAlign: "right", color: colors.text }}>
        {value}
        {suffix}
      </span>
    </div>
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
