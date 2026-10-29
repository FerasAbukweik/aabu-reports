import { ChangeDetectionStrategy, Component, computed, DestroyRef, effect, inject, input, output, signal, untracked } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { finalize } from 'rxjs';
import { ReportDTO } from '../../../../core/DTOs/report/report-dto';
import { ReportsTableService } from '../../../../core/services/api-services/reports-table.service';
import { ToastService } from '../../../../core/services/local/toast.service';
import { DialogComponent } from '../../../../shared/components/dialog/dialog.component';
import { ReportRow } from '../../reports-history.model';

const STARS = [1, 2, 3, 4, 5] as const;

const RATING_LABEL: Record<number, string> = {
  1: 'سيئ',
  2: 'مقبول',
  3: 'جيد',
  4: 'جيد جدًا',
  5: 'ممتاز',
};

/** Rates a fixed report (PUT /report/rate). ReportsTableService updates the list, so the table follows by itself. */
@Component({
  selector: 'app-report-rating-modal',
  imports: [DialogComponent],
  templateUrl: './report-rating-modal.component.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ReportRatingModalComponent {
  private readonly reportsTable = inject(ReportsTableService);
  private readonly toast = inject(ToastService);
  private readonly destroyRef = inject(DestroyRef);

  /** the report to rate, null keeps the dialog closed */
  readonly row = input<ReportRow | null>(null);

  readonly closed = output<void>();
  /** the report was rated: the api sent the whole report back (with its rate) */
  readonly rated = output<ReportDTO>();

  protected readonly stars = STARS;
  protected readonly ratingLabel = RATING_LABEL;

  /** what the user picked, 0 = nothing yet */
  protected readonly rating = signal(0);
  /** the star under the pointer, 0 = none */
  protected readonly hovered = signal(0);
  protected readonly submitting = signal(false);

  /** a computed, so a new row object of the same report (the list changed meanwhile) does not clear the pick */
  private readonly reportId = computed(() => this.row()?.report.id);

  constructor() {
    // every time the dialog opens for a report it starts clean
    effect(() => {
      this.reportId();
      untracked(() => {
        this.rating.set(0);
        this.hovered.set(0);
      });
    });
  }

  protected submit(): void {
    const row = this.row();
    const rate = this.rating();
    if (!row || rate === 0 || this.submitting()) return;

    this.submitting.set(true);

    // ReportsTableService shows the error toast; the dialog stays open so the user can try again
    this.reportsTable
      .rate(row.report.id, rate)
      .pipe(
        finalize(() => this.submitting.set(false)),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe({
        next: (updated) => {
          this.rated.emit(updated);
          this.toast.success('شكرًا لك', `تم تسجيل تقييمك للبلاغ ${row.shortId}`);
          this.closed.emit();
        },
        error: () => {},
      });
  }
}
