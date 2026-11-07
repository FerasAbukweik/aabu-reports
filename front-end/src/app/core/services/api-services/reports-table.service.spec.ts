import { TestBed } from '@angular/core/testing';
import { firstValueFrom, of, Subject, throwError } from 'rxjs';
import { ReportApiService } from '../api/report-api.service';
import { ToastService } from '../local/toast.service';
import { ReportsTableService } from './reports-table.service';

describe('ReportsTableService pagination', () => {
  it('caches the matching total with each page and keeps filter scopes separate', async () => {
    const getTable = vi
      .fn()
      .mockReturnValueOnce(of({ reports: [], totalCount: 42 }))
      .mockReturnValueOnce(of({ reports: [], totalCount: 0 }));
    TestBed.configureTestingModule({
      providers: [
        { provide: ReportApiService, useValue: { getTable } },
        { provide: ToastService, useValue: {} },
      ],
    });
    const service = TestBed.inject(ReportsTableService);
    const filter = { search: 'Library' };

    await firstValueFrom(service.loadPage(2, true, filter));
    expect(service.cachedTotal(2, true, filter)).toBe(42);
    expect(service.cachedTotal(2, false, filter)).toBeUndefined();
    expect(service.cachedTotal(2, true, {})).toBeUndefined();
    expect(getTable).toHaveBeenCalledWith({ taken: 10, sectionSize: 10 }, filter, true);

    await firstValueFrom(service.loadPage(2, true, filter));
    expect(getTable).toHaveBeenCalledTimes(1);

    await firstValueFrom(service.loadPage(1, true, { search: 'missing' }));
    expect(service.cachedTotal(1, true, { search: 'missing' })).toBe(0);
    expect(service.cachedTotal(2, true, filter)).toBe(42);

    service.clearPages();
    expect(service.cachedTotal(2, true, filter)).toBeUndefined();
  });
  it('invalidates every filtered page after a successful state change but retains them on failure', async () => {
    const api = {
      getTable: vi.fn().mockReturnValue(of({ reports: [], totalCount: 7 })),
      updateState: vi
        .fn()
        .mockReturnValueOnce(throwError(() => new Error('failed')))
        .mockReturnValueOnce(of({ id: 'report', state: 2 })),
    };
    TestBed.configureTestingModule({
      providers: [
        { provide: ReportApiService, useValue: api },
        { provide: ToastService, useValue: { error: vi.fn() } },
      ],
    });
    const service = TestBed.inject(ReportsTableService);
    const filter = { state: 3 };
    await firstValueFrom(service.loadPage(1, true, filter));
    await expect(firstValueFrom(service.updateState('report', 2))).rejects.toThrow('failed');
    expect(service.cachedTotal(1, true, filter)).toBe(7);
    await firstValueFrom(service.updateState('report', 2));
    expect(service.cachedPage(1, true, filter)).toBeUndefined();
    expect(service.cachedTotal(1, true, filter)).toBeUndefined();
  });

  it('cancels a pending page when the session ends so it cannot refill a cleared cache', () => {
    const response = new Subject<{ reports: []; totalCount: number }>();
    TestBed.configureTestingModule({
      providers: [
        { provide: ReportApiService, useValue: { getTable: () => response } },
        { provide: ToastService, useValue: {} },
      ],
    });
    const service = TestBed.inject(ReportsTableService);
    service.loadPage(1, false).subscribe();
    service.reset();
    response.next({ reports: [], totalCount: 100 });
    expect(service.cachedTotal(1, false)).toBeUndefined();
    expect(service.isLoading()).toBe(false);
  });
});
