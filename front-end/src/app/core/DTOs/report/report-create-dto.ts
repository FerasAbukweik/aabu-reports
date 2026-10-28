import { ReportCategory } from '../../enums/report-category.enum';
import { ReportSeverity } from '../../enums/report-severity.enum';

/** The fields every new report has (the report form), same as ReportCreate on the backend */
export interface ReportCreateDTO {
  category: ReportCategory;
  severity: ReportSeverity;
  description: string;
}
