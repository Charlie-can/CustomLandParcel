import { bindValue, trigger } from "cs2/api";
import { Parcel } from "domain";

export const GROUP = "customLandParcel";

export const parcelsBinding = bindValue<Parcel[]>(GROUP, "parcels", []);
export const selectedParcelIdBinding = bindValue<string>(GROUP, "selectedParcelId", "");
export const selectedVertexIndexBinding = bindValue<number>(GROUP, "selectedVertexIndex", -1);
export const editToolActiveBinding = bindValue<boolean>(GROUP, "parcelEditToolActive", false);

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
