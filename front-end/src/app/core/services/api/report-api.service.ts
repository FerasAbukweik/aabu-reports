import { HttpClient, HttpParams } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { URLs } from '../../constants/URLs';
import { LazyDTO } from '../../DTOs/lazy/lazy-dto';
import { MaintenanceStatsDTO } from '../../DTOs/report/maintenance-stats-dto';
import { ReportCreateForCoordinatesDTO } from '../../DTOs/report/report-create-for-coordinates-dto';
import { ReportCreateForLocationDTO } from '../../DTOs/report/report-create-for-location-dto';
import { ReportCreateResponseDTO } from '../../DTOs/report/report-create-response-dto';
import { ReportDTO } from '../../DTOs/report/report-dto';
import { ReportFilterDTO } from '../../DTOs/report/report-filter-dto';
import { ReportHistoryStatsDTO } from '../../DTOs/report/report-history-stats-dto';
import { ReportMapDTO } from '../../DTOs/report/report-map-dto';
import { ReportRateDTO } from '../../DTOs/report/report-rate-dto';
import { ReportUpdateStateDTO } from '../../DTOs/report/report-update-state-dto';

@Injectable({ providedIn: 'root' })
export class ReportApiService {
  // DI
  private http = inject(HttpClient);

  // private
  private readonly url = URLs.baseApi + '/report';

  /** the filled fields of the filter as query params (the names are the ones of the backend ReportFilterDto) */
  private filterParams(filter: ReportFilterDTO): Record<string, string | number> {
    const params: Record<string, string | number> = {};

    for (const [key, value] of Object.entries(filter)) {
      if (value !== undefined && value !== null && value !== '') params[key] = value;
    }

    return params;
  }

  /** the backend LazyDTO is `Skip` / `Take`: `taken` is sent as `skip`, `sectionSize` as `take` */
  private lazyParams({ taken, sectionSize }: LazyDTO) {
    return { skip: taken, take: sectionSize };
  }

  /**
   * The create endpoints take multipart/form-data (the report can come with images), not JSON.
   * The Content-Type header is not set on purpose: the browser sets it with the multipart boundary.
   * The backend binds the keys case-insensitively, `coordinates.lat` fills Coordinates.Lat,
   * and every image is added under the same `images` key to fill the Images list.
   */
  private toFormData(dto: ReportCreateForLocationDTO | ReportCreateForCoordinatesDTO): FormData {
    const form = new FormData();

    form.append('category', String(dto.category));
    form.append('severity', String(dto.severity));
    form.append('description', dto.description);

    if ('locationId' in dto) form.append('locationId', dto.locationId);

    if ('coordinates' in dto) {
      form.append('coordinates.lat', String(dto.coordinates.lat));
      form.append('coordinates.lng', String(dto.coordinates.lng));
    }

    for (const image of dto.images ?? []) form.append('images', image, image.name);

    return form;
  }

  // methods

  /** one page of the reports of a location, in the short shape the map needs */
  public getByLocation(locationId: string, lazyDto: LazyDTO) {
    return this.http.get<ReportMapDTO[]>(`${this.url}/location`, {
      params: new HttpParams({ fromObject: { locationId, ...this.lazyParams(lazyDto) } }),
    });
  }

  /**
   * one page of the reports table, filtered by the filter (the rows already hold the location name and coordinates).
   * The reports of the current user, or everyone's with `allReports` (the backend ignores it for a user who is not an admin).
   */
  public getTable(lazyDto: LazyDTO, filter: ReportFilterDTO = {}, allReports = false) {
    return this.http.get<ReportDTO[]>(`${this.url}/table`, {
      params: new HttpParams({
        fromObject: { ...this.filterParams(filter), ...this.lazyParams(lazyDto), ...(allReports ? { allReports: true } : {}) },
      }),
    });
  }

  /** the numbers of the stats cards of the reports table, about the same reports as `getTable` */
  public getTableStats(allReports = false) {
    return this.http.get<ReportHistoryStatsDTO>(`${this.url}/table/stats`, {
      params: new HttpParams({ fromObject: allReports ? { allReports: true } : {} }),
    });
  }

  public getStats() {
    return this.http.get<MaintenanceStatsDTO>(`${this.url}/stats`);
  }

  /** `id` is the guid or the short number of the report ("968FCB28" / "#968FCB28", the backend resolves both) */
  public getById(id: string) {
    // encoded: a "#" is not a valid part of a url path
    return this.http.get<ReportDTO>(`${this.url}/${encodeURIComponent(id)}`);
  }

  public createForLocation(dto: ReportCreateForLocationDTO) {
    return this.http.post<ReportCreateResponseDTO>(this.url, this.toFormData(dto));
  }

  public createForCoordinates(dto: ReportCreateForCoordinatesDTO) {
    return this.http.post<ReportCreateResponseDTO>(`${this.url}/coordinates`, this.toFormData(dto));
  }

  public updateState(dto: ReportUpdateStateDTO) {
    return this.http.put<ReportDTO>(`${this.url}/state`, dto);
  }

  public rate(dto: ReportRateDTO) {
    return this.http.put<ReportDTO>(`${this.url}/rate`, dto);
  }

  public delete(id: string) {
    return this.http.delete<void>(`${this.url}/${id}`);
  }
}
