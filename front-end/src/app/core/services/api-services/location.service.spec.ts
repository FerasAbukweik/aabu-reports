import { TestBed } from '@angular/core/testing';
import { firstValueFrom, of, Subject } from 'rxjs';
import { LocationApiService } from '../api/location-api.service';
import { ToastService } from '../local/toast.service';
import { LocationDTO } from '../../DTOs/location/location-dto';
import { LocationService } from './location.service';

describe('LocationService cache', () => {
  it('caches an empty result and starts loading only on subscription', async () => {
    const getAll = vi.fn().mockReturnValue(of([]));
    TestBed.configureTestingModule({
      providers: [
        { provide: LocationApiService, useValue: { getAll } },
        { provide: ToastService, useValue: {} },
      ],
    });
    const service = TestBed.inject(LocationService);
    const request = service.load();
    expect(service.loading()).toBe(false);
    expect(getAll).not.toHaveBeenCalled();
    await firstValueFrom(request);
    expect(await firstValueFrom(service.load())).toEqual([]);
    expect(getAll).toHaveBeenCalledTimes(1);
  });

  it('cancels an outstanding read when the session cache is reset', () => {
    const response = new Subject<readonly LocationDTO[]>();
    TestBed.configureTestingModule({
      providers: [
        { provide: LocationApiService, useValue: { getAll: () => response } },
        { provide: ToastService, useValue: {} },
      ],
    });
    const service = TestBed.inject(LocationService);
    const received = vi.fn();
    service.load().subscribe(received);
    expect(service.loading()).toBe(true);
    service.reset();
    response.next([]);
    expect(received).not.toHaveBeenCalled();
    expect(service.loading()).toBe(false);
  });
});
