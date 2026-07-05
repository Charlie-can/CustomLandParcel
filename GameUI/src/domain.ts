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
  points: Point[];
};

export type SelectedParcel = Parcel & {
  selectedVertexIndex: number;
};
