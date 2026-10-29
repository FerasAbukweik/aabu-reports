import { ChangeDetectionStrategy, Component, computed, DestroyRef, effect, inject, input, output, signal, untracked } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { finalize } from 'rxjs';
import { ReportDTO } from '../../../../core/DTOs/report/report-dto';
import { ReportState } from '../../../../core/enums/report-state.enum';
import { TableReportRow } from '../../../../core/interfaces/table-row';
import { ReportsTableService } from '../../../../core/services/api-services/reports-table.service';
import { ToastService } from '../../../../core/services/local/toast.service';
import { locationStatusLabel, locationStatusTone } from '../../../../core/utils/location-util';
import { DialogComponent } from '../../../../shared/components/dialog/dialog.component';

/** the states an admin can move a report to (a report is never "normal") */
const REPORT_STATES: readonly ReportState[] = [
  ReportState.Pending,
  ReportState.InProgress,
  ReportState.Fixed,
  ReportState.NeedsFollowUp,
];

/** Changes the state of a report (PUT /report/state, admin only). ReportsTableService updates the list, so the table follows by itself. */
@Component({
  selector: 'app-report-state-modal',
  imports: [DialogComponent],
  templateUrl: './report-state-modal.component.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ReportStateModalComponent {
  private readonly reportsTable = inject(ReportsTableService);
  private readonly toast = inject(ToastService);
  private readonly destroyRef = inject(DestroyRef);

  /** the report to change, null keeps the dialog closed */
  readonly row = input<TableReportRow | null>(null);

  readonly closed = output<void>();
  /** the state was changed: the api sent the whole report back */
  readonly updated = output<ReportDTO>();

  protected readonly states = REPORT_STATES;
  protected readonly stateLabel = locationStatusLabel;
  protected readonly stateTone = locationStatusTone;

  /** what the admin picked, starts from the current state of the report */
  protected readonly selected = signal<ReportState | null>(null);
  protected readonly submitting = signal(false);

  /** a computed, so a new row object of the same report (the list changed meanwhile) does not clear the pick */
  private readonly reportId = computed(() => this.row()?.report.id);

  protected readonly isUnchanged = computed(() => this.selected() === this.row()?.state);

  constructor() {
    // every time the dialog opens for a report it starts from the current state
    effect(() => {
      this.reportId();
      untracked(() => this.selected.set(this.row()?.state ?? null));
    });
  }

  protected submit(): void {
    const row = this.row();
    const state = this.selected();
    if (!row || state === null || this.isUnchanged() || this.submitting()) return;

    this.submitting.set(true);

    // ReportsTableService shows the error toast; the dialog stays open so the admin can try again
    this.reportsTable
      .updateState(row.report.id, state)
      .pipe(
        finalize(() => this.submitting.set(false)),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe({
        next: (updated) => {
          this.updated.emit(updated);
          this.toast.success('تم التحديث', `تم تحديث حالة البلاغ ${row.shortId}`);
          this.closed.emit();
        },
        error: () => {},
      });
  }
}
