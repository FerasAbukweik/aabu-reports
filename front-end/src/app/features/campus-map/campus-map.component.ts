import {
  ChangeDetectionStrategy,
  Component,
  computed,
  DestroyRef,
  ElementRef,
  inject,
  signal,
  viewChild,
} from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { ReportMapDTO } from '../../core/DTOs/report/report-map-dto';
import { Status } from '../../core/enums/status.enum';
import { ToastService } from '../../core/services/local/toast.service';
import { LocationService } from '../../core/services/api-services/location.service';
import { MapReportsService } from '../../core/services/api-services/map-reports.service';
import { MaintenanceStatsService } from '../../core/services/api-services/maintenance-stats.service';
import { LocationInspectorComponent } from './components/location-inspector/location-inspector.component';
import { MapViewComponent } from '../../shared/components/map-view/map-view.component';
import { MapViewService } from '../../shared/components/map-view/map-view.service';
import { BuildingsSearchComponent } from '../../shared/components/buildings-search/buildings-search.component';
import { ReportModalComponent } from './components/report-modal/report-modal.component';
import { StatCardsComponent } from '../../shared/components/stat-card/stat-cards.component';
import { StatCard } from '../../shared/components/stat-card/stat-card.model';
import { LocateResult } from '../../shared/components/map-view/map-view.model';

/** the reports of a location that has none loaded yet (one array, so the signal does not change) */
const NO_REPORTS: readonly ReportMapDTO[] = [];

@Component({
  selector: 'app-campus-map',
  imports: [
    BuildingsSearchComponent,
    MapViewComponent,
    LocationInspectorComponent,
    StatCardsComponent,
    ReportModalComponent,
  ],
  templateUrl: './campus-map.component.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class CampusMapComponent {
  private readonly toast = inject(ToastService);
  private readonly mapSelection = inject(MapViewService);
  private readonly locationService = inject(LocationService);
  private readonly mapReportsService = inject(MapReportsService);
  private readonly maintenanceStats = inject(MaintenanceStatsService);
  private readonly destroyRef = inject(DestroyRef);
  private readonly inspector = viewChild('inspector', { read: ElementRef });

  constructor() {
    // the map, the search and the chips read the locations from LocationService
    this.locationService
      .load()
      .pipe(takeUntilDestroyed())
      // LocationService already shows the error toast, nothing else to do on failure
      .subscribe({ error: () => {} });

    // the stats cards (the numbers stay empty until the request is done)
    this.maintenanceStats
      .load()
      .pipe(takeUntilDestroyed())
      // MaintenanceStatsService already shows the error toast, nothing else to do on failure
      .subscribe({ error: () => {} });
  }

  // ---------- stats ----------
  private readonly stats = this.maintenanceStats.stats;

  /** the average response time for the inspector, rounded up ("less than N hours"), null while unknown */
  protected readonly inspectorAvgResponseHours = computed(() => {
    const hours = this.stats()?.averageResponseTimeHours;
    return hours ? Math.ceil(hours) : null;
  });

  /** maintenance stats mapped to the generic stat cards (a dash while the numbers are not loaded) */
  protected readonly statCards = computed<StatCard[]>(() => {
    const s = this.stats();
    const dash = '—';

    return [
      {
        label: 'إجمالي البلاغات المنجزة',
        value: s ? String(s.completedReportsCount) : dash,
        unit: 'طلب صيانة مكتمل',
        icon: 'task_alt',
        status: Status.Success,
        note: s ? `نسبة إنجاز ${s.completionRatePercent}% خلال العام الحالي` : undefined,
        noteIcon: 'trending_up',
      },
      {
        label: 'بلاغات قيد التنفيذ والمتابعة',
        value: s ? String(s.inProgressReportsCount) : dash,
        unit: 'حالات صيانة ميدانية',
        icon: 'engineering',
        status: Status.Warning,
        badge: s && s.inProgressReportsCount > 0 ? 'نشط الآن' : undefined,
        note: !s
          ? undefined
          : s.activeCrewLocation
            ? `كادر الصيانة متواجد في ${s.activeCrewLocation}`
            : 'لا توجد أعمال صيانة جارية حاليًا',
        notePulse: !!s?.activeCrewLocation,
        route: '/history',
      },
      {
        label: 'متوسط زمن الاستجابة للبلاغ',
        value: s && s.averageResponseTimeHours > 0 ? String(s.averageResponseTimeHours) : dash,
        unit: 'ساعة فقط',
        icon: 'speed',
        status: Status.Info,
        note: 'إشعار الطالب فوري عبر الاشعارات والرسائل',
        noteIcon: 'bolt',
      },
    ];
  });

  // ---------- selection ----------
  /** the selection lives in MapViewService: the map, the search and the inspector all use it */
  protected readonly selected = this.mapSelection.selected;

  /** the reports, the loading flag and the "more" flag of the selected location */
  protected readonly locationReports = computed(() => {
    const id = this.mapSelection.selectedLocationId();
    return (id && this.mapReportsService.locationReports()[id]) || NO_REPORTS;
  });
  protected readonly isLocationReportsLoading = computed(() => {
    const id = this.mapSelection.selectedLocationId();
    return !!id && !!this.mapReportsService.isLocationReportsLoading()[id];
  });

  protected readonly hasLocationReportsFailed = computed(() => {
    const id = this.mapSelection.selectedLocationId();
    return !!id && !!this.mapReportsService.locationReportsFailed()[id];
  });

  protected readonly isMoreLocationReportsAvailable = computed(() => {
    const id = this.mapSelection.selectedLocationId();
    return !!id && this.mapReportsService.isMoreLocationReportsAvailable(id);
  });

  protected resetSelection(): void {
    this.mapSelection.clear();
  }

  // ---------- map actions ----------
  protected focusSelection(): void {
    this.mapSelection.focusSelected();
  }

  protected onLocated(result: LocateResult): void {
    switch (result.status) {
      case 'found':
        this.toast.success(
          'تم تحديد موقعك الحالي',
          result.nearest?.title ? `أنت بالقرب من ${result.nearest.title}` : '',
        );
        break;
      case 'outside':
        this.toast.warning('أنت خارج حدود الخريطة', 'لا يمكن عرض موقعك الحالي على مخطط الحرم');
        break;
      case 'unsupported':
        this.toast.error('تعذر تحديد الموقع', 'المتصفح لا يدعم خدمة تحديد الموقع');
        break;
      case 'failed':
        this.toast.error('تعذر تحديد الموقع', 'تأكد من السماح للموقع بالوصول إلى موقعك الجغرافي');
        break;
    }
  }

  /** small screens stack the inspector under the map; the chip over the map brings it into view */
  protected scrollToInspector(): void {
    this.inspector()?.nativeElement.scrollIntoView({ behavior: 'smooth', block: 'start' });
  }

  // ---------- report ----------
  /** the inspector asked for the next page of the reports of the selected location (the first page too: its sentinel is on the screen) */
  protected loadMoreReports(): void {
    const locationId = this.mapSelection.selectedLocationId();
    if (!locationId) return;

    this.mapReportsService
      .loadMoreLocationReports(locationId)
      .pipe(takeUntilDestroyed(this.destroyRef))
      // MapReportsService already shows the error toast
      .subscribe({ error: () => {} });
  }

  /** the location the report dialog is open for, null keeps it closed */
  protected readonly reportLocationId = signal<string | null>(null);

  /** opens the dialog with the current selection */
  protected openReport(): void {
    const location = this.selected();
    if (!location) return;

    this.reportLocationId.set(location.id);
  }
}