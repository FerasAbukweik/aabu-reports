import { TestBed } from '@angular/core/testing';
import { ReportsTableComponent } from './reports-table.component';

describe('ReportsTableComponent pagination', () => {
  it('uses the matching total for filtered page numbers and the last-page action', () => {
    const fixture = TestBed.createComponent(ReportsTableComponent);
    fixture.componentRef.setInput('reports', []);
    fixture.componentRef.setInput('pageSize', 10);
    fixture.componentRef.setInput('total', 21);
    fixture.componentRef.setInput('isFiltered', true);
    fixture.detectChanges();
    const changed = vi.fn();
    fixture.componentInstance.pageChange.subscribe(changed);

    const lastPage = fixture.nativeElement.querySelector(
      '[aria-label="الصفحة الأخيرة"]',
    ) as HTMLButtonElement;
    expect(fixture.nativeElement.querySelector('[aria-label="الصفحة 3"]')).toBeTruthy();
    expect(fixture.nativeElement.querySelector('[aria-label="الصفحة 4"]')).toBeNull();
    lastPage.click();
    expect(changed).toHaveBeenCalledWith(3);

    fixture.componentRef.setInput('page', 3);
    fixture.detectChanges();
    expect(lastPage.disabled).toBe(true);

    fixture.componentRef.setInput('total', 0);
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('nav')).toBeNull();
  });
});
