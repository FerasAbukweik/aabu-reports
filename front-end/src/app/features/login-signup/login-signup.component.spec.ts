import { TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import { Subject, of, throwError } from 'rxjs';
import { vi } from 'vitest';
import { LoginSignupComponent } from './login-signup.component';
import { AuthService } from '../../core/services/api-services/auth.service';
import { AccountService } from '../../core/services/api-services/account.service';

describe('LoginSignup verification flow', () => {
  const auth = { login: vi.fn(), verifyEmail: vi.fn(), resendCode: vi.fn() };
  const account = { register: vi.fn() };
  const router = { navigate: vi.fn() };

  beforeEach(() => {
    vi.useFakeTimers();
    vi.resetAllMocks();
    TestBed.configureTestingModule({
      imports: [LoginSignupComponent],
      providers: [
        { provide: AuthService, useValue: auth },
        { provide: AccountService, useValue: account },
        { provide: Router, useValue: router },
      ],
    }).overrideComponent(LoginSignupComponent, { set: { template: '' } });
  });

  afterEach(() => {
    TestBed.resetTestingModule();
    vi.useRealTimers();
  });

  function createComponent() {
    const fixture = TestBed.createComponent(LoginSignupComponent);
    const component = fixture.componentInstance;
    component.loginForm.setValue({ studentId: '1234567890', password: 'Password1!' });
    return { fixture, component };
  }

  it('waits for verification, resets initial success after 1.2s, then redirects 1.2s after verification', () => {
    const login = new Subject<unknown>();
    const verify = new Subject<unknown>();
    auth.login.mockReturnValue(login);
    auth.verifyEmail.mockReturnValue(verify);
    const { component } = createComponent();

    component.onLogin();
    expect(component.loginState()).toBe('loading');
    login.next(null);
    expect(component.verificationTab()).toBe('login');
    expect(component.verificationNoticeOpen()).toBe(true);
    component.dismissVerificationNotice();
    expect(component.verificationNoticeOpen()).toBe(false);
    expect(component.verificationTab()).toBe('login');
    expect(component.loginState()).toBe('success');
    expect(router.navigate).not.toHaveBeenCalled();
    vi.advanceTimersByTime(1199);
    expect(component.loginState()).toBe('success');
    vi.advanceTimersByTime(1);
    expect(component.loginState()).toBe('idle');

    component.onOtpCompleted('123456');
    expect(component.loginState()).toBe('loading');
    component.onOtpCompleted('123456');
    expect(auth.verifyEmail).toHaveBeenCalledTimes(1);
    expect(auth.verifyEmail).toHaveBeenCalledWith('1234567890', '123456');
    verify.next(null);
    expect(component.loginState()).toBe('success');
    expect(component.verificationState()).toBe('success');
    vi.advanceTimersByTime(1199);
    expect(router.navigate).not.toHaveBeenCalled();
    vi.advanceTimersByTime(1);
    expect(router.navigate).toHaveBeenCalledWith(['/report']);
  });

  it('uses the same verification flow for registration', () => {
    account.register.mockReturnValue(of(null));
    auth.verifyEmail.mockReturnValue(of(null));
    const { component } = createComponent();
    component.setTab('register');
    component.registerForm.setValue({
      fullName: 'Student Name',
      studentId: '1234567890',
      email: 'student@example.com',
      password: 'Password1!',
      confirmPassword: 'Password1!',
    });

    component.onRegister();
    expect(component.verificationTab()).toBe('register');
    expect(component.registerState()).toBe('success');
    vi.advanceTimersByTime(1200);
    expect(component.registerState()).toBe('idle');
    component.onOtpCompleted('123456');
    expect(component.registerState()).toBe('success');
    vi.advanceTimersByTime(1200);
    expect(router.navigate).toHaveBeenCalledWith(['/report']);
  });

  it('allows retry after a wrong code without redirecting', () => {
    auth.login.mockReturnValue(of(null));
    auth.verifyEmail.mockReturnValue(throwError(() => new Error('wrong code')));
    const { component } = createComponent();
    component.onLogin();
    vi.advanceTimersByTime(1200);
    component.onOtpCompleted('000000');

    expect(component.loginState()).toBe('idle');
    expect(component.verificationState()).toBe('idle');
    expect(component.verificationTab()).toBe('login');
    vi.advanceTimersByTime(5000);
    expect(router.navigate).not.toHaveBeenCalled();
  });

  it('cancels the redirect when the component is destroyed', () => {
    auth.login.mockReturnValue(of(null));
    auth.verifyEmail.mockReturnValue(of(null));
    const { fixture, component } = createComponent();
    component.onLogin();
    vi.advanceTimersByTime(1200);
    component.onOtpCompleted('123456');
    fixture.destroy();
    vi.advanceTimersByTime(1200);
    expect(router.navigate).not.toHaveBeenCalled();
  });
});
