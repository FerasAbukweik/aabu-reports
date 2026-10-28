import {
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  OnInit,
  inject,
  signal,
  viewChild,
} from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { finalize } from 'rxjs';
import { ReportModalService } from './report-modal.service';
import { getReportCategories, reportCategoryLabel } from './report-modal.data';
import { ReportSeverity } from '../../../../core/enums/report-severity.enum';
import { ToastService } from '../../../../core/services/local/toast.service';
import { ReportsService } from '../../../../core/services/api-services/reports.service';
import { MapViewService } from '../../../../shared/components/map-view/map-view.service';

const MAX_ATTACHMENT_BYTES = 5 * 1024 * 1024;

@Component({
  selector: 'app-report-modal',
  standalone: true,
  imports: [ReactiveFormsModule],
  templateUrl: './report-modal.component.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ReportModalComponent implements OnInit {
  private readonly _fb = inject(FormBuilder);
  protected readonly modal = inject(ReportModalService);
  private readonly _dialog = viewChild<ElementRef<HTMLDialogElement>>('dialog');
  private readonly _fileInput = viewChild<ElementRef<HTMLInputElement>>('fileInput');
  private readonly _toast = inject(ToastService);
  private readonly _reportsService = inject(ReportsService);
  private readonly _mapSelection = inject(MapViewService);

  // protected
  protected readonly location = this.modal.location;
  protected readonly categories = getReportCategories();
  protected readonly reportCategoryLabel = reportCategoryLabel;
  protected readonly ReportSeverity = ReportSeverity;

  protected readonly form = this._fb.nonNullable.group({
    severity: [ReportSeverity.Medium],
    category: [this.categories[0]],
    description: ['', [Validators.required, Validators.maxLength(1000)]],
  });

  protected readonly attachment = signal<File | null>(null);
  protected readonly attachmentError = signal<string | null>(null);
  protected readonly submitting = signal(false);

  protected onBackdropClick(event: MouseEvent): void {
    if (event.target === this._dialog()?.nativeElement) this.modal.close();
  }

  // methods
  ngOnInit(): void {
    this.modal.init(this._dialog()?.nativeElement!);
  }

  protected onFileSelected(event: Event): void {
    const file = (event.target as HTMLInputElement).files?.[0] ?? null;
    this.attachmentError.set(null);

    if (!file) {
      this.attachment.set(null);
      return;
    }
    if (!file.type.startsWith('image/') && !file.type.startsWith('video/')) {
      this.attachment.set(null);
      this.attachmentError.set('الملف يجب أن يكون صورة أو فيديو');
      return;
    }
    if (file.size > MAX_ATTACHMENT_BYTES) {
      this.attachment.set(null);
      this.attachmentError.set(`حجم الملف يتجاوز ${MAX_ATTACHMENT_BYTES / (1024 * 1024)} ميجابايت`);
      return;
    }
    this.attachment.set(file);
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

    // ReportsService shows the error toast; the form stays filled so the user can try again
    this._reportsService
      .create(location, this.form.getRawValue())
      .pipe(finalize(() => this.submitting.set(false)))
      .subscribe({
        next: ({ location: saved }) => {
          // the saved location (a dropped pin became a real one) is in the list now: select it
          // again so the inspector shows its updated status
          this._mapSelection.selectLocation(saved.id);

          this.reset();
          this.modal.close();
          this._toast.success(
            'تم إرسال البلاغ',
            `تم تسجيل بلاغك عن: ${location.title ?? 'موقع مخصص على الخريطة'}`,
          );
        },
        error: () => {},
      });
  }

  // private
  reset() {
    this.form.reset({
      severity: ReportSeverity.Medium,
      category: this.categories[0],
      description: '',
    });
    const fileInput = this._fileInput()?.nativeElement;
    if (fileInput) fileInput.value = '';
    this.attachment.set(null);
    this.attachmentError.set(null);
  }
}
