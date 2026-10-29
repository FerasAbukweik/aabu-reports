import { DecimalPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject, input, signal, viewChild } from '@angular/core';
import { takeUntilDestroyed, toObservable } from '@angular/core/rxjs-interop';
import { RouterLink } from '@angular/router';
import { catchError, EMPTY, switchMap, tap } from 'rxjs';
import { ReportDTO } from '../../core/DTOs/report/report-dto';
import { ReportState } from '../../core/enums/report-state.enum';
import { ReportSeverity } from '../../core/enums/report-severity.enum';
import { ReportsTableService } from '../../core/services/api-services/reports-table.service';
import { ToastService } from '../../core/services/local/toast.service';
import { formatFullDate } from '../../core/utils/date-format';
import { locationStatusLabel, locationStatusTone } from '../../core/utils/location-util';
import { googleMapsUrl } from '../../core/utils/map.utils';
import { reportCategoryIcon, reportCategoryLabel } from '../../core/utils/report-category-utils';
import { reportSeverityLabel, reportSeverityTone } from '../../core/utils/report-severity-utils';
import { DialogComponent } from '../../shared/components/dialog/dialog.component';
import { StarRatingComponent } from '../../shared/components/star-rating/star-rating.component';
import { ReportRatingModalComponent } from '../reports-history/components/report-rating-modal/report-rating-modal.component';
import { toReportTableRow } from '../../core/utils/table-utils';
import { ReportChatComponent } from './components/report-chat/report-chat.component';
import { NEXT_STEP } from './report-details.data';
import { ProgressStep } from './report-details.model';

/**
 * One report with everything the api knows about it (GET /report/{id}).
 * Route: `report/:id`, where `:id` is the guid or the short number of the report ("968FCB28", the backend resolves both). The `id` route param is bound to the input (withComponentInputBinding),
 * and the report is fetched again whenever it changes.
 */
@Component({
  selector: 'app-report-details',
  imports: [DecimalPipe, RouterLink, DialogComponent, StarRatingComponent, ReportRatingModalComponent, ReportChatComponent],
  templateUrl: './report-details.component.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ReportDetailsComponent {
  private readonly reportsTable = inject(ReportsTableService);
  private readonly toast = inject(ToastService);

  /** the `:id` of the route */
  readonly id = input.required<string>();

  /** the conversation card, the header button focuses its box */
  protected readonly chat = viewChild(ReportChatComponent);

  // state
  protected readonly report = signal<ReportDTO | null>(null);
  protected readonly isLoading = signal(true);
  protected readonly hasFailed = signal(false);
  /** bumped by the retry button, so the same id is requested again */
  private readonly reloadTick = signal(0);

  protected readonly ratingOpen = signal(false);
  /** the url of the image shown big, null keeps the lightbox closed */
  protected readonly lightboxUrl = signal<string | null>(null);

  // template helpers
  protected readonly ReportState = ReportState;
  protected readonly ReportSeverity = ReportSeverity;
  protected readonly stateLabel = locationStatusLabel;
  protected readonly stateTone = locationStatusTone;
  protected readonly categoryLabel = reportCategoryLabel;
  protected readonly categoryIcon = reportCategoryIcon;
  protected readonly severityLabel = reportSeverityLabel;
  protected readonly severityTone = reportSeverityTone;
  protected readonly googleMapsUrl = googleMapsUrl;
  protected readonly formatFullDate = formatFullDate;

  /** the report with its enums read safely (same shape the history table uses) */
  protected readonly row = computed(() => {
    const report = this.report();
    return report ? toReportTableRow(report) : null;
  });

  /** the row of the rating dialog, null keeps it closed */
  protected readonly ratingRow = computed(() => (this.ratingOpen() ? this.row() : null));

  protected readonly nextStep = computed(() => {
    const row = this.row();
    if (!row) return '';
    if (row.state === ReportState.Fixed) {
      return row.rate === null ? 'تمت الصيانة، يرجى تقييم الخدمة' : 'تم إغلاق البلاغ، شكرًا لتقييمك';
    }
    return NEXT_STEP[row.state];
  });

  /** received -> in progress -> fixed -> rated, with the state of the report deciding where it stands */
  protected readonly steps = computed<ProgressStep[]>(() => {
    const row = this.row();
    if (!row) return [];

    const received: ProgressStep = {
      label: 'تم استلام البلاغ',
      icon: 'inbox',
      status: 'done',
      note: formatFullDate(row.report.reportDate),
    };
    const working: ProgressStep = { label: 'قيد المعالجة', icon: 'build', status: 'upcoming' };
    const fixed: ProgressStep = { label: 'تمت الصيانة', icon: 'task_alt', status: 'upcoming' };
    const rated: ProgressStep = { label: 'تقييم الخدمة', icon: 'star', status: 'upcoming' };

    switch (row.state) {
      case ReportState.InProgress:
        working.status = 'current';
        break;
      case ReportState.NeedsFollowUp:
        working.status = 'done';
        fixed.status = 'warning';
        fixed.label = 'تحتاج متابعة';
        break;
      case ReportState.Fixed:
        working.status = 'done';
        fixed.status = 'done';
        rated.status = row.rate === null ? 'current' : 'done';
        break;
    }

    return [received, working, fixed, rated];
  });

  /** how far the line between the steps is filled (0-100) */
  protected readonly progress = computed(() => {
    const steps = this.steps();
    const reached = steps.reduce((last, step, i) => (step.status === 'upcoming' ? last : i), 0);

    return steps.length > 1 ? (reached / (steps.length - 1)) * 100 : 0;
  });

  constructor() {
    toObservable(computed(() => ({ id: this.id(), tick: this.reloadTick() })))
      .pipe(
        tap(() => {
          this.isLoading.set(true);
          this.hasFailed.set(false);
          this.report.set(null);
        }),
        switchMap(({ id }) =>
          // ReportsTableService already shows the error toast
          this.reportsTable.getById(id).pipe(
            catchError(() => {
              this.hasFailed.set(true);
              this.isLoading.set(false);
              return EMPTY;
            }),
          ),
        ),
        takeUntilDestroyed(),
      )
      .subscribe((report) => {
        this.report.set(report);
        this.isLoading.set(false);
      });
  }

  // ---------- actions ----------
  protected retry(): void {
    this.reloadTick.update((n) => n + 1);
  }

  protected copyNumber(): void {
    const shortId = this.row()?.shortId;
    if (!shortId) return;

    navigator.clipboard.writeText(shortId).then(
      () => this.toast.success('تم النسخ', `تم نسخ رقم البلاغ ${shortId}`),
      () => this.toast.error('تعذر النسخ', 'انسخ الرقم يدويًا'),
    );
  }

  protected share(): void {
    const shortId = this.row()?.shortId ?? '';
    const url = window.location.href;

    if (navigator.share) {
      navigator.share({ title: `بلاغ ${shortId} - جامعة آل البيت`, url }).catch(() => {});
      return;
    }

    navigator.clipboard.writeText(url).then(
      () => this.toast.success('تم النسخ', 'تم نسخ رابط البلاغ'),
      () => this.toast.error('تعذر النسخ', 'انسخ الرابط من شريط العنوان'),
    );
  }

  protected print(): void {
    window.print();
  }

  /** the rating dialog rated the report: the api sent the whole report back */
  protected onRated(updated: ReportDTO): void {
    this.report.set(updated);
  }
}
