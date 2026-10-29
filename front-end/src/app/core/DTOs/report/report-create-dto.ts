import { ReportCategory } from '../../enums/report-category.enum';
import { ReportSeverity } from '../../enums/report-severity.enum';

/** The fields every new report has (the report form), same as ReportCreateDto on the backend. It is sent as multipart/form-data because of the images */
export interface ReportCreateDTO {
  category: ReportCategory;
  severity: ReportSeverity;
  description: string;
  /** optional photos of the problem, only image files (the backend rejects anything else) */
  images?: readonly File[];
}
