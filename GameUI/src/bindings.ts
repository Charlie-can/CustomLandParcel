import { bindValue, trigger } from "cs2/api";
import { Parcel } from "domain";

export const GROUP = "customLandParcel";

export const parcelsBinding = bindValue<Parcel[]>(GROUP, "parcels", []);
export const selectedParcelIdBinding = bindValue<string>(GROUP, "selectedParcelId", "");
export const selectedVertexIndexBinding = bindValue<number>(GROUP, "selectedVertexIndex", -1);
export const editToolActiveBinding = bindValue<boolean>(GROUP, "parcelEditToolActive", false);
export const activeLocaleBinding = bindValue<string>(GROUP, "activeLocale", "en-US");
export const showVanillaUnlockedMapTileBordersBinding = bindValue<boolean>(
  GROUP,
  "showVanillaUnlockedMapTileBorders",
  true,
);
export const parcelBoundaryRedBinding = bindValue<number>(GROUP, "parcelBoundaryRed", 51);
export const parcelBoundaryGreenBinding = bindValue<number>(GROUP, "parcelBoundaryGreen", 255);
export const parcelBoundaryBlueBinding = bindValue<number>(GROUP, "parcelBoundaryBlue", 148);
export const parcelBoundaryOpacityBinding = bindValue<number>(GROUP, "parcelBoundaryOpacity", 90);
export const parcelFillOpacityBinding = bindValue<number>(GROUP, "parcelFillOpacity", 28);
export const parcelBoundaryWidthBinding = bindValue<number>(GROUP, "parcelBoundaryWidth", 7);

export function send(command: string, payload?: unknown): void {
  if (payload === undefined) {
    trigger(GROUP, command);
    return;
  }

  trigger(GROUP, command, payload);
}

export function moveSelectedParcel(x: number, y: number): void {
  send("moveSelectedParcel", { x, y });
}

export function moveSelectedVertex(x: number, y: number): void {
  send("moveSelectedVertex", { x, y });
}

export function setParcelAppearanceValue(key: string, value: number): void {
  trigger(GROUP, "setParcelAppearanceValue", key, value);
}
