import {
  ChangeDetectionStrategy,
  Component,
  computed,
  DestroyRef,
  ElementRef,
  inject,
  viewChild,
} from '@angular/core';
import { takeUntilDestroyed, toObservable } from '@angular/core/rxjs-interop';
import { catchError, EMPTY, switchMap } from 'rxjs';
import { ReportDTO } from '../../core/DTOs/report/report-dto';
import { Status } from '../../core/enums/status.enum';
import { MaintenanceStatsService } from '../../core/services/local/maintenance-stats.service';
import { ReportModalService } from './components/report-modal/report-modal.service';
import { ToastService } from '../../core/services/local/toast.service';
import { LocationService } from '../../core/services/api-services/location.service';
import { ReportsService } from '../../core/services/api-services/reports.service';
import { LocationInspectorComponent } from './components/location-inspector/location-inspector.component';
import { MapViewComponent } from '../../shared/components/map-view/map-view.component';
import { MapViewService } from '../../shared/components/map-view/map-view.service';
import { PlaceSearchComponent } from './components/place-search/place-search.component';
import { ReportModalComponent } from './components/report-modal/report-modal.component';
import { StatCardsComponent } from '../../shared/components/stat-card/stat-card.component';
import { StatCard } from '../../shared/components/stat-card/stat-card.model';
import { LocateResult } from '../../shared/components/map-view/map-view.model';

/** the reports of a location that has none loaded yet (one array, so the signal does not change) */
const NO_REPORTS: readonly ReportDTO[] = [];

@Component({
  selector: 'app-campus-map',
  standalone: true,
  imports: [
    PlaceSearchComponent,
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
  private readonly reportModal = inject(ReportModalService);
  private readonly mapSelection = inject(MapViewService);
  private readonly locationService = inject(LocationService);
  private readonly reportsService = inject(ReportsService);
  private readonly destroyRef = inject(DestroyRef);
  private readonly inspector = viewChild('inspector', { read: ElementRef });

  constructor() {
    // the map, the search and the chips read the locations from LocationService
    this.locationService
      .load()
      .pipe(takeUntilDestroyed())
      // LocationService already shows the error toast, nothing else to do on failure
      .subscribe({ error: () => {} });

    // the inspector shows the reports of the selected location: load its first page when the
    // selection changes (switchMap cancels the request of the previous selection)
    toObservable(this.mapSelection.selectedLocationId)
      .pipe(
        switchMap((locationId) => {
          // nothing selected, or the dropped pin (it has no reports yet)
          if (!locationId) return EMPTY;
          // a location that already has reports keeps them, the inspector asks for the next page itself
          if (this.reportsService.locationReports()[locationId]?.length) return EMPTY;
          // ReportsService already shows the error toast
          return this.reportsService.loadMoreLocationReports(locationId).pipe(catchError(() => EMPTY));
        }),
        takeUntilDestroyed(),
      )
      .subscribe();
  }

  // ---------- static data ----------
  protected readonly stats = inject(MaintenanceStatsService).stats;

  /** maintenance stats mapped to the generic stat cards */
  protected readonly statCards = computed<StatCard[]>(() => {
    const s = this.stats();

    return [
      {
        label: 'إجمالي البلاغات المنجزة',
        value: String(s.completed),
        unit: 'طلب صيانة مكتمل',
        icon: 'task_alt',
        status: Status.Success,
        note: `نسبة إنجاز ${s.completionRate} خلال العام الحالي`,
        noteIcon: 'trending_up',
      },
      {
        label: 'بلاغات قيد التنفيذ والمتابعة',
        value: String(s.inProgress),
        unit: 'حالات صيانة ميدانية',
        icon: 'engineering',
        status: Status.Warning,
        badge: 'نشط الآن',
        note: s.fieldCrewNote,
        notePulse: true,
        route: '/history',
      },
      {
        label: 'متوسط زمن الاستجابة للبلاغ',
        value: String(s.avgResponseHours),
        unit: 'ساعة فقط',
        icon: 'speed',
        status: Status.Info,
        note: 'إشعار الطالب فوري عبر البوابة والرسائل',
        noteIcon: 'bolt',
      },
    ];
  });

  // ---------- selection ----------
  /** the selection lives in MapViewService: the map, the search and the inspector all use it */
  protected readonly selected = this.mapSelection.selected;
  protected readonly isCustomPin = this.mapSelection.isCustomPin;

  /** the reports, the loading flag and the "more" flag of the selected location */
  protected readonly locationReports = computed(() => {
    const id = this.mapSelection.selectedLocationId();
    return (id && this.reportsService.locationReports()[id]) || NO_REPORTS;
  });
  protected readonly isLocationReportsLoading = computed(() => {
    const id = this.mapSelection.selectedLocationId();
    return !!id && !!this.reportsService.isLocationReportsLoading()[id];
  });

  protected readonly isMoreLocationReportsAvailable = computed(() => {
    const id = this.mapSelection.selectedLocationId();
    return !!id && this.reportsService.isMoreLocationReportsAvailable(id);
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
  /** the inspector asked for the next page of the reports of the selected location */
  protected loadMoreReports(): void {
    const locationId = this.mapSelection.selectedLocationId();
    if (!locationId) return;

    this.reportsService
      .loadMoreLocationReports(locationId)
      .pipe(takeUntilDestroyed(this.destroyRef))
      // ReportsService already shows the error toast
      .subscribe({ error: () => {} });
  }

  /** opens the dialog with the current selection */
  protected openReport(): void {
    const location = this.selected();
    if (!location) return;

    this.reportModal.open(location);
  }
}
