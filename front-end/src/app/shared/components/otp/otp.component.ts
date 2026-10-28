import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  ElementRef,
  OnInit,
  computed,
  inject,
  input,
  output,
  signal,
  viewChildren,
} from '@angular/core';

@Component({
  selector: 'app-otp',
  standalone: true,
  templateUrl: './otp.component.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class OtpComponent implements OnInit {
  private readonly destroyRef = inject(DestroyRef);

  // ---------- API ----------
  /** number of digits */
  readonly length = input(6);
  /** countdown before the code can be re-sent, in seconds */
  readonly seconds = input(45);
  /** phone number shown in the card */
  readonly phone = input('+962 79XXXXXXX');

  /** emits the full code as soon as the last digit is entered */
  readonly completed = output<string>();
  /** emits when the user asks for a new code (the timer is restarted by the component itself) */
  readonly resend = output<void>();

  // ---------- state ----------
  private readonly entered = signal<string[]>([]);

  readonly indexes = computed(() => Array.from({ length: this.length() }, (_, i) => i));
  readonly digits = computed(() => this.indexes().map((i) => this.entered()[i] ?? ''));
  readonly code = computed(() => this.digits().join(''));

  readonly inputs = viewChildren<ElementRef<HTMLInputElement>>('otpInput');

  readonly secondsLeft = signal(0);
  readonly countdownLabel = computed(() => {
    const s = this.secondsLeft();
    return `00:${s.toString().padStart(2, '0')}`;
  });

  private timerId: ReturnType<typeof setInterval> | null = null;

  ngOnInit(): void {
    this.startTimer();
    this.destroyRef.onDestroy(() => this.stopTimer());
  }

  // ---------- methods for the parent (via viewChild) ----------

  /** clears the digits, restarts the countdown and focuses the first box */
  restart(): void {
    this.clear();
    this.startTimer();
    this.focus(0);
  }

  /** clears the digits only (e.g. after a wrong code) */
  clear(): void {
    this.entered.set([]);
    for (const input of this.inputs()) input.nativeElement.value = '';
  }

  focus(index: number): void {
    const el = this.inputs()[index]?.nativeElement;
    el?.focus();
    el?.select();
  }

  // ---------- template handlers ----------
  onInput(index: number, event: Event): void {
    const input = event.target as HTMLInputElement;
    const digit = input.value.replace(/\D/g, '').slice(-1);
    input.value = digit;
    this.setDigit(index, digit);

    if (digit && index < this.length() - 1) this.focus(index + 1);

    this.emitIfComplete();
  }

  onKeydown(index: number, event: KeyboardEvent): void {
    if (event.key === 'Backspace' && !this.digits()[index] && index > 0) {
      event.preventDefault();
      this.setDigit(index - 1, '');
      this.focus(index - 1);
    }
  }

  onPaste(event: ClipboardEvent): void {
    event.preventDefault();
    const pasted = (event.clipboardData?.getData('text') ?? '')
      .replace(/\D/g, '')
      .slice(0, this.length());
    if (!pasted) return;

    this.entered.set(this.indexes().map((i) => pasted[i] ?? ''));
    this.focus(Math.min(pasted.length, this.length() - 1));

    this.emitIfComplete();
  }

  onResend(): void {
    this.startTimer();
    this.resend.emit();
  }

  // ---------- private ----------
  private setDigit(index: number, digit: string): void {
    this.entered.set(this.digits().map((d, i) => (i === index ? digit : d)));
  }

  private emitIfComplete(): void {
    const code = this.code();
    if (code.length === this.length()) this.completed.emit(code);
  }

  private startTimer(): void {
    this.stopTimer();
    this.secondsLeft.set(this.seconds());

    this.timerId = setInterval(() => {
      this.secondsLeft.update((s) => Math.max(0, s - 1));
      if (this.secondsLeft() === 0) this.stopTimer();
    }, 1000);
  }

  private stopTimer(): void {
    if (this.timerId) {
      clearInterval(this.timerId);
      this.timerId = null;
    }
  }
}
