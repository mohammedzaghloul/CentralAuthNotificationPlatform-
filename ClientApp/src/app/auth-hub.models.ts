export interface AuthResponse {
  userId: string;
  email: string;
  displayName: string;
  roles: string[];
  accessToken: string;
  expiresAt: string;
}

export interface NotificationListResponse {
  items: NotificationItem[];
  unreadCount: number;
}

export interface NotificationItem {
  id: string;
  type: string;
  title: string;
  message: string;
  sourceAppName?: string | null;
  isRead: boolean;
  createdAt: string;
  readAt?: string | null;
  actionUrl?: string | null;
  actionLabel?: string | null;
}

export interface ExternalApp {
  id: string;
  name: string;
  domain: string;
  clientId: string;
  redirectUri: string;
  apiKeyPreview: string;
  isApiKeyActive: boolean;
  isActive: boolean;
  createdAt: string;
  lastUsedAt?: string | null;
}

export interface CreatedExternalApp {
  id: string;
  name: string;
  domain: string;
  clientId: string;
  clientSecret: string;
  redirectUri: string;
  apiKey: string;
  apiKeyPreview: string;
  createdAt: string;
}

export interface RegeneratedApiKey {
  id: string;
  apiKey: string;
  apiKeyPreview: string;
  isApiKeyActive: boolean;
  regeneratedAt: string;
}

export interface UserLink {
  id: string;
  externalAppId: string;
  externalAppName: string;
  externalUserId: string;
  platformUserId: string;
  platformEmail: string;
  platformDisplayName: string;
  createdAt: string;
}

export interface AuditLogListResponse {
  items: AuditLogItem[];
}

export interface AuditLogItem {
  id: string;
  action: string;
  appName?: string | null;
  externalUserId?: string | null;
  platformEmail?: string | null;
  createdAt: string;
}

export interface ApiErrorResponse {
  error?: {
    code?: string;
    message?: string;
    details?: Record<string, string[]>;
  };
  message?: string;
}
