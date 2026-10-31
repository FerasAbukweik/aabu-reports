import { HttpErrorResponse } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { RegisterDTO } from '../../DTOs/account/register-dto';
import { defaultErrorReason, toastError } from '../../utils/error-toast';
import { AccountApiService } from '../api/account-api.service';
import { ToastService } from '../local/toast.service';

@Injectable({ providedIn: 'root' })
export class AccountService {
  // DI
  private accountApi = inject(AccountApiService);
  private toast = inject(ToastService);

  // methods

  /** Creates the account and sends its email verification code. */
  public register(registerDTO: RegisterDTO): Observable<unknown> {
    return this.accountApi
      .register({
        ...registerDTO,
        studentId: registerDTO.studentId.trim(),
        email: registerDTO.email.trim(),
        fullName: registerDTO.fullName.trim(),
      })
      .pipe(toastError(this.toast, 'تعذر إنشاء الحساب', (e) => this.registerErrorMessage(e)));
  }

  // private
  private registerErrorMessage(error: HttpErrorResponse): string {
    switch (error.status) {
      case 400:
        return 'تحقق من البيانات المدخلة (الرقم الجامعي 10 أرقام، وكلمة السر 8 خانات على الأقل)';
      case 409:
        // the api answers e.g. "Already in use: student id, email"
        return typeof error.error === 'string' ? error.error : 'Already in use';
      default:
        return defaultErrorReason(error);
    }
  }
}
