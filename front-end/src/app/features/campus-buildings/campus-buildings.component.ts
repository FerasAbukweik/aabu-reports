import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  ElementRef,
  computed,
  effect,
  inject,
  signal,
  untracked,
  viewChild,
} from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { Router } from '@angular/router';
import { LocationTypeEnum } from '../../core/enums/location-type.enum';
import { LocationService } from '../../core/services/api-services/location.service';
import { ToastService } from '../../core/services/local/toast.service';
import { MapViewComponent } from '../../shared/components/map-view/map-view.component';
import { LocateResult } from '../../shared/components/map-view/map-view.model';
import { MapViewService } from '../../shared/components/map-view/map-view.service';
import { BuildingFormValue } from './components/building.model';
import { BuildingDeleteDialogComponent } from './components/building-delete-dialog/building-delete-dialog.component';
import { BuildingDetailsComponent } from './components/building-details/building-details.component';
import { BuildingEmptyStateComponent } from './components/building-empty-state/building-empty-state.component';
import { BuildingFormComponent } from './components/building-form/building-form.component';
import { BuildingHeaderComponent } from './components/building-header/building-header.component';
import { BuildingsSearchComponent } from '../../shared/components/buildings-search/buildings-search.component';

@Component({
  selector: 'app-campus-buildings',
  imports: [
    MapViewComponent,
    BuildingHeaderComponent,
    BuildingDetailsComponent,
    BuildingEmptyStateComponent,
    BuildingFormComponent,
    BuildingDeleteDialogComponent,
    BuildingsSearchComponent
],
  templateUrl: './campus-buildings.component.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class CampusBuildingsComponent {
  private readonly toast = inject(ToastService);
  private readonly router = inject(Router);
  private readonly destroyRef = inject(DestroyRef);
  private readonly locationService = inject(LocationService);
  private readonly mapSelection = inject(MapViewService);
  private readonly panel = viewChild('panel', { read: ElementRef });

  // ---------- state ----------
  /** the selection lives in MapViewService: the map writes to it, this page reads it */
  protected readonly selected = this.mapSelection.selected;
  protected readonly isDroppedPin = this.mapSelection.isDroppedPin;
  protected readonly buildingsCount = computed(() => this.locationService.buildings().length);

  /** 'view' shows the details, 'edit' shows the form; a dropped pin always shows the add form */
  private readonly viewMode = signal<'view' | 'edit'>('view');
  protected readonly panelMode = computed<'empty' | 'add' | 'view' | 'edit'>(() => {
    if (this.isDroppedPin()) return 'add';
    return this.selected() ? this.viewMode() : 'empty';
  });
  /** mode handed to the form component (it is only rendered for 'add' and 'edit') */
  protected readonly formMode = computed<'add' | 'edit'>(() =>
    this.panelMode() === 'edit' ? 'edit' : 'add',
  );

  /** a new object here resets the form component to these values */
  protected readonly formSeed = signal<BuildingFormValue>({ title: '', lat: 0, lng: 0 });

  protected readonly isDeleteOpen = signal(false);
  protected readonly isSaving = signal(false);
  protected readonly isDeleting = signal(false);

  constructor() {
    // the map reads the buildings from LocationService
    this.locationService
      .load()
      .pipe(takeUntilDestroyed())
      // LocationService already shows the error toast, nothing else to do on failure
      .subscribe({ error: () => {} });

    // start without a selection left over from another page
    this.mapSelection.clear();
    this.destroyRef.onDestroy(() => this.mapSelection.clear());

    // a new selection closes the edit form and the delete dialog;
    // a dropped pin opens the add form with the coordinates of the pin
    effect(() => {
      this.mapSelection.selectedLocationId();
      const pin = this.isDroppedPin() ? this.selected() : null;
      untracked(() => {
        this.viewMode.set('view');
        this.isDeleteOpen.set(false);
        if (pin) {
          this.formSeed.set({ title: '', lat: pin.coordinates.lat, lng: pin.coordinates.lng });
        }
      });
    });
  }

  // ---------- map ----------
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

  /** small screens stack the panel under the map; the chip over the map brings it into view */
  protected scrollToPanel(): void {
    this.panel()?.nativeElement.scrollIntoView({ behavior: 'smooth', block: 'start' });
  }

  protected focusSelection(): void {
    this.mapSelection.focusSelected();
  }

  // ---------- add / edit ----------
  protected openEdit(): void {
    const location = this.selected();
    if (!location || this.isDroppedPin()) return;

    this.formSeed.set({
      title: location.title,
      lat: location.coordinates.lat,
      lng: location.coordinates.lng,
    });
    this.viewMode.set('edit');
  }

  /** closes the edit form, or drops the pin of the add form */
  protected cancelForm(): void {
    if (this.panelMode() === 'add') this.mapSelection.clear();
    else this.viewMode.set('view');
  }

  /** the form component already validated the value */
  protected onFormSubmitted(value: BuildingFormValue): void {
    if (this.isSaving()) return;

    if (this.panelMode() === 'add') this.create(value);
    else this.update(value);
  }

  private create({ title, lat, lng }: BuildingFormValue): void {
    const coordinates = { lat: Number(lat.toFixed(6)), lng: Number(lng.toFixed(6)) };
    this.isSaving.set(true);

    this.locationService
      .create({ title, coordinates, type: LocationTypeEnum.Building })
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: () => {
          this.isSaving.set(false);
          this.toast.success('تمت إضافة المبنى', title.trim());

          // the api returns no id: find the new building in the reloaded list
          const created = this.locationService
            .buildings()
            .filter(
              (b) =>
                b.title === title.trim() &&
                Math.abs(b.coordinates.lat - coordinates.lat) < 1e-5 &&
                Math.abs(b.coordinates.lng - coordinates.lng) < 1e-5,
            )
            .at(-1);
          if (created) this.mapSelection.selectLocation(created.id);
          else this.mapSelection.clear();
        },
        // LocationService already shows the error toast
        error: () => this.isSaving.set(false),
      });
  }

  private update({ title, lat, lng }: BuildingFormValue): void {
    const location = this.selected();
    if (!location) return;

    this.isSaving.set(true);

    this.locationService
      .update({
        id: location.id,
        title: title.trim(),
        coordinates: { lat: Number(lat.toFixed(6)), lng: Number(lng.toFixed(6)) },
      })
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (updated) => {
          this.isSaving.set(false);
          this.viewMode.set('view');
          this.toast.success('تم تحديث المبنى', updated.title);
        },
        // LocationService already shows the error toast
        error: () => this.isSaving.set(false),
      });
  }

  // ---------- delete ----------
  protected confirmDelete(): void {
    const location = this.selected();
    if (!location || this.isDeleting()) return;

    this.isDeleting.set(true);
    this.locationService
      .delete(location.id)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: () => {
          this.isDeleting.set(false);
          this.isDeleteOpen.set(false);
          this.mapSelection.clear();
          this.toast.success('تم حذف المبنى', location.title);
        },
        // LocationService already shows the error toast
        error: () => this.isDeleting.set(false),
      });
  }
}
