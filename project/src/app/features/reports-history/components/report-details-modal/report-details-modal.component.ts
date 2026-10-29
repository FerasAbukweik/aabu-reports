import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';
import { ReportSeverity } from '../../../../core/enums/report-severity.enum';
import { locationStatusLabel, locationStatusTone } from '../../../../core/utils/location-util';
import { googleMapsUrl } from '../../../../core/utils/map.utils';
import { reportCategoryIcon, reportCategoryLabel } from '../../../../core/utils/report-category-utils';
import { reportSeverityLabel, reportSeverityTone } from '../../../../core/utils/report-severity-utils';
import { DialogComponent } from '../../../../shared/components/dialog/dialog.component';
import { StarRatingComponent } from '../../../../shared/components/star-rating/star-rating.component';
import { ReportRow } from '../../reports-history.model';

/** Everything the api knows about one report: description, photos, severity, rating... */
@Component({
  selector: 'app-report-details-modal',
  imports: [DatePipe, DialogComponent, StarRatingComponent],
  templateUrl: './report-details-modal.component.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ReportDetailsModalComponent {
  /** the report to show, null keeps the dialog closed */
  readonly row = input<ReportRow | null>(null);

  readonly closed = output<void>();
  /** the user wants to rate this fixed report (the page closes this dialog and opens the rating one) */
  readonly rateRequested = output<ReportRow>();

  protected readonly ReportSeverity = ReportSeverity;
  protected readonly stateLabel = locationStatusLabel;
  protected readonly stateTone = locationStatusTone;
  protected readonly categoryLabel = reportCategoryLabel;
  protected readonly categoryIcon = reportCategoryIcon;
  protected readonly severityLabel = reportSeverityLabel;
  protected readonly severityTone = reportSeverityTone;
  protected readonly googleMapsUrl = googleMapsUrl;
}
