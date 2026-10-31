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

  /** Validates credentials and sends an email code; authentication completes after verification. */
  public login(loginDTO: LoginDTO): Observable<unknown> {
    return this.authApi
      .login({ ...loginDTO, studentId: loginDTO.studentId.trim() })
      .pipe(toastError(this.toast, 'تعذر تسجيل الدخول', (e) => this.loginErrorMessage(e)));
  }

  public verifyEmail(studentId: string, code: string): Observable<MeDTO> {
    return this.authApi.verifyEmail(studentId, code).pipe(
      toastError(this.toast, 'تعذر التحقق من الرمز'),
      switchMap(() => this.loadCurrentUser()),
    );
  }

  public resendCode(studentId: string): Observable<unknown> {
    return this.authApi.resendCode(studentId).pipe(toastError(this.toast, 'تعذر إرسال رمز جديد'));
  }

  /** Loads the authenticated user after verification or when restoring a session. */
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
    try {
      localStorage.removeItem(USER_KEY);
    } catch {
      /* Storage can be disabled. */
    }
  }

  private setUser(user: MeDTO): void {
    this._currentUser.set(user);
    try {
      localStorage.setItem(USER_KEY, JSON.stringify(user));
    } catch {
      /* The in-memory session remains usable. */
    }
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
        return 'طلبات متكررة. انتظر قليلاً قبل طلب رمز آخر، أو حاول لاحقاً إذا كان الحساب مقفلاً مؤقتاً';
      default:
        return defaultErrorReason(error);
    }
  }
}
