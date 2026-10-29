import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  inject,
  signal,
} from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import {
  AbstractControl,
  FormBuilder,
  ReactiveFormsModule,
  ValidationErrors,
  Validators,
} from '@angular/forms';
import { AccountService } from '../../core/services/api-services/account.service';
import { RegularExpression } from '../../core/constants/regular-expression';
import { AuthService } from '../../core/services/api-services/auth.service';
import { OtpComponent } from '../../shared/components/otp/otp.component';
import { Router } from '@angular/router';
import { AuthTab, SubmitState } from './login-signup.model';

const PASSWORD_MIN_LENGTH = 8;

/** how long the success state of the button stays on the screen before the page changes */
const REDIRECT_DELAY_MS = 1200;

function passwordsMatch(group: AbstractControl): ValidationErrors | null {
  const password = group.get('password')?.value;
  const confirm = group.get('confirmPassword')?.value;
  return password && confirm && password !== confirm ? { passwordMismatch: true } : null;
}

@Component({
  selector: 'app-login-signup',
  imports: [ReactiveFormsModule, OtpComponent],
  templateUrl: './login-signup.component.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class LoginSignupComponent {
  // DI
  private readonly _fb = inject(FormBuilder);
  private readonly _destroyRef = inject(DestroyRef);
  private readonly _authService = inject(AuthService);
  private readonly _accountService = inject(AccountService);
  private readonly _router = inject(Router);

  // ---------- UI state ----------
  readonly activeTab = signal<AuthTab>('login');
  readonly loginState = signal<SubmitState>('idle');
  readonly registerState = signal<SubmitState>('idle');

  readonly showLoginPassword = signal(false);
  readonly showNewPassword = signal(false);
  readonly showConfirmPassword = signal(false);

  // ---------- Forms ----------
  readonly loginForm = this._fb.nonNullable.group({
    studentId: ['', [Validators.required]],
    password: ['', [Validators.required]],
  });

  readonly registerForm = this._fb.nonNullable.group(
    {
      fullName: ['', [Validators.required, Validators.minLength(2), Validators.maxLength(150)]],
      studentId: ['', [Validators.required, Validators.pattern(RegularExpression.StudentId)]],
      email: [
        '',
        [
          Validators.required,
          Validators.maxLength(150),
          Validators.pattern(RegularExpression.Email),
        ],
      ],
      password: ['', [Validators.required, Validators.minLength(PASSWORD_MIN_LENGTH)]],
      confirmPassword: ['', [Validators.required]],
    },
    { validators: passwordsMatch },
  );

  private redirectTimeoutId: ReturnType<typeof setTimeout> | null = null;

  constructor() {
    this._destroyRef.onDestroy(() => {
      if (this.redirectTimeoutId) clearTimeout(this.redirectTimeoutId);
    });
  }

  // ---------- Tabs ----------
  setTab(tab: AuthTab): void {
    this.activeTab.set(tab);
  }

  // ---------- Password visibility ----------
  toggle(field: 'login' | 'new' | 'confirm'): void {
    const target =
      field === 'login'
        ? this.showLoginPassword
        : field === 'new'
          ? this.showNewPassword
          : this.showConfirmPassword;
    target.update((v) => !v);
  }

  // ---------- Validation messages ----------
  loginError(name: keyof typeof this.loginForm.controls): string | null {
    const control = this.loginForm.controls[name];
    if (!control.touched || control.valid) return null;
    return 'هذا الحقل مطلوب';
  }

  registerError(name: keyof typeof this.registerForm.controls): string | null {
    const control = this.registerForm.controls[name];
    if (!control.touched) return null;

    if (
      name === 'confirmPassword' &&
      !control.errors &&
      this.registerForm.hasError('passwordMismatch')
    ) {
      return 'كلمتا السر غير متطابقتين';
    }
    if (control.hasError('required')) return 'هذا الحقل مطلوب';
    if (control.hasError('pattern')) {
      if (name === 'studentId') return 'الرقم الجامعي يجب أن يتكون من 10 أرقام';
      if (name === 'email') return 'البريد الإلكتروني غير صالح';
      return 'القيمة المدخلة غير صالحة';
    }
    if (control.hasError('minlength')) {
      return name === 'fullName'
        ? 'الاسم قصير جداً'
        : `كلمة السر يجب ألا تقل عن ${PASSWORD_MIN_LENGTH} خانات`;
    }
    return null;
  }

  // ---------- Actions ----------
  onLogin(): void {
    if (this.loginForm.invalid) {
      this.loginForm.markAllAsTouched();
      return;
    }
    if (this.loginState() !== 'idle') return;

    this.loginState.set('loading');

    const { studentId, password } = this.loginForm.getRawValue();

    this._authService
      .login({ studentId, password })
      .pipe(takeUntilDestroyed(this._destroyRef))
      .subscribe({
        next: () => {
          // logged in and the current user ("me") is loaded, so it is safe to move on
          this.loginState.set('success');
          this.goToApp();
        },
        // the service already showed the error toast, only reset the button
        error: () => this.loginState.set('idle'),
      });
  }

  onRegister(): void {
    if (this.registerForm.invalid) {
      this.registerForm.markAllAsTouched();
      return;
    }
    if (this.registerState() !== 'idle') return;

    this.registerState.set('loading');

    const { fullName, studentId, email, password } = this.registerForm.getRawValue();

    this._accountService
      .register({ fullName, studentId, email, password })
      .pipe(takeUntilDestroyed(this._destroyRef))
      .subscribe({
        next: () => {
          // account created and the current user ("me") is loaded, same as login
          this.registerState.set('success');
          this.goToApp();
        },
        // the service already showed the error toast, only reset the button
        error: () => this.registerState.set('idle'),
      });
  }

  private goToApp(): void {
    this.redirectTimeoutId = setTimeout(() => this._router.navigate(['/report']), REDIRECT_DELAY_MS);
  }

  onResendOtp(): void {
    // the otp component already restarted its countdown
    // TODO: connect to API
  }

  onOtpCompleted(_code: string): void {
    // TODO: verify the code with the API, and clear the otp component if it is wrong
  }

  openHelp(): void {
    // TODO: open forgot-password help (dialog or route)
  }
}
