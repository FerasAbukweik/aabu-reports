import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  ElementRef,
  computed,
  inject,
  signal,
  viewChildren,
} from '@angular/core';
import {
  AbstractControl,
  FormBuilder,
  ReactiveFormsModule,
  ValidationErrors,
  Validators,
} from '@angular/forms';

type AuthTab = 'login' | 'activate';
type LoginState = 'idle' | 'loading' | 'success';

const OTP_LENGTH = 6;
const OTP_SECONDS = 45;
const PASSWORD_MIN_LENGTH = 8;
const JORDAN_MOBILE_PATTERN = /^07[789]\d{7}$/;

function passwordsMatch(group: AbstractControl): ValidationErrors | null {
  const password = group.get('password')?.value;
  const confirm = group.get('confirmPassword')?.value;
  return password && confirm && password !== confirm ? { passwordMismatch: true } : null;
}

@Component({
  selector: 'app-login-signup',
  standalone: true,
  imports: [ReactiveFormsModule],
  templateUrl: './login-signup.component.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class LoginSignupComponent {
  private readonly fb = inject(FormBuilder);
  private readonly destroyRef = inject(DestroyRef);

  // ---------- UI state ----------
  readonly activeTab = signal<AuthTab>('login'); 
  readonly loginState = signal<LoginState>('idle');

  readonly showLoginPassword = signal(false);
  readonly showNewPassword = signal(false);
  readonly showConfirmPassword = signal(false);

  // ---------- Forms ----------
  readonly loginForm = this.fb.nonNullable.group({
    studentId: ['', [Validators.required]],
    password: ['', [Validators.required]],
    remember: [true],
  });

  readonly activateForm = this.fb.nonNullable.group(
    {
      fullName: ['', [Validators.required]],
      nationalId: ['', [Validators.required]],
      birthDate: ['', [Validators.required]],
      phone: ['', [Validators.required, Validators.pattern(JORDAN_MOBILE_PATTERN)]],
      password: ['', [Validators.required, Validators.minLength(PASSWORD_MIN_LENGTH)]],
      confirmPassword: ['', [Validators.required]],
    },
    { validators: passwordsMatch },
  );

  // ---------- OTP ----------
  readonly otpIndexes = Array.from({ length: OTP_LENGTH }, (_, i) => i);
  readonly otpDigits = signal<string[]>(Array(OTP_LENGTH).fill(''));
  readonly otpCode = computed(() => this.otpDigits().join(''));
  readonly otpInputs = viewChildren<ElementRef<HTMLInputElement>>('otpInput');

  readonly secondsLeft = signal(OTP_SECONDS);
  readonly countdownLabel = computed(() => {
    const s = this.secondsLeft();
    return `00:${s.toString().padStart(2, '0')}`;
  });

  private timerId: ReturnType<typeof setInterval> | null = null;
  private loginTimeoutId: ReturnType<typeof setTimeout> | null = null;

  constructor() {
    this.startOtpTimer();

    this.destroyRef.onDestroy(() => {
      this.stopOtpTimer();
      if (this.loginTimeoutId) clearTimeout(this.loginTimeoutId);
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

  activateError(name: keyof typeof this.activateForm.controls): string | null {
    const control = this.activateForm.controls[name];
    if (!control.touched) return null;

    if (name === 'confirmPassword' && !control.errors && this.activateForm.hasError('passwordMismatch')) {
      return 'كلمتا السر غير متطابقتين';
    }
    if (control.hasError('required')) return 'هذا الحقل مطلوب';
    if (control.hasError('pattern')) return 'رقم الهاتف غير صالح (مثال: 079XXXXXXX)';
    if (control.hasError('minlength')) return `كلمة السر يجب ألا تقل عن ${PASSWORD_MIN_LENGTH} خانات`;
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

    // TODO: connect to API (this.loginForm.getRawValue())
    this.loginTimeoutId = setTimeout(() => {
      this.loginState.set('success');
      this.loginTimeoutId = setTimeout(() => this.loginState.set('idle'), 1200);
    }, 1000);
  }

  onActivate(): void {
    if (this.activateForm.invalid) {
      this.activateForm.markAllAsTouched();
      return;
    }

    // TODO: connect to API (this.activateForm.getRawValue()) then send the OTP
    this.startOtpTimer();
    this.focusOtp(0);
  }

  onResendOtp(): void {
    // TODO: connect to API
    this.startOtpTimer();
  }

  openHelp(): void {
    // TODO: open forgot-password help (dialog or route)
  }

  // ---------- OTP handlers ----------
  onOtpInput(index: number, event: Event): void {
    const input = event.target as HTMLInputElement;
    const digit = input.value.replace(/\D/g, '').slice(-1);
    input.value = digit;
    this.setDigit(index, digit);

    if (digit && index < OTP_LENGTH - 1) {
      this.focusOtp(index + 1);
    }
  }

  onOtpKeydown(index: number, event: KeyboardEvent): void {
    if (event.key === 'Backspace' && !this.otpDigits()[index] && index > 0) {
      event.preventDefault();
      this.setDigit(index - 1, '');
      this.focusOtp(index - 1);
    }
  }

  onOtpPaste(event: ClipboardEvent): void {
    event.preventDefault();
    const pasted = (event.clipboardData?.getData('text') ?? '').replace(/\D/g, '').slice(0, OTP_LENGTH);
    if (!pasted) return;

    this.otpDigits.set(Array.from({ length: OTP_LENGTH }, (_, i) => pasted[i] ?? ''));
    this.focusOtp(Math.min(pasted.length, OTP_LENGTH - 1));
  }

  // ---------- Helpers ----------
  private setDigit(index: number, digit: string): void {
    this.otpDigits.update((digits) => digits.map((d, i) => (i === index ? digit : d)));
  }

  private focusOtp(index: number): void {
    const el = this.otpInputs()[index]?.nativeElement;
    el?.focus();
    el?.select();
  }

  private startOtpTimer(): void {
    this.stopOtpTimer();
    this.secondsLeft.set(OTP_SECONDS);

    this.timerId = setInterval(() => {
      this.secondsLeft.update((s) => Math.max(0, s - 1));
      if (this.secondsLeft() === 0) this.stopOtpTimer();
    }, 1000);
  }

  private stopOtpTimer(): void {
    if (this.timerId) {
      clearInterval(this.timerId);
      this.timerId = null;
    }
  }
}