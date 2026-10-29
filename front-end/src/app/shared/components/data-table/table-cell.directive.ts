import { Directive, TemplateRef, inject, input } from '@angular/core';

/** What a cell template receives: `let-row` is the row of the table */
export interface TableCellContext<T = unknown> {
  $implicit: T;
}

/**
 * Marks an <ng-template> as the cell of one column of <app-data-table>.
 * Pass the same rows to `appTableCellOf` so `let-row` is typed (like `ngForOf`, it is only used for typing):
 *   <ng-template appTableCell="state" [appTableCellOf]="rows()" let-row>...</ng-template>
 */
@Directive({ selector: 'ng-template[appTableCell]' })
export class TableCellDirective<T = unknown> {
  /** the `key` of the column */
  readonly key = input.required<string>({ alias: 'appTableCell' });

  /** the rows of the table, only so the template knows the type of `let-row` */
  readonly rows = input<readonly T[]>([], { alias: 'appTableCellOf' });

  readonly template = inject<TemplateRef<TableCellContext<T>>>(TemplateRef);

  static ngTemplateContextGuard<T>(
    _directive: TableCellDirective<T>,
    _context: unknown,
  ): _context is TableCellContext<T> {
    return true;
  }
}
