import { HttpErrorResponse } from '@angular/common/http';

export type ReportAction = 'load' | 'create' | 'update' | 'rate' | 'delete';

/** The Arabic toast message of a failed report request, based on the http status. */
export function reportErrorMessage(error: HttpErrorResponse, action: ReportAction): string {
  switch (error.status) {
    case 0:
      return 'تعذّر الاتصال بالخادم، تحقق من اتصالك بالإنترنت وحاول مرة أخرى';
    case 400:
      return action === 'rate'
        ? 'لا يمكن تقييم البلاغ إلا بعد إنجاز الصيانة'
        : 'تحقق من بيانات البلاغ ثم حاول مرة أخرى';
    case 401:
    case 403:
      return action === 'load' || action === 'create' || action === 'rate'
        ? 'انتهت الجلسة أو ليس لديك صلاحية، سجّل الدخول مرة أخرى'
        : 'ليس لديك صلاحية لتنفيذ هذا الإجراء';
    case 404:
      return 'البلاغ أو الموقع غير موجود';
    default:
      return 'حدث خطأ غير متوقع، حاول مرة أخرى لاحقًا';
  }
}
