import { Component, signal } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { ThemeToggleComponent } from './shared/components/theme-toggle.component';
import { LoginSignupComponent } from './features/login-signup.component/login-signup.component';

@Component({
  imports: [RouterOutlet, ThemeToggleComponent, LoginSignupComponent],
  selector: 'app-root',
  styleUrl: './app.css',
  templateUrl: './app.html',
})
export class App {
  protected readonly title = signal('aabu-reports');
}
