export interface FilterOption {
  value: string;
  label: string;
}

/** One dropdown of the filter bar. Its value is the `value` of the picked option, an empty string means "all" */
export interface FilterConfig {
  key: string;
  /** accessible name of the dropdown */
  label: string;
  /** material symbol shown inside the dropdown */
  icon: string;
  /** text of the "no filter" option, e.g. "all states" */
  allLabel: string;
  options: readonly FilterOption[];
}

/** The picked value of every filter, by `FilterConfig.key`. A missing key means "all" */
export type FilterValues = Record<string, string>;
