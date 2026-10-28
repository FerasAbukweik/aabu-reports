// auth.service.ts
import { HttpErrorResponse } from '@angular/common/http';
import { computed, inject, Injectable, signal } from '@angular/core';
import { firstValueFrom, Observable, switchMap, tap } from 'rxjs';
import { LoginDTO } from '../../DTOs/auth/login-dto';
import { MeDTO } from '../../DTOs/account/me-dto';
import { AccountApiService } from '../api/account-api.service';
import { toastError } from '../../utils/error-toast';
import { AuthApiService } from '../api/auth-api.service';
import { ToastService } from '../local/toast.service';

@Injectable({ providedIn: 'root' })
export class AuthService {
  // DI
  private authApi = inject(AuthApiService);
  private accountApi = inject(AccountApiService);
  private toast = inject(ToastService);

  // state
  private readonly _currentUser = signal<MeDTO | null>(null);
  public readonly currentUser = this._currentUser.asReadonly();

  // methods

  /**
   * Logs in, then loads the current user (GET /account/me) and saves it in local storage.
   * The returned observable only emits after "me" has loaded, so the caller can keep showing
   * its loading state until then. On failure it shows an error toast and rethrows the error.
   */
  public login(loginDTO: LoginDTO): Observable<MeDTO> {
    return this.authApi.login({ ...loginDTO, studentId: loginDTO.studentId.trim() }).pipe(
      toastError(this.toast, 'تعذر تسجيل الدخول', (e) => this.loginErrorMessage(e)),
      switchMap(() => this.loadCurrentUser()),
    );
  }

  /**
   * Loads the current user (GET /account/me) and saves it in local storage.
   * Used by login here and by register in AccountService.
   * On failure it shows an error toast and rethrows the error.
   */
  public loadCurrentUser(): Observable<MeDTO> {
    return this.accountApi.me().pipe(
      tap((user) => this._currentUser.set(user)),
      toastError(this.toast, 'تعذر تحميل بيانات الحساب'),
    );
  }

  public async isLoggedIn() {
    if (this._currentUser() !== null) return true;

    try {
      // no toast here: the auth guard calls this and a 401 is expected for a guest
      await firstValueFrom(this.accountApi.me().pipe(tap((user) => this._currentUser.set(user))));
      return true;
    } catch {
      return false;
    }
  }

  /** Revokes the refresh token and clears the cookies. Shows an error toast and rethrows if it fails. */
  public logout(): Observable<unknown> {
    return this.authApi.logout().pipe(
      toastError(this.toast, 'تعذر تسجيل الخروج'),
      tap({ next: () => window.location.reload() }),
    );
  }

  public refresh(): Observable<unknown> {
    return this.authApi.refresh();
  }

  private loginErrorMessage(error: HttpErrorResponse): string {
    switch (error.status) {
      case 0:
        return 'تعذّر الاتصال بالخادم، تحقق من اتصالك بالإنترنت وحاول مرة أخرى';
      case 400:
        return 'تحقق من الرقم الجامعي (10 أرقام) وكلمة المرور ثم حاول مرة أخرى';
      case 401:
        return 'الرقم الجامعي أو كلمة المرور غير صحيحة';
      case 429:
        return 'تم قفل الحساب مؤقتًا بسبب محاولات متكررة، حاول مرة أخرى لاحقًا';
      default:
        return 'حدث خطأ غير متوقع، حاول مرة أخرى لاحقًا';
    }
  }
}
