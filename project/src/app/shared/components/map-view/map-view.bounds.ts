import { MapCoordinates } from "../../../core/interfaces/map-coordinates"; 

/** where the map opens, and the zoom it opens at (16 shows the whole campus on a wide screen) */
export const CAMPUS_CENTER: MapCoordinates = { lat: 32.33306, lng: 36.24106 };
export const CAMPUS_ZOOM = 16;
/** how far the "+" / "-" buttons and the focus button can go */
export const FOCUS_ZOOM = 18;

/**
 * A box around the campus. It is only used by the "my location" button to tell
 * a student who is on campus from one who is far away, so it is generous on purpose.
 */
export const CAMPUS_BOUNDS = { north: 32.347, south: 32.333, west: 36.225, east: 36.25 } as const;

export function isInsideCampusBounds({ lat, lng }: MapCoordinates): boolean {
  return (
    lat <= CAMPUS_BOUNDS.north &&
    lat >= CAMPUS_BOUNDS.south &&
    lng >= CAMPUS_BOUNDS.west &&
    lng <= CAMPUS_BOUNDS.east
  );
}
