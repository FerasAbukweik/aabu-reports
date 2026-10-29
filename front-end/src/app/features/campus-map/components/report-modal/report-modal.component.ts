import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  computed,
  inject,
  input,
  output,
  signal,
} from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { finalize } from 'rxjs';
import { ReportAttachment } from './report-modal.model';
import { CUSTOM_PIN_ID } from '../../../../core/constants/location';
import { ReportSeverity } from '../../../../core/enums/report-severity.enum';
import { LocationService } from '../../../../core/services/api-services/location.service';
import { MapReportsService } from '../../../../core/services/api-services/map-reports.service';
import { ToastService } from '../../../../core/services/local/toast.service';
import {
  REPORT_CATEGORIES,
  reportCategoryLabel,
} from '../../../../core/utils/report-category-utils';
import { DialogComponent } from '../../../../shared/components/dialog/dialog.component';
import { MapViewService } from '../../../../shared/components/map-view/map-view.service';

/** per image. 5 images of this size stay under the backend request size limit (30 MB) */
const MAX_ATTACHMENT_BYTES = 5 * 1024 * 1024;
const MAX_ATTACHMENTS = 5;

/** Sends a report for a location of the map (or for the pin the user dropped). The page owns the state: `locationId` opens it, `closed` closes it. */
@Component({
  selector: 'app-report-modal',
  imports: [ReactiveFormsModule, DialogComponent],
  templateUrl: './report-modal.component.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ReportModalComponent {
  private readonly fb = inject(FormBuilder);
  private readonly toast = inject(ToastService);
  private readonly mapReportsService = inject(MapReportsService);
  private readonly mapSelection = inject(MapViewService);
  private readonly locationService = inject(LocationService);

  /** the location to report on, null keeps the dialog closed (only the id: the dialog looks the location up, so it is never old) */
  readonly locationId = input<string | null>(null);

  readonly closed = output<void>();

  /** the location of the report: from the list, or the pin the user dropped (it is not in the list) */
  protected readonly location = computed(() => {
    const id = this.locationId();
    if (!id) return null;

    if (id === CUSTOM_PIN_ID) return this.mapSelection.selected();
    return this.locationService.locations().find((l) => l.id === id) ?? null;
  });
  protected readonly categories = REPORT_CATEGORIES;
  protected readonly reportCategoryLabel = reportCategoryLabel;
  protected readonly ReportSeverity = ReportSeverity;

  protected readonly form = this.fb.nonNullable.group({
    severity: [ReportSeverity.Medium],
    category: [REPORT_CATEGORIES[0]],
    description: ['', [Validators.required, Validators.maxLength(1000)]],
  });

  protected readonly maxAttachments = MAX_ATTACHMENTS;
  protected readonly maxAttachmentMb = MAX_ATTACHMENT_BYTES / (1024 * 1024);

  /** the images picked for the report (only images, the backend rejects any other file) */
  protected readonly attachments = signal<readonly ReportAttachment[]>([]);
  protected readonly attachmentError = signal<string | null>(null);
  protected readonly submitting = signal(false);

  constructor() {
    inject(DestroyRef).onDestroy(() =>
      this.attachments().forEach((a) => URL.revokeObjectURL(a.previewUrl)),
    );
  }

  protected onFilesSelected(event: Event): void {
    const input = event.target as HTMLInputElement;
    const picked = Array.from(input.files ?? []);

    // clear the input so picking the same file again (after removing it) still fires `change`
    input.value = '';

    if (picked.length === 0) return;

    const accepted = [...this.attachments()];
    let error: string | null = null;

    for (const file of picked) {
      if (!file.type.startsWith('image/')) {
        error = 'الملف يجب أن يكون صورة';
        continue;
      }
      if (file.size > MAX_ATTACHMENT_BYTES) {
        error = `حجم الصورة يتجاوز ${this.maxAttachmentMb} ميجابايت`;
        continue;
      }
      // the same image picked twice is added once
      if (accepted.some((a) => this.isSameFile(a.file, file))) continue;

      if (accepted.length >= MAX_ATTACHMENTS) {
        error = `الحد الأقصى ${MAX_ATTACHMENTS} صور للبلاغ الواحد`;
        continue;
      }
      accepted.push({ file, previewUrl: URL.createObjectURL(file) });
    }

    this.attachments.set(accepted);
    this.attachmentError.set(error);
  }

  protected removeAttachment(index: number): void {
    const removed = this.attachments()[index];
    if (!removed) return;

    URL.revokeObjectURL(removed.previewUrl);
    this.attachments.update((list) => list.filter((_, i) => i !== index));
    this.attachmentError.set(null);
  }

  protected formatSize(file: File): string {
    return file.size >= 1024 * 1024
      ? `${(file.size / (1024 * 1024)).toFixed(1)} MB`
      : `${Math.max(1, Math.round(file.size / 1024))} KB`;
  }

  protected onSubmit(): void {
    const location = this.location();

    if (this.form.invalid || !location || this.submitting()) {
      this.form.markAllAsTouched();
      return;
    }

    this.submitting.set(true);

    const images = this.attachments().map((a) => a.file);

    // MapReportsService shows the error toast; the form stays filled so the user can try again
    this.mapReportsService
      .create(location, { ...this.form.getRawValue(), images })
      .pipe(
        finalize(() => {
          this.submitting.set(false);
          this.closed.emit();
        }),
      )
      .subscribe({
        next: ({ location: saved }) => {
          // a dropped pin became a real location: select that one, so the inspector shows it
          this.mapSelection.selectAndFocusLocation(saved.id);

          this.reset();
          this.toast.success('تم إرسال البلاغ', `تم تسجيل بلاغك عن: ${location.title}`);
        },
        error: () => {},
      });
  }

  // private
  private reset(): void {
    this.form.reset({
      severity: ReportSeverity.Medium,
      category: REPORT_CATEGORIES[0],
      description: '',
    });
    this.attachments().forEach((a) => URL.revokeObjectURL(a.previewUrl));
    this.attachments.set([]);
    this.attachmentError.set(null);
  }

  private isSameFile(a: File, b: File): boolean {
    return a.name === b.name && a.size === b.size && a.lastModified === b.lastModified;
  }
}
