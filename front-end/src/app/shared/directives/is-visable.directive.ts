import { Directive, ElementRef, inject, OnDestroy, OnInit, output } from '@angular/core';

/** Emits `visible` when the host element enters the screen (or its scroll container). */
@Directive({
  selector: '[appIsVisable]',
})
export class IsVisableDirective implements OnInit, OnDestroy {
  // DI
  private readonly host = inject(ElementRef);

  // private
  private observer!: IntersectionObserver;

  // output
  visible = output<void>();

  ngOnInit() {
    this.observer = new IntersectionObserver(([entry]) => {
      if (entry.isIntersecting) this.visible.emit();
    });

    this.observer.observe(this.host.nativeElement);
  }

  ngOnDestroy(): void {
    this.observer.disconnect();
  }
}
