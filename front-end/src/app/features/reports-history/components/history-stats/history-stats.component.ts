import { ChangeDetectionStrategy, Component, computed, DestroyRef, inject, input, OnInit } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { Status } from '../../../../core/enums/status.enum';
import { ReportsTableService } from '../../../../core/services/api-services/reports-table.service';
import { StatCardsComponent } from '../../../../shared/components/stat-card/stat-cards.component';
import { StatCard } from '../../../../shared/components/stat-card/stat-card.model';

/**
 * The stats cards of the reports history. They load their own numbers (the numbers stay empty until the request is done).
 * The history stats live in ReportsTableService, the maintenance stats of the map page are not used here.
 */
@Component({
  selector: 'app-history-stats',
  imports: [StatCardsComponent],
  template: `<app-stat-cards [cards]="cards()" ariaLabel="إحصائيات البلاغات والصيانة" />`,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class HistoryStatsComponent implements OnInit {
  private readonly reportsTable = inject(ReportsTableService);
  private readonly destroyRef = inject(DestroyRef);

  /** the admin page shows the numbers of everyone's reports */
  readonly isAdmin = input(false);

  private readonly stats = computed(() =>
    this.isAdmin() ? this.reportsTable.allStats() : this.reportsTable.myStats(),
  );

  ngOnInit(): void {
    this.reportsTable
      .loadStats(this.isAdmin())
      .pipe(takeUntilDestroyed(this.destroyRef))
      // ReportsTableService already shows the error toast, nothing else to do on failure
      .subscribe({ error: () => {} });
  }

  protected readonly cards = computed<StatCard[]>(() => {
    const s = this.stats();
    const dash = '—';
    const completionRate =
      s && s.totalReportsCount > 0
        ? Math.round((s.completedReportsCount / s.totalReportsCount) * 100)
        : null;

    return [
      {
        label: 'إجمالي البلاغات المسجلة',
        value: s ? String(s.totalReportsCount) : dash,
        unit: 'بلاغ',
        icon: 'folder_open',
        status: Status.Info,
        note: 'جميع البلاغات التي قدمتها',
        noteIcon: 'history',
      },
      {
        label: 'قيد المتابعة والتنفيذ',
        value: s ? String(s.inProgressReportsCount) : dash,
        unit: 'حالات نشطة',
        icon: 'engineering',
        status: Status.InProgress,
        note: !s
          ? undefined
          : s.inProgressReportsCount > 0
            ? 'فرق الصيانة تعمل عليها الآن'
            : 'لا توجد أعمال جارية حاليًا',
        notePulse: !!s && s.inProgressReportsCount > 0,
      },
      {
        label: 'تم إنجازها بنجاح',
        value: s ? String(s.completedReportsCount) : dash,
        unit: 'مكتملة ومغلقة',
        icon: 'task_alt',
        status: Status.Success,
        note: completionRate !== null ? `نسبة حل بلغت ${completionRate}%` : undefined,
        noteIcon: 'verified',
      },
      {
        label: 'بانتظار تقييمك للخدمة',
        value: s ? String(s.awaitingRatingReportsCount) : dash,
        unit: 'يتطلب رأيك',
        icon: 'reviews',
        status: Status.Warning,
        note: 'تقييم الجودة يعزز الخدمة',
        noteIcon: 'star',
      },
    ];
  });
}
