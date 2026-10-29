import { HttpClient, HttpParams } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { URLs } from '../../constants/URLs';
import { LazyDTO } from '../../DTOs/lazy/lazy-dto';
import { MessageDTO } from '../../DTOs/message/message-dto';

@Injectable({ providedIn: 'root' })
export class MessageApiService {
  // DI
  private http = inject(HttpClient);

  // private
  private readonly url = URLs.baseApi + '/message';

  /** the backend LazyDTO is `Skip` / `Take`: `taken` is sent as `skip`, `sectionSize` as `take` */
  private lazyParams({ taken, sectionSize }: LazyDTO) {
    return { skip: taken, take: sectionSize };
  }

  // methods

  /** one page of the chat of a report, NEWEST first (the new messages arrive live through the MessageHub) */
  public getByReport(reportId: string, lazyDto: LazyDTO) {
    return this.http.get<MessageDTO[]>(this.url, {
      params: new HttpParams({ fromObject: { reportId, ...this.lazyParams(lazyDto) } }),
    });
  }
}
