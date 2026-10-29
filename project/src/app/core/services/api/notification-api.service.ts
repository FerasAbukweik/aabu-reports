import { HttpClient, HttpParams } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { URLs } from '../../constants/URLs';
import { LazyDTO } from '../../DTOs/lazy/lazy-dto';
import { NotificationDTO } from '../../DTOs/notification/notification-dto';

@Injectable({ providedIn: 'root' })
export class NotificationApiService {
  // DI
  private http = inject(HttpClient);

  // private
  private readonly url = URLs.baseApi + '/notification';

  /** the backend LazyDTO is `Skip` / `Take`: `taken` is sent as `skip`, `sectionSize` as `take` */
  private lazyParams({ taken, sectionSize }: LazyDTO) {
    return { skip: taken, take: sectionSize };
  }

  // methods

  /** one page of the notifications of the current user, newest first */
  public getMine(lazyDto: LazyDTO, unreadOnly = false) {
    return this.http.get<NotificationDTO[]>(this.url, {
      params: new HttpParams({ fromObject: { ...this.lazyParams(lazyDto), unreadOnly } }),
    });
  }

  /** how many notifications the current user did not see yet */
  public getUnreadCount() {
    return this.http.get<number>(`${this.url}/unread-count`);
  }

  public markAllAsRead() {
    return this.http.put<void>(`${this.url}/read-all`, {});
  }
}
