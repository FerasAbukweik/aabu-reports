import { ReportCategory } from '../enums/report-category.enum';
import { getEnumValues } from './enum-utils';

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
export const REPORT_CATEGORIES: readonly ReportCategory[] = getEnumValues(ReportCategory);

/** The material symbol of every report category */
export const reportCategoryIcon: Record<ReportCategory, string> = {
  [ReportCategory.Electrical]: 'lightbulb',
  [ReportCategory.Plumbing]: 'water_drop',
  [ReportCategory.Roads]: 'edit_road',
  [ReportCategory.Furniture]: 'chair',
  [ReportCategory.Cleaning]: 'cleaning_services',
  [ReportCategory.Other]: 'more_horiz',
};
