import { ChangeDetectionStrategy, Component, DestroyRef, inject, input, output, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { finalize } from 'rxjs';
import { TableReportRow } from '../../../../core/interfaces/table-row';
import { ReportsTableService } from '../../../../core/services/api-services/reports-table.service';
import { ToastService } from '../../../../core/services/local/toast.service';
import { DialogComponent } from '../../../../shared/components/dialog/dialog.component';

/** Asks the admin to confirm, then deletes the report (DELETE /report/{id}, admin only). ReportsTableService removes it from the list. */
@Component({
  selector: 'app-report-delete-modal',
  imports: [DialogComponent],
  templateUrl: './report-delete-modal.component.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ReportDeleteModalComponent {
  private readonly reportsTable = inject(ReportsTableService);
  private readonly toast = inject(ToastService);
  private readonly destroyRef = inject(DestroyRef);

  /** the report to delete, null keeps the dialog closed */
  readonly row = input<TableReportRow | null>(null);

  readonly closed = output<void>();
  /** the report was deleted */
  readonly deleted = output<void>();

  protected readonly submitting = signal(false);

  protected confirm(): void {
    const row = this.row();
    if (!row || this.submitting()) return;

    this.submitting.set(true);

    // ReportsTableService shows the error toast; the dialog stays open so the admin can try again
    this.reportsTable
      .delete(row.report.id)
      .pipe(
        finalize(() => this.submitting.set(false)),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe({
        next: () => {
          this.toast.success('تم الحذف', `تم حذف البلاغ ${row.shortId}`);
          this.deleted.emit();
          this.closed.emit();
        },
        error: () => {},
      });
  }
}
