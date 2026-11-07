import { TestBed } from '@angular/core/testing';
import { firstValueFrom, of, Subject, throwError } from 'rxjs';
import { AccountApiService } from '../api/account-api.service';
import { AuthApiService } from '../api/auth-api.service';
import { ToastService } from '../local/toast.service';
import { AuthService } from './auth.service';

const user = {
  userId: 'id',
  studentId: '1234567890',
  email: 'student@example.com',
  roles: ['Student'],
};

describe('AuthService session', () => {
  const account = { me: vi.fn() };
  const auth = { verifyEmail: vi.fn(), resendCode: vi.fn() };

  beforeEach(() => {
    vi.resetAllMocks();
    TestBed.configureTestingModule({
      providers: [
        { provide: AccountApiService, useValue: account },
        { provide: AuthApiService, useValue: auth },
        { provide: ToastService, useValue: { error: vi.fn() } },
      ],
    });
  });

  it('starts unauthenticated and loads Me before trusting the session, then reuses it in memory', async () => {
    const response = new Subject<typeof user>();
    account.me.mockReturnValue(response);
    const service = TestBed.inject(AuthService);
    expect(service.isLoggedIn()).toBe(false);
    const pending = firstValueFrom(service.loadCurrentUser());
    expect(account.me).toHaveBeenCalledTimes(1);
    expect(service.currentUser()).toBeNull();
    response.next(user);
    expect(await pending).toEqual(user);
    expect(service.isLoggedIn()).toBe(true);
    expect(await firstValueFrom(service.loadCurrentUser())).toEqual(user);
    expect(account.me).toHaveBeenCalledTimes(1);

    service.clearUser();
    account.me.mockReturnValue(of(user));
    await firstValueFrom(service.loadCurrentUser());
    expect(account.me).toHaveBeenCalledTimes(2);
  });

  it('stays unauthenticated when Me fails and can retry', async () => {
    account.me
      .mockReturnValueOnce(throwError(() => new Error('unauthenticated')))
      .mockReturnValueOnce(of(user));
    const service = TestBed.inject(AuthService);
    await expect(firstValueFrom(service.loadCurrentUser())).rejects.toThrow('unauthenticated');
    expect(service.currentUser()).toBeNull();
    await firstValueFrom(service.loadCurrentUser());
    expect(service.currentUser()).toEqual(user);
  });

  it('passes the submitted registration ID to verification and resend without a prior login', async () => {
    auth.verifyEmail.mockReturnValue(of(null));
    auth.resendCode.mockReturnValue(of(null));
    account.me.mockReturnValue(of(user));
    const service = TestBed.inject(AuthService);
    await firstValueFrom(service.verifyEmail(user.studentId, '123456'));
    await firstValueFrom(service.resendCode(user.studentId));
    expect(auth.verifyEmail).toHaveBeenCalledWith(user.studentId, '123456');
    expect(auth.resendCode).toHaveBeenCalledWith(user.studentId);
    expect(service.currentUser()).toEqual(user);
  });
});
