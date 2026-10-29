import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';

/** "968FCB28" or a whole guid (with or without the dashes), nothing else is taken for a report number */
const REPORT_NUMBER = /^[0-9a-f-]{8,36}$/i;

/**
 * Lets the number of a report be typed the way it is shown, with the "#": `/report/#968FCB28`.
 * The browser never sends what comes after the "#" as part of the path, the router only sees `/report` with the
 * fragment `968FCB28` (the map page), so the fragment is turned back into the path `/report/968FCB28` here.
 * Any other fragment (or none) leaves the map page alone.
 */
export const reportNumberFragmentGuard: CanActivateFn = (route) => {
  const fragment = route.fragment;

  return fragment && REPORT_NUMBER.test(fragment)
    ? inject(Router).createUrlTree(['/report', fragment])
    : true;
};
