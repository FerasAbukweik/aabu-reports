export interface TableColumn {
  /** matches the `appTableCell` of the cell template. Without a template the cell shows `row[key]` */
  key: string;
  /** text of the header cell */
  header: string;
  /** text alignment of the header and the cells, `start` by default */
  align?: 'start' | 'center';
  /** css min-width of the column, e.g. "15rem" */
  minWidth?: string;
  /** keep the cell on one line */
  nowrap?: boolean;
}
