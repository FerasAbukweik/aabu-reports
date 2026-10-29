import { HttpErrorResponse } from '@angular/common/http';
import { computed, inject, Injectable, signal } from '@angular/core';
import { Observable, switchMap, tap } from 'rxjs';
import { LoginDTO } from '../../DTOs/auth/login-dto';
import { MeDTO } from '../../DTOs/account/me-dto';
import { AccountApiService } from '../api/account-api.service';
import { defaultErrorReason, toastError } from '../../utils/error-toast';
import { AuthApiService } from '../api/auth-api.service';
import { ToastService } from '../local/toast.service';

const USER_KEY = 'user';

@Injectable({ providedIn: 'root' })
export class AuthService {
  // DI
  private authApi = inject(AuthApiService);
  private accountApi = inject(AccountApiService);
  private toast = inject(ToastService);

  // state
  private readonly _currentUser = signal<MeDTO | null>(null);
  public readonly currentUser = this._currentUser.asReadonly();

  public readonly isLoggedIn = computed(() => this._currentUser() !== null);

  constructor() {
    this._currentUser.set(this.readStoredUser());
  }

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
      tap((user) => this.setUser(user)),
      toastError(this.toast, 'تعذر تحميل بيانات الحساب'),
    );
  }

  /** Revokes the refresh token and clears the cookies. Shows an error toast and rethrows if it fails. */
  public logout(): Observable<unknown> {
    return this.authApi.logout().pipe(
      toastError(this.toast, 'تعذر تسجيل الخروج'),
      // the page reloads so every service starts from a clean state
      tap(() => {
        this.clearUser();
        window.location.reload();
      }),
    );
  }

  public refresh(): Observable<unknown> {
    return this.authApi.refresh();
  }

  /** Forgets the user (signal and local storage), so the guards send them to the login page. */
  public clearUser(): void {
    this._currentUser.set(null);
    localStorage.removeItem(USER_KEY);
  }

  private setUser(user: MeDTO): void {
    this._currentUser.set(user);
    localStorage.setItem(USER_KEY, JSON.stringify(user));
  }

  private readStoredUser(): MeDTO | null {
    try {
      const stored = localStorage.getItem(USER_KEY);
      return stored ? (JSON.parse(stored) as MeDTO) : null;
    } catch {
      // storage unavailable or the value is broken: start logged out
      return null;
    }
  }

  private loginErrorMessage(error: HttpErrorResponse): string {
    switch (error.status) {
      case 400:
        return 'تحقق من الرقم الجامعي (10 أرقام) وكلمة المرور ثم حاول مرة أخرى';
      case 401:
        return 'الرقم الجامعي أو كلمة المرور غير صحيحة';
      case 429:
        return 'تم قفل الحساب مؤقتًا بسبب محاولات متكررة، حاول مرة أخرى لاحقًا';
      default:
        return defaultErrorReason(error);
    }
  }
}
