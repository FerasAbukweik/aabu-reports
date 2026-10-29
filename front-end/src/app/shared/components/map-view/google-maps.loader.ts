/* eslint-disable @typescript-eslint/no-explicit-any */
import { signal } from '@angular/core';
import { GOOGLE_MAPS_API_KEY } from '../../../core/constants/google-maps';
import { GoogleMapsApi } from './map-view.model';

// Loads the Google Maps JavaScript API once, without an npm package.

declare global {
  interface Window {
    google?: any;
    __onGoogleMapsReady?: () => void;
    gm_authFailure?: () => void;
  }
}

/** becomes true when Google rejects the key (wrong key, blocked site, or the demo key's daily limit) */
export const googleMapsAuthFailed = signal(false);

let pending: Promise<GoogleMapsApi> | null = null;

export function loadGoogleMaps(): Promise<GoogleMapsApi> {
  pending ??= load().catch((error) => {
    pending = null; // let the "try again" button start over
    throw error;
  });
  return pending;
}

async function load(): Promise<GoogleMapsApi> {
  if (typeof window === 'undefined') throw new Error('Google Maps needs a browser');

  if (!window.google?.maps?.importLibrary) {
    await new Promise<void>((resolve, reject) => {
      window.__onGoogleMapsReady = () => resolve();
      window.gm_authFailure = () => googleMapsAuthFailed.set(true);

      const params = new URLSearchParams({
        key: GOOGLE_MAPS_API_KEY,
        v: 'weekly',
        loading: 'async',
        callback: '__onGoogleMapsReady',
        language: 'ar',
        region: 'JO',
      });
      const script = document.createElement('script');
      script.src = `https://maps.googleapis.com/maps/api/js?${params}`;
      script.async = true;
      script.onerror = () => {
        script.remove();
        reject(new Error('Could not load the Google Maps script'));
      };
      document.head.appendChild(script);
    });
  }

  const [maps, marker] = await Promise.all([
    window.google.maps.importLibrary('maps'),
    window.google.maps.importLibrary('marker'),
  ]);
  return {
    Map: maps.Map,
    AdvancedMarkerElement: marker.AdvancedMarkerElement,
    PinElement: marker.PinElement,
    event: window.google.maps.event,
  };
}
