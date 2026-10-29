import { ReportState } from "../../core/enums/report-state.enum";
import { locationStatusLabel } from "../../core/utils/location-util";
import { REPORT_CATEGORIES, reportCategoryLabel } from "../../core/utils/report-category-utils";
import { FilterConfig } from "./components/search-filter-bar/search-filter-bar.model";

/** the states a report can be in (a report is never "normal", that one is only for locations without reports) */
export const REPORT_STATES: readonly ReportState[] = [
  ReportState.Pending,
  ReportState.InProgress,
  ReportState.Fixed,
  ReportState.NeedsFollowUp,
];

export const FILTERS: readonly FilterConfig[] = [
  {
    key: 'state',
    label: 'تصفية حسب الحالة',
    icon: 'tune',
    allLabel: 'جميع الحالات',
    options: REPORT_STATES.map((state) => ({
      value: String(state),
      label: locationStatusLabel[state],
    })),
  },
  {
    key: 'category',
    label: 'تصفية حسب التصنيف',
    icon: 'category',
    allLabel: 'كل التصنيفات',
    options: REPORT_CATEGORIES.map((category) => ({
      value: String(category),
      label: reportCategoryLabel[category],
    })),
  },
];