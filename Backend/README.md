# Central Auth Notification Platform

Production-oriented ASP.NET Core Web API that acts as an Identity Provider, OAuth2/OpenID Connect server, internal notification hub, and password-reset delivery service.

## Features

- ASP.NET Identity for user registration, login, password hashing, lockout, and email-as-identifier.
- JWT access tokens for API authentication.
- OAuth2 Authorization Code Flow with OpenID Connect-style `id_token`.
- External client apps with `ClientId`, `ClientSecret`, `RedirectUri`, and API key.
- Forgot-password requests delivered through both email and internal inbox.
- EF Core SQL Server persistence.
- Rate limiting on auth and external app endpoints.
- Dashboard UI at `/dashboard.html`.

## Run Locally

```powershell
dotnet restore
dotnet ef database update
dotnet run --launch-profile http
```

Open:

```text
http://localhost:5045/dashboard.html
```

The default connection string targets SQL Server on `localhost`.

For production, override these with environment variables or a secret store:

```text
Jwt__SigningKey
ConnectionStrings__DefaultConnection
Smtp__Host
Smtp__UserName
Smtp__Password
Smtp__FromEmail
```

## API Contract

### Auth

```http
POST /api/auth/register
POST /api/auth/login
POST /api/auth/forgot-password
POST /api/auth/forgot-password/validate
POST /api/auth/forgot-password/reset
```

`POST /api/auth/forgot-password` requires `X-API-Key` from an external app. It always stores an internal notification and also sends email when SMTP is configured.

### OAuth2 / OIDC

```http
GET  /connect/authorize
POST /connect/token
GET  /connect/userinfo
```

Authorization Code Flow:

```text
GET /connect/authorize?response_type=code&client_id=...&redirect_uri=...&scope=openid profile email&state=...
```

Token exchange:

```http
POST /connect/token
Content-Type: application/x-www-form-urlencoded

grant_type=authorization_code&
code=...&
redirect_uri=...&
client_id=...&
client_secret=...
```

The token endpoint returns `access_token`, `id_token`, `token_type`, `expires_in`, and `scope`.

### External Apps

Protected by user JWT:

```http
GET  /api/external-apps
POST /api/external-apps
```

`POST /api/external-apps` returns `clientSecret` and raw `apiKey` once. Only hashes are stored.

### Notifications

Protected by user JWT:

```http
GET  /api/notifications
POST /api/notifications/read
PATCH /api/notifications/{id}/read
```

## Security Notes

- Passwords are hashed by ASP.NET Identity.
- Client secrets and API keys are generated cryptographically and stored as SHA-256 hashes.
- Authorization codes expire in 5 minutes.
- Password reset tokens expire in 10 minutes and are stored salted+hashed.
- Forgot-password responses do not reveal whether the user email exists.
- SMTP is abstracted behind `IAppEmailSender`; when SMTP is not configured, email sending is skipped but inbox delivery still happens.
- The development JWT signing key in `appsettings.json` must be replaced before deployment.
