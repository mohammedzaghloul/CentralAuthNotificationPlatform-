import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { HttpClient } from '@angular/common/http';

interface ConsentRequest {
  responseType: string;
  clientId: string;
  redirectUri: string;
  scope: string;
  state?: string;
  nonce?: string;
  codeChallenge?: string;
  codeChallengeMethod?: string;
}

@Component({
  selector: 'app-consent',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <main class="consent-shell" dir="rtl">
      <section class="consent-card">
        <div class="brand-header">
          <div class="brand-icon">IDP</div>
          <p class="request-title">طلب ربط حساب</p>
          <h1 class="app-name">{{ appName || 'التطبيق' }} يريد ربط حسابك</h1>
        </div>
        
        <p class="description">
          سيحصل التطبيق على هويتك داخل منصة الدخول الموحد حتى يتم ربط الحسابين بشكل آمن.
        </p>
        
        <div class="scopes-section" *ngIf="scopes.length > 0">
          <p class="scopes-title">الصلاحيات المطلوبة</p>
          <div class="scopes-list">
            <span class="scope-badge" *ngFor="let scope of scopes">{{ scope }}</span>
          </div>
        </div>
        
        <!-- Debug info -->
        <div class="debug-info" *ngIf="debugMessage">
          <p>{{ debugMessage }}</p>
        </div>
        
        <div class="actions">
          <button type="button" class="btn btn-primary" (click)="submitConsent(true)" [disabled]="loading || !request">
            {{ loading ? 'جاري المعالجة...' : 'السماح بالربط' }}
          </button>
          <button type="button" class="btn btn-secondary" (click)="submitConsent(false)" [disabled]="loading || !request">
            إلغاء
          </button>
        </div>
        
        <p class="footer-note">
          بوابة الدخول الموحد - منصة مركزية آمنة
        </p>
      </section>
    </main>
  `,
  styles: [`
    :host {
      display: block;
      min-height: 100vh;
    }
    
    .consent-shell {
      min-height: 100vh;
      display: flex;
      align-items: center;
      justify-content: center;
      padding: 2rem 1rem;
      background: var(--background);
    }
    
    .consent-card {
      max-width: 480px;
      width: 100%;
      background: var(--surface);
      border: 1px solid var(--border);
      border-radius: var(--radius-xl);
      box-shadow: var(--shadow-md);
      padding: 2rem;
    }
    
    .brand-header {
      text-align: center;
      margin-bottom: 2rem;
    }
    
    .brand-icon {
      width: 44px;
      height: 44px;
      background: var(--primary);
      color: white;
      border-radius: 10px;
      display: inline-flex;
      align-items: center;
      justify-content: center;
      font-weight: 800;
      font-size: 0.875rem;
      margin-bottom: 1rem;
    }
    
    .request-title {
      font-size: 0.75rem;
      color: var(--text-muted);
      font-weight: 500;
      margin-bottom: 0.5rem;
    }
    
    .app-name {
      font-size: 1.25rem;
      font-weight: 700;
      color: var(--text-main);
      margin-bottom: 0.5rem;
    }
    
    .description {
      font-size: 0.875rem;
      color: var(--text-secondary);
      margin-bottom: 1.5rem;
    }
    
    .scopes-section {
      background: var(--primary-light);
      border: 1px solid var(--border);
      border-radius: var(--radius-lg);
      padding: 1rem;
      margin-bottom: 1.5rem;
    }
    
    .scopes-title {
      font-size: 0.75rem;
      font-weight: 600;
      color: var(--text-secondary);
      text-transform: uppercase;
      letter-spacing: 0.02em;
      margin-bottom: 0.75rem;
    }
    
    .scopes-list {
      display: flex;
      flex-wrap: wrap;
      gap: 0.5rem;
    }
    
    .scope-badge {
      display: inline-flex;
      align-items: center;
      padding: 0.375rem 0.75rem;
      border-radius: var(--radius-md);
      background: var(--surface);
      border: 1px solid var(--border);
      font-size: 0.8125rem;
      font-weight: 500;
      color: var(--text-secondary);
    }
    
    .actions {
      display: flex;
      flex-direction: column;
      gap: 0.75rem;
    }
    
    .btn {
      font-family: inherit;
      font-weight: 500;
      font-size: 0.9rem;
      padding: 0.75rem 1.5rem;
      border-radius: var(--radius-md);
      border: none;
      cursor: pointer;
      transition: all 0.15s ease;
      display: inline-flex;
      align-items: center;
      justify-content: center;
    }
    
    .btn:disabled {
      opacity: 0.6;
      cursor: not-allowed;
    }
    
    .btn:active { transform: scale(0.98); }
    
    .btn-primary {
      background: var(--primary);
      color: white;
      box-shadow: var(--shadow-sm);
    }
    
    .btn-primary:hover:not(:disabled) {
      background: var(--primary-hover);
      box-shadow: var(--shadow-md);
    }
    
    .btn-secondary {
      background: var(--surface);
      color: var(--text-secondary);
      border: 1px solid var(--border);
    }
    
    .btn-secondary:hover:not(:disabled) {
      background: var(--background);
      border-color: var(--border-strong);
    }
    
    .footer-note {
      text-align: center;
      margin-top: 1.5rem;
      font-size: 0.75rem;
      color: var(--text-light);
    }
    
    .debug-info {
      background: #fef3c7;
      border: 1px solid #f59e0b;
      border-radius: var(--radius-md);
      padding: 0.75rem;
      margin-bottom: 1rem;
      font-size: 0.8rem;
      color: #92400e;
    }
    
    .btn:disabled {
      opacity: 0.5;
      cursor: not-allowed;
    }
    
    @media (max-width: 480px) {
      .consent-card { padding: 1.5rem; }
      .app-name { font-size: 1.1rem; }
    }
  `]
})
export class ConsentComponent implements OnInit {
  appName = '';
  scopes: string[] = [];
  loading = false;
  debugMessage = '';
  
  request: ConsentRequest | null = null;

  constructor(
    private route: ActivatedRoute,
    private http: HttpClient,
    private router: Router
  ) {}

  ngOnInit(): void {
    this.debugMessage = 'جاري تحميل البيانات...';
    
    // Get params from URL
    this.route.queryParams.subscribe({
      next: (params) => {
        console.log('Query params:', params);
        
        if (!params['client_id']) {
          this.debugMessage = 'خطأ: لا يوجد client_id في الرابط';
          return;
        }
        
        this.request = {
          responseType: params['response_type'] || '',
          clientId: params['client_id'] || '',
          redirectUri: params['redirect_uri'] || '',
          scope: params['scope'] || '',
          state: params['state'],
          nonce: params['nonce'],
          codeChallenge: params['code_challenge'],
          codeChallengeMethod: params['code_challenge_method']
        };
        
        // Parse scopes
        this.scopes = this.request.scope ? this.request.scope.split(' ').filter(s => s.trim()) : [];
        
        // Get app name from client_id
        this.appName = this.request.clientId;
        
        this.debugMessage = `جاهز: ${this.appName}`;
        console.log('Consent page loaded:', this.request);
      },
      error: (err) => {
        this.debugMessage = 'خطأ في قراءة البيانات: ' + err.message;
      }
    });
  }

  submitConsent(allow: boolean): void {
    console.log('submitConsent called:', allow);
    if (!this.request || this.loading) {
      console.log('Blocked: no request or already loading');
      return;
    }
    
    this.loading = true;
    
    const formData = new FormData();
    formData.append('consent', allow ? 'allow' : 'deny');
    formData.append('response_type', this.request.responseType);
    formData.append('client_id', this.request.clientId);
    formData.append('redirect_uri', this.request.redirectUri);
    formData.append('scope', this.request.scope);
    if (this.request.state) formData.append('state', this.request.state);
    if (this.request.nonce) formData.append('nonce', this.request.nonce);
    if (this.request.codeChallenge) formData.append('code_challenge', this.request.codeChallenge);
    if (this.request.codeChallengeMethod) formData.append('code_challenge_method', this.request.codeChallengeMethod);
    
    console.log('Submitting to /connect/authorize');
    
    // Submit to backend
    fetch('/connect/authorize', {
      method: 'POST',
      body: formData,
      redirect: 'follow',
      credentials: 'include'
    }).then(response => {
      console.log('Response:', response.status, response.redirected);
      if (response.redirected) {
        window.location.href = response.url;
      } else if (response.ok) {
        response.text().then(html => {
          // If we get HTML back, the server returned the consent page again
          // This means we need to handle the redirect manually
          console.log('Got HTML response, checking for redirect...');
          document.open();
          document.write(html);
          document.close();
        });
      } else {
        console.error('Error response:', response.status);
        this.loading = false;
      }
    }).catch(err => {
      console.error('Fetch error:', err);
      this.loading = false;
    });
  }
}
