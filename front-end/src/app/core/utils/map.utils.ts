import { LocationDTO } from '../DTOs/location/location-dto';
import { MapCoordinates } from '../interfaces/map-Coordinates';

/** The building closest to a point (flat approximation, good enough at campus scale). */
export function nearestBuilding(
  point: MapCoordinates,
  buildings: readonly LocationDTO[],
): LocationDTO | null {
  const lngScale = Math.cos((point.lat * Math.PI) / 180);
  let best: LocationDTO | null = null;
  let bestDistance = Infinity;

  for (const b of buildings) {
    const dLat = b.coordinates.lat - point.lat;
    const dLng = (b.coordinates.lng - point.lng) * lngScale;
    const distance = dLat * dLat + dLng * dLng;
    if (distance < bestDistance) {
      best = b;
      bestDistance = distance;
    }
  }
  return best;
}

export function googleMapsUrl({ lat, lng }: MapCoordinates): string {
  return `https://www.google.com/maps?q=${lat},${lng}`;
}
