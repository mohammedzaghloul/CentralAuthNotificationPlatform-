using System.Security.Cryptography;
using System.Text;
using System.Threading.RateLimiting;
using CentralAuthNotificationPlatform.Data;
using CentralAuthNotificationPlatform.Middleware;
using CentralAuthNotificationPlatform.Models;
using CentralAuthNotificationPlatform.Options;
using CentralAuthNotificationPlatform.Repositories;
using CentralAuthNotificationPlatform.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.AddConsole();

var jwtOptions = ResolveJwtOptions(builder.Configuration);
var smtpOptions = builder.Configuration.GetSection("Smtp").Get<SmtpOptions>() ?? new SmtpOptions();
var connectionString = ResolveConnectionString(builder.Configuration);
var allowedCorsOrigins = ResolveAllowedCorsOrigins(builder.Configuration, builder.Environment);

builder.Services.Configure<JwtOptions>(options =>
{
    options.Issuer = jwtOptions.Issuer;
    options.Audience = jwtOptions.Audience;
    options.SigningKey = jwtOptions.SigningKey;
    options.ExpirationMinutes = jwtOptions.ExpirationMinutes;
    options.OAuthAccessTokenMinutes = jwtOptions.OAuthAccessTokenMinutes;
    options.AuthorizationCodeMinutes = jwtOptions.AuthorizationCodeMinutes;
    options.PasswordResetMinutes = jwtOptions.PasswordResetMinutes;
});
builder.Services.Configure<SmtpOptions>(builder.Configuration.GetSection("Smtp"));

ValidateStartupConfiguration(builder.Environment, jwtOptions, smtpOptions, connectionString);

builder.Services.AddDbContext<AuthHubDbContext>(options =>
    options.UseSqlServer(connectionString!));

builder.Services.AddIdentity<ApplicationUser, IdentityRole<Guid>>(options =>
    {
        options.User.RequireUniqueEmail = true;
        options.SignIn.RequireConfirmedEmail = false;
        options.Lockout.AllowedForNewUsers = true;
        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(10);
        options.Password.RequiredLength = 8;
        options.Password.RequireDigit = true;
        options.Password.RequireLowercase = true;
        options.Password.RequireUppercase = false;
        options.Password.RequireNonAlphanumeric = false;
    })
    .AddEntityFrameworkStores<AuthHubDbContext>()
    .AddDefaultTokenProviders();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = builder.Environment.IsDevelopment()
        ? SameSiteMode.Lax
        : SameSiteMode.None;
    options.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
        ? CookieSecurePolicy.SameAsRequest
        : CookieSecurePolicy.Always;
    options.LoginPath = "/dashboard.html";
    options.AccessDeniedPath = "/dashboard.html";
});

builder.Services.AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateIssuerSigningKey = true,
            ValidateLifetime = true,
            ValidIssuer = jwtOptions.Issuer,
            ValidAudience = jwtOptions.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.SigningKey)),
            ClockSkew = TimeSpan.FromSeconds(30)
        };
});

builder.Services.AddAuthorization();
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
});
builder.Services.AddScoped<IExternalAppRepository, ExternalAppRepository>();
builder.Services.AddScoped<INotificationRepository, NotificationRepository>();
builder.Services.AddScoped<IOAuthRepository, OAuthRepository>();
builder.Services.AddScoped<IJwtTokenService, JwtTokenService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IExternalAppService, ExternalAppService>();
builder.Services.AddScoped<IUserLinkService, UserLinkService>();
builder.Services.AddScoped<INotificationService, NotificationService>();
builder.Services.AddScoped<IOAuthService, OAuthService>();
builder.Services.AddScoped<IAuditLogService, AuditLogService>();
builder.Services.AddScoped<IAppEmailSender, SmtpEmailSender>();

builder.Services.AddControllers()
    .ConfigureApiBehaviorOptions(options =>
    {
        options.InvalidModelStateResponseFactory = context =>
        {
            var details = context.ModelState
                .Where(item => item.Value?.Errors.Count > 0)
                .ToDictionary(
                    item => item.Key,
                    item => item.Value!.Errors.Select(error => error.ErrorMessage).ToArray());

            return new BadRequestObjectResult(new
            {
                error = new
                {
                    code = "VALIDATION_ERROR",
                    message = "Request validation failed.",
                    details
                }
            });
        };
    });
builder.Services.AddOpenApi();
builder.Services.AddCors(options =>
{
    options.AddPolicy("frontend", policy =>
    {
        if (allowedCorsOrigins.Length == 0)
        {
            policy.SetIsOriginAllowed(_ => false);
            return;
        }

        policy
            .WithOrigins(allowedCorsOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    options.AddPolicy("auth", context =>
        RateLimitPartition.GetFixedWindowLimiter(GetRateLimitPartitionKey(context, includeApiKey: false), _ =>
            new FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(15),
                QueueLimit = 0,
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst
            }));

    options.AddPolicy("external-app", context =>
        RateLimitPartition.GetFixedWindowLimiter(GetRateLimitPartitionKey(context, includeApiKey: true), _ =>
            new FixedWindowRateLimiterOptions
            {
                PermitLimit = 30,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst
            }));

    options.AddPolicy("password-reset", context =>
        RateLimitPartition.GetFixedWindowLimiter(GetRateLimitPartitionKey(context, includeApiKey: true), _ =>
            new FixedWindowRateLimiterOptions
            {
                PermitLimit = 1,
                Window = TimeSpan.FromSeconds(60),
                QueueLimit = 0,
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst
            }));
});

var app = builder.Build();

app.UseForwardedHeaders();
await ApplyDatabaseMigrationsAsync(app);
await SeedIdentityRolesAsync(app);

app.Use(async (context, next) =>
{
    context.Response.Headers.TryAdd("X-Content-Type-Options", "nosniff");
    context.Response.Headers.TryAdd("X-Frame-Options", "DENY");
    context.Response.Headers.TryAdd("Referrer-Policy", "no-referrer");
    context.Response.Headers.TryAdd("Permissions-Policy", "camera=(), microphone=(), geolocation=()");
    context.Response.Headers.TryAdd(
        "Content-Security-Policy",
        "default-src 'self'; " +
        "script-src 'self' 'unsafe-inline'; " +
        "style-src 'self' 'unsafe-inline' https://fonts.googleapis.com; " +
        "font-src 'self' https://fonts.gstatic.com data:; " +
        "connect-src 'self'; " +
        "object-src 'none'; " +
        "frame-ancestors 'none'; " +
        "base-uri 'self'");
    await next();
});

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}
else
{
    app.UseHsts();
    app.UseHttpsRedirection();
}

app.UseStaticFiles();
app.UseCors("frontend");
app.UseRateLimiter();
app.UseMiddleware<ApiKeyAuthenticationMiddleware>();
app.UseAuthentication();
app.UseAuthorization();

if (app.Environment.IsDevelopment())
{
    app.MapGet("/", () => Results.Redirect("http://localhost:4300/dashboard/")).AllowAnonymous();
    app.MapGet("/dashboard", (HttpContext context) => Results.Redirect(BuildDashboardRedirectUrl(context, null))).AllowAnonymous();
    app.MapGet("/dashboard.html", (HttpContext context) => Results.Redirect(BuildDashboardRedirectUrl(context, null))).AllowAnonymous();
    app.MapGet("/dashboard/{*path}", (HttpContext context, string? path) =>
    {
        return Results.Redirect(BuildDashboardRedirectUrl(context, path));
    }).AllowAnonymous();
}
else
{
    app.MapGet("/", () => Results.Ok(new
    {
        name = "Central Auth Notification Platform",
        status = "API only",
        dashboard = "Serve ClientApp separately."
    })).AllowAnonymous();
}

app.MapGet("/health", async (AuthHubDbContext dbContext, CancellationToken cancellationToken) =>
{
    var canConnect = await dbContext.Database.CanConnectAsync(cancellationToken);
    return canConnect
        ? Results.Ok(new { status = "Healthy" })
        : Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
}).AllowAnonymous();
app.MapControllers();

var port = Environment.GetEnvironmentVariable("PORT");
if (!string.IsNullOrWhiteSpace(port))
{
    app.Run($"http://0.0.0.0:{port}");
}
else
{
    app.Run();
}

static JwtOptions ResolveJwtOptions(IConfiguration configuration)
{
    var options = configuration.GetSection("Jwt").Get<JwtOptions>() ?? new JwtOptions();
    var jwtSecret = configuration["JWT_SECRET"];
    if (!string.IsNullOrWhiteSpace(jwtSecret))
    {
        options.SigningKey = jwtSecret.Trim();
    }

    var jwtIssuer = configuration["JWT_ISSUER"];
    if (string.IsNullOrWhiteSpace(jwtIssuer))
    {
        var railwayPublicDomain = configuration["RAILWAY_PUBLIC_DOMAIN"];
        if (!string.IsNullOrWhiteSpace(railwayPublicDomain))
        {
            jwtIssuer = railwayPublicDomain.StartsWith("http", StringComparison.OrdinalIgnoreCase)
                ? railwayPublicDomain
                : $"https://{railwayPublicDomain}";
        }
    }

    if (!string.IsNullOrWhiteSpace(jwtIssuer))
    {
        options.Issuer = jwtIssuer.Trim();
    }

    return options;
}

static string? ResolveConnectionString(IConfiguration configuration)
{
    var connectionString = configuration["CONNECTION_STRING"];
    if (string.IsNullOrWhiteSpace(connectionString))
    {
        connectionString = configuration.GetConnectionString("DefaultConnection");
    }

    var dbPassword = configuration["DB_PASSWORD"];
    if (string.IsNullOrWhiteSpace(connectionString) && !string.IsNullOrWhiteSpace(dbPassword))
    {
        connectionString = "Server=db49846.databaseasp.net;Database=db49846;User Id=db49846;Password=${DB_PASSWORD};Encrypt=False;MultipleActiveResultSets=True;";
    }

    if (string.IsNullOrWhiteSpace(connectionString) || string.IsNullOrWhiteSpace(dbPassword))
    {
        return connectionString;
    }

    return connectionString
        .Replace("${DB_PASSWORD}", dbPassword, StringComparison.Ordinal)
        .Replace("${{DB_PASSWORD}}", dbPassword, StringComparison.Ordinal)
        .Replace("%DB_PASSWORD%", dbPassword, StringComparison.Ordinal);
}

static string[] ResolveAllowedCorsOrigins(IConfiguration configuration, IHostEnvironment environment)
{
    var configuredOrigins = configuration["ALLOWED_ORIGINS"]
        ?? configuration["FRONTEND_ORIGIN"]
        ?? configuration["FRONTEND_URL"];

    var origins = string.IsNullOrWhiteSpace(configuredOrigins)
        ? []
        : configuredOrigins
            .Split([',', ';'], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Where(origin => Uri.TryCreate(origin, UriKind.Absolute, out _))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

    if (origins.Length > 0 || !environment.IsDevelopment())
    {
        return origins;
    }

    return
    [
        "http://localhost:4200",
        "http://localhost:4300",
        "http://127.0.0.1:4200",
        "http://127.0.0.1:4300"
    ];
}

static async Task ApplyDatabaseMigrationsAsync(WebApplication app)
{
    if (!app.Configuration.GetValue("APPLY_MIGRATIONS", false))
    {
        return;
    }

    using var scope = app.Services.CreateScope();
    var logger = scope.ServiceProvider
        .GetRequiredService<ILoggerFactory>()
        .CreateLogger("DatabaseMigrations");
    var dbContext = scope.ServiceProvider.GetRequiredService<AuthHubDbContext>();

    logger.LogInformation("Applying EF Core migrations.");
    await dbContext.Database.MigrateAsync();
    logger.LogInformation("EF Core migrations applied.");
}

static void ValidateStartupConfiguration(
    IHostEnvironment environment,
    JwtOptions jwtOptions,
    SmtpOptions smtpOptions,
    string? connectionString)
{
    if (string.IsNullOrWhiteSpace(connectionString))
    {
        throw new InvalidOperationException("ConnectionStrings:DefaultConnection must be configured.");
    }

    if (string.IsNullOrWhiteSpace(jwtOptions.SigningKey) || jwtOptions.SigningKey.Length < 32)
    {
        throw new InvalidOperationException("Jwt:SigningKey must be at least 32 characters.");
    }

    if (!environment.IsProduction())
    {
        return;
    }

    const string developmentSigningKey = "dev-only-change-this-signing-key-32-chars-minimum";
    if (string.Equals(jwtOptions.SigningKey, developmentSigningKey, StringComparison.Ordinal))
    {
        throw new InvalidOperationException("Jwt:SigningKey must be replaced before running in Production.");
    }

    if (string.IsNullOrWhiteSpace(jwtOptions.Issuer))
    {
        throw new InvalidOperationException("Jwt:Issuer or JWT_ISSUER must be configured.");
    }

    if (!string.IsNullOrWhiteSpace(smtpOptions.Host) && string.IsNullOrWhiteSpace(smtpOptions.FromEmail))
    {
        throw new InvalidOperationException("Smtp:FromEmail must be configured when SMTP is enabled.");
    }
}

static string GetRateLimitPartitionKey(HttpContext context, bool includeApiKey)
{
    var remoteIp = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    if (!includeApiKey)
    {
        return $"ip:{remoteIp}";
    }

    var apiKey = context.Request.Headers["X-API-Key"].FirstOrDefault();
    return string.IsNullOrWhiteSpace(apiKey)
        ? $"ip:{remoteIp}"
        : $"api:{HashPartition(apiKey)}:{remoteIp}";
}

static string HashPartition(string value)
{
    var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
    return Convert.ToHexString(bytes)[..16];
}

static string BuildDashboardRedirectUrl(HttpContext context, string? path)
{
    var suffix = string.IsNullOrWhiteSpace(path) ? string.Empty : path;
    var builder = new UriBuilder($"http://localhost:4300/dashboard/{suffix}")
    {
        Query = context.Request.QueryString.HasValue
            ? context.Request.QueryString.Value![1..]
            : string.Empty
    };

    return builder.Uri.ToString();
}

static async Task SeedIdentityRolesAsync(WebApplication app)
{
    using var scope = app.Services.CreateScope();
    var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
    var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
    var dbContext = scope.ServiceProvider.GetRequiredService<AuthHubDbContext>();

    foreach (var role in PlatformRoles.AllowedRoles)
    {
        if (!await roleManager.RoleExistsAsync(role))
        {
            await roleManager.CreateAsync(new IdentityRole<Guid>(role));
        }
    }

    var users = await dbContext.Users
        .AsNoTracking()
        .Select(user => new
        {
            user.Id,
            user.CreatedAt,
            HasOwnedApps = dbContext.ExternalApps.Any(appRecord => appRecord.OwnerUserId == user.Id)
        })
        .ToListAsync();

    var hasAdmin = (await userManager.GetUsersInRoleAsync(PlatformRoles.Admin)).Count > 0;
    var adminSeedCandidate = users
        .Where(user => user.HasOwnedApps)
        .OrderBy(user => user.CreatedAt)
        .FirstOrDefault()
        ?? users.OrderBy(user => user.CreatedAt).FirstOrDefault();

    foreach (var userInfo in users)
    {
        var user = await userManager.FindByIdAsync(userInfo.Id.ToString());
        if (user is null)
        {
            continue;
        }

        var roles = await userManager.GetRolesAsync(user);
        if (!hasAdmin && adminSeedCandidate?.Id == userInfo.Id)
        {
            if (!roles.Contains(PlatformRoles.Admin, StringComparer.OrdinalIgnoreCase))
            {
                await userManager.AddToRoleAsync(user, PlatformRoles.Admin);
            }

            hasAdmin = true;
            continue;
        }

        if (roles.Count > 0)
        {
            continue;
        }

        await userManager.AddToRoleAsync(user, PlatformRoles.User);
    }
}
