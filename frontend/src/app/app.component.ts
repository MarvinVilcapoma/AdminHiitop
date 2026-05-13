import { Component, inject, OnInit } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { ToastHostComponent } from './core/components/toast-host/toast-host.component';
import { AuthService } from './core/services/auth.service';

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [RouterOutlet, ToastHostComponent],
  templateUrl: './app.component.html',
  styleUrl: './app.component.scss',
})
export class AppComponent implements OnInit {
  private readonly auth = inject(AuthService);

  ngOnInit(): void {
    if (!this.auth.isAuthenticated()) {
      return;
    }

    this.auth.refreshMe().subscribe({
      error: () => this.auth.logout(),
    });
  }
}
