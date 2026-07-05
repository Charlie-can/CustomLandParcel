export type Point = {
  x: number;
  y: number;
};

export type Parcel = {
  id: string;
  name: string;
  state: string;
  price: number;
  area: number;
  selected: boolean;
  boundaryRed: number;
  boundaryGreen: number;
  boundaryBlue: number;
  boundaryOpacity: number;
  fillOpacity: number;
  boundaryWidth: number;
  points: Point[];
};

export type SelectedParcel = Parcel & {
  selectedVertexIndex: number;
};
