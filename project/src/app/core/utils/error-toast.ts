import { HttpErrorResponse } from '@angular/common/http';
import { catchError, MonoTypeOperatorFunction, throwError } from 'rxjs';
import { ToastService } from '../services/local/toast.service';

/** Fallback reason when the service does not give its own: network down, or anything else. */
export function defaultErrorReason(error: unknown): string {
  return error instanceof HttpErrorResponse && error.status === 0
    ? 'تعذّر الاتصال بالخادم، تحقق من اتصالك بالإنترنت وحاول مرة أخرى'
    : 'حدث خطأ غير متوقع، حاول مرة أخرى لاحقًا';
}

/**
 * Shows an error toast when the request fails, then rethrows the SAME error,
 * so the caller can still stop its spinner / re-enable its form.
 *
 * @param toast  the ToastService of the calling service
 * @param title  what went wrong, specific to the call (e.g. "تعذر تحميل المواقع")
 * @param reason optional, builds the toast message from the error (status based)
 *
 * Usage inside a service:
 *   this.http.get(...).pipe(toastError(this.toast, 'تعذر تحميل المواقع'))
 */
export function toastError<T>(
  toast: ToastService,
  title: string,
  reason: (error: HttpErrorResponse) => string = defaultErrorReason,
): MonoTypeOperatorFunction<T> {
  return catchError((error) => {
    toast.error(title, reason(error));
    return throwError(() => error);
  });
}
