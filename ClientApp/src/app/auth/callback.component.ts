import { CommonModule } from '@angular/common';
import { Component, OnInit } from '@angular/core';
import { OAuthAuthService } from './oauth-auth.service';

@Component({
  selector: 'auth-callback',
  standalone: true,
  imports: [CommonModule],
  template: `
    <main class="callback-page">
      <section class="callback-panel">
        <h1>{{ error ? 'Sign-in failed' : 'Signing you in' }}</h1>
        <p>{{ error || 'Completing the secure authorization code exchange.' }}</p>
      </section>
    </main>
  `,
  styles: [`
    .callback-page {
      min-height: 100vh;
      display: grid;
      place-items: center;
      padding: 24px;
      background: #f4f7fb;
      color: #111827;
      font-family: Arial, Helvetica, sans-serif;
    }
    .callback-panel {
      width: min(100%, 420px);
      padding: 28px;
      border: 1px solid #d9e1ec;
      border-radius: 8px;
      background: #fff;
      text-align: center;
    }
    h1 { margin: 0 0 8px; font-size: 1.35rem; }
    p { margin: 0; color: #5b6472; }
  `]
})
export class CallbackComponent implements OnInit {
  error = '';

  constructor(private readonly auth: OAuthAuthService) {}

  async ngOnInit(): Promise<void> {
    try {
      await this.auth.handleCallback();
    } catch (error) {
      this.error = error instanceof Error ? error.message : 'Invalid OAuth callback.';
    }
  }
}
