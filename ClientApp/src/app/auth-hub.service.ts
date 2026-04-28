import { HttpClient, HttpErrorResponse, HttpHeaders } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import {
  ApiErrorResponse,
  AuditLogListResponse,
  AuthResponse,
  CreatedExternalApp,
  ExternalApp,
  NotificationListResponse,
  RegeneratedApiKey,
  UserLink
} from './auth-hub.models';

declare global {
  interface Window {
    __AUTH_HUB_CONFIG__?: {
      apiBaseUrl?: string;
    };
  }
}

@Injectable({ providedIn: 'root' })
export class AuthHubService {
  private accessToken = '';
  private readonly apiBaseUrl = (window.__AUTH_HUB_CONFIG__?.apiBaseUrl ?? '').replace(/\/+$/, '');

  constructor(private readonly http: HttpClient) {}

  setAccessToken(token: string): void {
    this.accessToken = token;
  }

  clearAccessToken(): void {
    this.accessToken = '';
  }

  getBaseUrl(): string {
    return this.apiBaseUrl;
  }

  login(email: string, password: string): Promise<AuthResponse> {
    return this.request<AuthResponse>('POST', '/api/auth/login', { email, password }, false);
  }

  register(email: string, displayName: string, password: string, role: 'User' | 'Developer'): Promise<AuthResponse> {
    return this.request<AuthResponse>('POST', '/api/auth/register', { email, displayName, password, role }, false);
  }

  getSession(): Promise<AuthResponse> {
    return this.request<AuthResponse>('GET', '/api/auth/session', undefined, false);
  }

  logout(): Promise<void> {
    return this.request<void>('POST', '/api/auth/logout', undefined, false);
  }

  forgotPassword(email: string): Promise<void> {
    return this.request<void>('POST', '/api/auth/forgot-password', { email }, false);
  }

  getNotifications(): Promise<NotificationListResponse> {
    return this.request<NotificationListResponse>('GET', '/api/notifications');
  }

  markNotificationRead(id: string): Promise<void> {
    return this.request<void>('PATCH', `/api/notifications/${id}/read`);
  }

  getExternalApps(): Promise<ExternalApp[]> {
    return this.request<ExternalApp[]>('GET', '/api/external-apps');
  }

  createExternalApp(name: string, domain: string, redirectUri: string): Promise<CreatedExternalApp> {
    return this.request<CreatedExternalApp>('POST', '/api/external-apps', { name, domain, redirectUri });
  }

  regenerateApiKey(appId: string): Promise<RegeneratedApiKey> {
    return this.request<RegeneratedApiKey>('POST', `/api/external-apps/${appId}/api-key/regenerate`);
  }

  revokeApiKey(appId: string): Promise<void> {
    return this.request<void>('POST', `/api/external-apps/${appId}/api-key/revoke`);
  }

  getUserLinks(): Promise<UserLink[]> {
    return this.request<UserLink[]>('GET', '/api/user-links');
  }

  createUserLink(externalAppId: string, externalUserId: string, platformEmail: string | null): Promise<UserLink> {
    return this.request<UserLink>('POST', '/api/user-links', {
      externalAppId,
      externalUserId,
      platformEmail
    });
  }

  deleteUserLink(linkId: string): Promise<void> {
    return this.request<void>('DELETE', `/api/user-links/${linkId}`);
  }

  getAuditLogs(): Promise<AuditLogListResponse> {
    return this.request<AuditLogListResponse>('GET', '/api/audit-logs');
  }

  private async request<T>(method: string, url: string, body?: unknown, withAuth = true): Promise<T> {
    let headers = new HttpHeaders({ 'Content-Type': 'application/json' });
    if (withAuth && this.accessToken) {
      headers = headers.set('Authorization', `Bearer ${this.accessToken}`);
    }

    try {
      return await firstValueFrom(this.http.request<T>(method, this.resolveUrl(url), {
        body,
        headers,
        withCredentials: true
      }));
    } catch (error) {
      if (error instanceof HttpErrorResponse) {
        const response = error.error as ApiErrorResponse | null;
        throw new Error(this.extractErrorMessage(response));
      }

      throw new Error('تعذر تنفيذ الطلب.');
    }
  }

  private extractErrorMessage(response: ApiErrorResponse | null): string {
    const validationMessage = this.extractValidationMessage(response?.error?.details);
    if (validationMessage) {
      return validationMessage;
    }

    return response?.error?.message ?? response?.message ?? 'تعذر تنفيذ الطلب.';
  }

  private resolveUrl(url: string): string {
    if (/^https?:\/\//i.test(url)) {
      return url;
    }

    return `${this.apiBaseUrl}${url}`;
  }

  private extractValidationMessage(details?: Record<string, string[]>): string | null {
    if (!details) {
      return null;
    }

    for (const messages of Object.values(details)) {
      const firstMessage = messages.find(message => message.trim().length > 0);
      if (firstMessage) {
        return this.localizeError(firstMessage);
      }
    }

    return null;
  }

  private localizeError(message: string): string {
    if (/valid e-?mail address/i.test(message)) {
      return 'اكتب بريدًا إلكترونيًا صحيحًا.';
    }

    if (/email field is required/i.test(message)) {
      return 'أدخل البريد الإلكتروني.';
    }

    if (/password field is required/i.test(message)) {
      return 'أدخل كلمة المرور.';
    }

    return message;
  }
}
