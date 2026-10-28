import { HttpErrorResponse } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable, switchMap } from 'rxjs';
import { MeDTO } from '../../DTOs/account/me-dto';
import { RegisterDTO } from '../../DTOs/account/register-dto';
import { toastError } from '../../utils/error-toast';
import { AccountApiService } from '../api/account-api.service';
import { ToastService } from '../local/toast.service';
import { AuthService } from './auth.service';

@Injectable({ providedIn: 'root' })
export class AccountService {
  // DI
  private accountApi = inject(AccountApiService);
  private authService = inject(AuthService);
  private toast = inject(ToastService);

  // methods

  /**
   * Creates the account (the api also signs the user in by setting the auth cookies), then loads
   * the current user (GET /account/me) like login does.
   * The returned observable only emits after "me" has loaded.
   * On failure it shows an error toast and rethrows the error.
   */
  public register(registerDTO: RegisterDTO): Observable<MeDTO> {
    return this.accountApi
      .register({
        ...registerDTO,
        studentId: registerDTO.studentId.trim(),
        email: registerDTO.email.trim(),
        fullName: registerDTO.fullName.trim(),
      })
      .pipe(
        toastError(this.toast, 'تعذر إنشاء الحساب', (e) => this.registerErrorMessage(e)),
        switchMap(() => this.authService.loadCurrentUser()),
      );
  }

  // private
  private registerErrorMessage(error: HttpErrorResponse): string {
    switch (error.status) {
      case 0:
        return 'تعذّر الاتصال بالخادم، تحقق من اتصالك بالإنترنت وحاول مرة أخرى';
      case 400:
        return 'تحقق من البيانات المدخلة (الرقم الجامعي 10 أرقام، وكلمة السر 8 خانات على الأقل)';
      case 409:
        // the api answers e.g. "Already in use: student id, email"
        return typeof error.error === 'string' ? error.error : 'Already in use';
      default:
        return 'حدث خطأ غير متوقع، حاول مرة أخرى لاحقًا';
    }
  }
}
