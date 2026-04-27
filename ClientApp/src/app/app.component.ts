import { CommonModule, DatePipe } from '@angular/common';
import { Component, OnInit } from '@angular/core';
import { FormsModule, NgForm } from '@angular/forms';
import { ConsentComponent } from './consent/consent.component';
import {
  AuditLogItem,
  AuthResponse,
  CreatedExternalApp,
  ExternalApp,
  NotificationItem,
  RegeneratedApiKey,
  UserLink
} from './auth-hub.models';
import { AuthHubService } from './auth-hub.service';

type AuthMode = 'login' | 'register';
type AlertKind = 'success' | 'danger' | 'info' | 'warning';
type DashboardMode = 'user' | 'developer';
type PlatformRole = 'User' | 'Developer';

@Component({
  selector: 'auth-root',
  standalone: true,
  imports: [CommonModule, FormsModule, DatePipe, ConsentComponent],
  templateUrl: './app.component.html',
  styleUrl: './app.component.css'
})
export class AppComponent implements OnInit {
  authMode: AuthMode = 'login';
  dashboardMode: DashboardMode = window.location.pathname.includes('/developer') ? 'developer' : 'user';
  session: AuthResponse | null = null;
  alert: { kind: AlertKind; message: string } | null = null;
  loading = false;
  refreshing = false;
  sidebarOpen = false;

  loginForm = {
    email: '',
    password: ''
  };

  registerForm: { displayName: string; email: string; password: string; role: PlatformRole } = {
    displayName: '',
    email: '',
    password: '',
    role: 'User'
  };

  appForm = {
    name: '',
    domain: '',
    redirectUri: ''
  };

  linkForm = {
    externalAppId: '',
    externalUserId: '',
    platformEmail: ''
  };

  notifications: NotificationItem[] = [];
  externalApps: ExternalApp[] = [];
  userLinks: UserLink[] = [];
  auditLogs: AuditLogItem[] = [];
  createdApp: CreatedExternalApp | null = null;
  regeneratedKey: RegeneratedApiKey | null = null;

  forgotPasswordForm = {
    email: ''
  };

  showForgotPassword = false;

  constructor(private readonly api: AuthHubService) {}

  ngOnInit(): void {
    void this.restoreSession();
  }

  get isConsentRoute(): boolean {
    return window.location.pathname.includes('/consent');
  }

  get isDeveloper(): boolean {
    return this.session?.roles?.includes('Developer') ?? false;
  }

  get unreadCount(): number {
    return this.notifications.filter(item => !item.isRead).length;
  }

  async login(form: NgForm): Promise<void> {
    if (!this.ensureValidForm(form)) {
      return;
    }

    await this.authenticate(() => this.api.login(this.loginForm.email, this.loginForm.password));
  }

  async register(form: NgForm): Promise<void> {
    if (!this.ensureValidForm(form)) {
      return;
    }

    await this.authenticate(() =>
      this.api.register(
        this.registerForm.email,
        this.registerForm.displayName,
        this.registerForm.password,
        this.registerForm.role));
  }

  async forgotPassword(form: NgForm): Promise<void> {
    if (!this.ensureValidForm(form)) {
      return;
    }

    this.loading = true;
    this.clearAlert();
    try {
      await this.api.forgotPassword(this.forgotPasswordForm.email);
      this.setAlert('success', 'تم إرسال رابط استعادة كلمة المرور إلى بريدك الإلكتروني.');
      this.forgotPasswordForm.email = '';
      this.showForgotPassword = false;
    } catch (error) {
      this.setAlert('danger', this.getErrorMessage(error));
    } finally {
      this.loading = false;
    }
  }

  async signOut(): Promise<void> {
    try {
      await this.api.logout();
    } catch {
      // Local state still needs to be cleared if the cookie was already expired.
    }

    this.api.clearAccessToken();
    this.session = null;
    this.dashboardMode = 'user';
    this.notifications = [];
    this.externalApps = [];
    this.userLinks = [];
    this.auditLogs = [];
    this.createdApp = null;
    this.regeneratedKey = null;
    this.setAlert('info', 'تم تسجيل الخروج.');
  }

  async refreshAll(): Promise<void> {
    if (!this.session) {
      return;
    }

    if (this.dashboardMode === 'developer' && !this.isDeveloper) {
      this.setDashboardMode('user');
    }

    this.refreshing = true;
    this.clearAlert();
    try {
      const inbox = await this.api.getNotifications();
      this.notifications = inbox.items;

      const links = await this.api.getUserLinks();
      this.userLinks = links;

      if (this.isDeveloper) {
        const [apps, logs] = await Promise.all([
          this.api.getExternalApps(),
          this.api.getAuditLogs()
        ]);

        this.externalApps = apps;
        this.auditLogs = logs.items;

        if (!this.linkForm.externalAppId && apps.length > 0) {
          this.linkForm.externalAppId = apps[0].id;
        }
      } else {
        this.externalApps = [];
        this.auditLogs = [];
      }
    } catch (error) {
      console.error('Dashboard load error:', error);
      this.setAlert('danger', this.getErrorMessage(error));
    } finally {
      this.refreshing = false;
    }
  }

  async createExternalApp(): Promise<void> {
    this.loading = true;
    this.clearAlert();
    this.regeneratedKey = null;
    try {
      this.createdApp = await this.api.createExternalApp(
        this.appForm.name,
        this.appForm.domain,
        this.appForm.redirectUri);
      this.appForm = { name: '', domain: '', redirectUri: '' };
      this.setAlert('success', 'تم إنشاء التطبيق. انسخ القيم السرية الآن لأنها تظهر مرة واحدة فقط.');
      await this.refreshAll();
    } catch (error) {
      this.setAlert('danger', this.getErrorMessage(error));
    } finally {
      this.loading = false;
    }
  }

  async regenerateApiKey(app: ExternalApp): Promise<void> {
    this.loading = true;
    this.clearAlert();
    this.createdApp = null;
    try {
      this.regeneratedKey = await this.api.regenerateApiKey(app.id);
      this.setAlert('warning', 'تم توليد مفتاح API جديد. انسخه الآن لأنه لن يظهر مرة أخرى.');
      await this.refreshAll();
    } catch (error) {
      this.setAlert('danger', this.getErrorMessage(error));
    } finally {
      this.loading = false;
    }
  }

  async revokeApiKey(app: ExternalApp): Promise<void> {
    this.loading = true;
    this.clearAlert();
    try {
      await this.api.revokeApiKey(app.id);
      this.regeneratedKey = null;
      this.setAlert('success', 'تم إلغاء مفتاح API لهذا التطبيق.');
      await this.refreshAll();
    } catch (error) {
      this.setAlert('danger', this.getErrorMessage(error));
    } finally {
      this.loading = false;
    }
  }

  async createUserLink(): Promise<void> {
    this.loading = true;
    this.clearAlert();
    try {
      await this.api.createUserLink(
        this.linkForm.externalAppId,
        this.linkForm.externalUserId,
        this.linkForm.platformEmail.trim() || null);
      this.linkForm.externalUserId = '';
      this.linkForm.platformEmail = '';
      this.setAlert('success', 'تم إنشاء ربط الحساب.');
      await this.refreshAll();
    } catch (error) {
      this.setAlert('danger', this.getErrorMessage(error));
    } finally {
      this.loading = false;
    }
  }

  async unlinkUserLink(link: UserLink): Promise<void> {
    const confirmed = window.confirm(`هل تريد إلغاء ربط حساب ${link.externalUserId} من ${link.externalAppName}؟`);
    if (!confirmed) {
      return;
    }

    this.loading = true;
    this.clearAlert();
    try {
      await this.api.deleteUserLink(link.id);
      this.setAlert('success', 'تم إلغاء ربط الحساب.');
      await this.refreshAll();
    } catch (error) {
      this.setAlert('danger', this.getErrorMessage(error));
    } finally {
      this.loading = false;
    }
  }

  async markRead(notification: NotificationItem): Promise<void> {
    if (notification.isRead) {
      return;
    }

    this.clearAlert();
    try {
      await this.api.markNotificationRead(notification.id);
      notification.isRead = true;
      notification.readAt = new Date().toISOString();
    } catch (error) {
      this.setAlert('danger', this.getErrorMessage(error));
    }
  }

  percentageRead(): number {
    if (this.notifications.length === 0) {
      return 0;
    }

    const readCount = this.notifications.length - this.unreadCount;
    return Math.round((readCount / this.notifications.length) * 100);
  }

  resetUrl(message: string): string | null {
    const match = message.match(/https?:\/\/[^\s]+/);
    return match?.[0] ?? null;
  }

  resetSummary(message: string): string {
    return message.replace(/https?:\/\/[^\s]+/, 'رابط إعادة التعيين متاح داخل هذا الإشعار.');
  }

  setDashboardMode(mode: DashboardMode): void {
    if (mode === 'developer' && !this.isDeveloper) {
      this.dashboardMode = 'user';
      window.history.pushState({}, '', '/dashboard/');
      this.setAlert('warning', 'لوحة المطور متاحة لحسابات المطورين فقط.');
      return;
    }

    this.dashboardMode = mode;
    const path = mode === 'developer' ? '/dashboard/developer' : '/dashboard/';
    window.history.pushState({}, '', path);
  }

  actionLabel(action: string): string {
    const labels: Record<string, string> = {
      ACCOUNT_LINKED: 'ربط حساب مدرسي',
      ACCOUNT_UNLINKED: 'إلغاء ربط حساب',
      FORGOT_PASSWORD_REQUESTED: 'طلب استعادة كلمة مرور',
      FORGOT_PASSWORD_RATE_LIMITED: 'محاولة استعادة مكررة (محظور)'
    };

    return labels[action] || action;
  }

  private async authenticate(action: () => Promise<AuthResponse>): Promise<void> {
    this.loading = true;
    this.clearAlert();
    try {
      const response = await action();
      this.session = {
        ...response,
        roles: response.roles ?? ['User']
      };
      this.dashboardMode = this.isDeveloper && window.location.pathname.includes('/developer') ? 'developer' : 'user';
      this.api.setAccessToken(response.accessToken);

      const returnUrl = this.getSafeReturnUrl();
      if (returnUrl) {
        window.location.assign(returnUrl);
        return;
      }

      this.setAlert('success', `مرحبًا ${response.displayName}.`);
      await this.refreshAll();
    } catch (error) {
      this.setAlert('danger', this.getErrorMessage(error));
    } finally {
      this.loading = false;
    }
  }

  private async restoreSession(): Promise<void> {
    if (this.session) {
      return;
    }

    try {
      const response = await this.api.getSession();
      this.session = {
        ...response,
        roles: response.roles ?? ['User']
      };
      this.dashboardMode = this.isDeveloper && window.location.pathname.includes('/developer') ? 'developer' : 'user';
      this.api.setAccessToken(response.accessToken);

      const returnUrl = this.getSafeReturnUrl();
      if (returnUrl) {
        window.location.assign(returnUrl);
        return;
      }

      await this.refreshAll();
    } catch (error) {
      console.error('Session restore error:', error);
      this.api.clearAccessToken();
      this.session = null;
      if (this.dashboardMode === 'developer') {
        this.dashboardMode = 'user';
      }
    }
  }

  private setAlert(kind: AlertKind, message: string): void {
    this.alert = { kind, message };
    if (kind === 'success') {
      setTimeout(() => {
        if (this.alert?.message === message) {
          this.clearAlert();
        }
      }, 5000);
    }
  }

  private clearAlert(): void {
    this.alert = null;
  }

  private ensureValidForm(form: NgForm): boolean {
    if (form.valid) {
      return true;
    }

    form.control.markAllAsTouched();
    this.setAlert('warning', 'تحقق من البريد الإلكتروني والحقول المطلوبة.');
    return false;
  }

  private getSafeReturnUrl(): string | null {
    const currentUrl = new URL(window.location.href);
    const candidate = currentUrl.searchParams.get('returnUrl');
    if (!candidate) {
      return null;
    }

    try {
      const resolvedUrl = new URL(candidate, window.location.origin);
      const isLocalHost = resolvedUrl.hostname === window.location.hostname;
      const isAuthorizeRoute = resolvedUrl.pathname.startsWith('/connect/');
      return isLocalHost && isAuthorizeRoute ? resolvedUrl.toString() : null;
    } catch {
      return null;
    }
  }

  private getErrorMessage(error: unknown): string {
    return error instanceof Error ? error.message : 'تعذر تنفيذ الطلب.';
  }
}
