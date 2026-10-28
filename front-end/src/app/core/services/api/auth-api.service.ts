// auth-api.service.ts
import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { URLs } from '../../constants/URLs';
import { LoginDTO } from '../../DTOs/auth/login-dto';

@Injectable({ providedIn: 'root' })
export class AuthApiService {
  // DI
  private http = inject(HttpClient);

  // private
  private readonly url = URLs.baseApi + '/auth';

  // methods
  public login(loginDTO: LoginDTO) {
    return this.http.post(`${this.url}/login`, loginDTO);
  }

  public refresh() {
    return this.http.post(`${this.url}/refresh`, {});
  }

  public logout() {
    return this.http.post(`${this.url}/logout`, {});
  }
}
