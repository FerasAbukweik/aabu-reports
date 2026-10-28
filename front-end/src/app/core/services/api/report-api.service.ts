import { HttpClient, HttpParams } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { URLs } from '../../constants/URLs';
import { LazyDTO } from '../../DTOs/lazy/lazy-dto';
import { ReportCreateForCoordinatesDTO } from '../../DTOs/report/report-create-for-coordinates-dto';
import { ReportCreateForLocationDTO } from '../../DTOs/report/report-create-for-location-dto';
import { ReportCreateResponseDTO } from '../../DTOs/report/report-create-response-dto';
import { ReportDTO } from '../../DTOs/report/report-dto';
import { ReportRateDTO } from '../../DTOs/report/report-rate-dto';
import { ReportUpdateStateDTO } from '../../DTOs/report/report-update-state-dto';

@Injectable({ providedIn: 'root' })
export class ReportApiService {
  // DI
  private http = inject(HttpClient);

  // private
  private readonly url = URLs.baseApi + '/report';

  /** the backend LazyDTO is `Skip` / `Take`: `taken` is sent as `skip`, `sectionSize` as `take` */
  private lazyParams({ taken, sectionSize }: LazyDTO) {
    return { skip: taken, take: sectionSize };
  }

  // methods

  /** admin only */
  public getAll(lazyDto: LazyDTO) {
    return this.http.get<ReportDTO[]>(this.url, { params: new HttpParams({ fromObject: this.lazyParams(lazyDto) }) });
  }

  /** one page of the reports of a location */
  public getByLocation(locationId: string, lazyDto: LazyDTO) {
    return this.http.get<ReportDTO[]>(`${this.url}/location`, {
      params: new HttpParams({ fromObject: { locationId, ...this.lazyParams(lazyDto) } }),
    });
  }

  /** one page of the reports of the current user */
  public getMine(lazyDto: LazyDTO) {
    return this.http.get<ReportDTO[]>(`${this.url}/mine`, {
      params: new HttpParams({ fromObject: this.lazyParams(lazyDto) }),
    });
  }

  public getById(id: string) {
    return this.http.get<ReportDTO>(`${this.url}/${id}`);
  }

  public createForLocation(dto: ReportCreateForLocationDTO) {
    return this.http.post<ReportCreateResponseDTO>(this.url, dto);
  }

  public createForCoordinates(dto: ReportCreateForCoordinatesDTO) {
    return this.http.post<ReportCreateResponseDTO>(`${this.url}/coordinates`, dto);
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
