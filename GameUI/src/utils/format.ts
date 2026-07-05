import { Point } from "domain";

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

export function formatArea(value: number): string {
  return `${Math.round(Number(value) || 0).toLocaleString()} m2`;
}

export function formatPoint(value: unknown): string {
  const point = asPoint(value);
  return `${Math.round(point.x)}, ${Math.round(point.y)}`;
}
