import { ReportState } from '../../core/enums/report-state.enum';

/** the "what happens next" sentence of every state (the report has no engineer / ETA data, so it only says what the state means) */
export const NEXT_STEP: Record<ReportState, string> = {
  [ReportState.Normal]: '',
  [ReportState.Pending]: 'بانتظار استلام فريق الصيانة للبلاغ',
  [ReportState.InProgress]: 'فريق الصيانة يعمل على معالجة المشكلة الآن',
  [ReportState.Fixed]: 'تمت الصيانة',
  [ReportState.NeedsFollowUp]: 'البلاغ يحتاج إلى متابعة إضافية من فريق الصيانة',
};
