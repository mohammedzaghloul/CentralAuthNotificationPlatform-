import { HttpClient, HttpHeaders, HttpParams } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Router } from '@angular/router';
import { firstValueFrom } from 'rxjs';

type TokenResponse = {
  access_token: string;
  id_token?: string;
  token_type: string;
  expires_in: number;
  scope?: string;
};

@Injectable({ providedIn: 'root' })
export class OAuthAuthService {
  private readonly tokenKey = 'school_spa_access_token';
  private readonly verifierKey = 'school_spa_pkce_verifier';
  private readonly stateKey = 'school_spa_oauth_state';
  private readonly apiBaseUrl = (window.__AUTH_HUB_CONFIG__?.apiBaseUrl ?? '').replace(/\/+$/, '');
  private readonly clientId = window.__AUTH_HUB_CONFIG__?.oauthClientId ?? 'school_app';
  private readonly scope = window.__AUTH_HUB_CONFIG__?.oauthScope ?? 'openid profile email';

  constructor(
    private readonly http: HttpClient,
    private readonly router: Router
  ) {}

  get accessToken(): string {
    return sessionStorage.getItem(this.tokenKey) ?? '';
  }

  async redirectToLogin(): Promise<void> {
    const verifier = this.randomString(64);
    const state = this.randomString(32);
    const challenge = await this.createChallenge(verifier);

    sessionStorage.setItem(this.verifierKey, verifier);
    sessionStorage.setItem(this.stateKey, state);

    const params = new URLSearchParams({
      client_id: this.clientId,
      redirect_uri: `${window.location.origin}/auth/callback`,
      response_type: 'code',
      scope: this.scope,
      state,
      code_challenge: challenge,
      code_challenge_method: 'S256'
    });

    window.location.assign(`${this.apiBaseUrl}/connect/authorize?${params.toString()}`);
  }

  async handleCallback(): Promise<void> {
    const url = new URL(window.location.href);
    const error = url.searchParams.get('error');
    if (error) {
      throw new Error(error);
    }

    const code = url.searchParams.get('code');
    const state = url.searchParams.get('state');
    const expectedState = sessionStorage.getItem(this.stateKey);
    const verifier = sessionStorage.getItem(this.verifierKey);
    if (!code || !state || state !== expectedState || !verifier) {
      throw new Error('Invalid OAuth callback.');
    }

    const body = new HttpParams()
      .set('grant_type', 'authorization_code')
      .set('client_id', this.clientId)
      .set('code', code)
      .set('redirect_uri', `${window.location.origin}/auth/callback`)
      .set('code_verifier', verifier);

    const response = await firstValueFrom(this.http.post<TokenResponse>(
      `${this.apiBaseUrl}/connect/token`,
      body.toString(),
      {
        headers: new HttpHeaders({ 'Content-Type': 'application/x-www-form-urlencoded' }),
        withCredentials: true
      }));

    sessionStorage.setItem(this.tokenKey, response.access_token);
    sessionStorage.removeItem(this.verifierKey);
    sessionStorage.removeItem(this.stateKey);
    await this.router.navigateByUrl('/dashboard');
  }

  clear(): void {
    sessionStorage.removeItem(this.tokenKey);
  }

  private randomString(length: number): string {
    const bytes = new Uint8Array(length);
    crypto.getRandomValues(bytes);
    return this.base64Url(bytes).slice(0, length);
  }

  private async createChallenge(verifier: string): Promise<string> {
    const bytes = new TextEncoder().encode(verifier);
    const digest = await crypto.subtle.digest('SHA-256', bytes);
    return this.base64Url(new Uint8Array(digest));
  }

  private base64Url(bytes: Uint8Array): string {
    return btoa(String.fromCharCode(...bytes))
      .replace(/\+/g, '-')
      .replace(/\//g, '_')
      .replace(/=+$/g, '');
  }
}
