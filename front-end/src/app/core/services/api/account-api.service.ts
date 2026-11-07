import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { URLs } from '../../constants/URLs';
import { RegisterDTO } from '../../DTOs/account/register-dto';
import { MeDTO } from '../../DTOs/account/me-dto';

@Injectable({ providedIn: 'root' })
export class AccountApiService {
  // DI
  private http = inject(HttpClient);

  // private
  private readonly url = URLs.baseApi + '/account';

  // methods
  public register(registerDTO: RegisterDTO) {
    return this.http.post(`${this.url}/register`, registerDTO);
  }

  public me() {
    return this.http.get<MeDTO>(`${this.url}/me`);
  }
}
