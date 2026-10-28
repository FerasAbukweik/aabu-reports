import { ReportCategory } from '../../../../core/enums/report-category.enum';
import { getEnumValues } from '../../../../core/utils/enum-utils';

/** The Arabic label of every report category */
export const reportCategoryLabel: Record<ReportCategory, string> = {
  [ReportCategory.Electrical]: 'إنارة وكهرباء',
  [ReportCategory.Plumbing]: 'مياه وتمديدات صحية',
  [ReportCategory.Roads]: 'طرق وأرصفة وممرات',
  [ReportCategory.Furniture]: 'أثاث ومرافق قاعات',
  [ReportCategory.Cleaning]: 'نظافة ومساحات خضراء',
  [ReportCategory.Other]: 'أخرى / عام',
};

/** All the categories in enum order */
export function getReportCategories(): ReportCategory[] {
  return getEnumValues(ReportCategory);
}
