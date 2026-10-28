import { Signal } from '@angular/core';
import { toObservable, toSignal } from '@angular/core/rxjs-interop';
import { debounceTime, distinctUntilChanged, startWith } from 'rxjs';
import { environment } from '../../../environments/environment';

/**
 * A copy of `source` that follows it only after it stayed unchanged for `ms`.
 * The first value is available at once, and a value equal to the current one is not emitted again.
 * Call it where `inject()` works (a field initializer or the constructor).
 */
export function debouncedSignal<T>(
  source: Signal<T>,
  ms = environment.FilterDebounceMs,
): Signal<T> {
  const initial = source();
  return toSignal(
    toObservable(source).pipe(
      debounceTime(ms),
      startWith(initial),
      distinctUntilChanged((a, b) => JSON.stringify(a) === JSON.stringify(b)),
    ),
    { requireSync: true },
  );
}
