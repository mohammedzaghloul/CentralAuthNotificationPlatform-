using CentralAuthNotificationPlatform.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace CentralAuthNotificationPlatform.Data;

public sealed class AuthHubDbContext(DbContextOptions<AuthHubDbContext> options)
    : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>(options)
{
    public DbSet<ExternalApp> ExternalApps => Set<ExternalApp>();
    public DbSet<UserApp> UserApps => Set<UserApp>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<PasswordResetToken> PasswordResetTokens => Set<PasswordResetToken>();
    public DbSet<OAuthAuthorizationCode> OAuthAuthorizationCodes => Set<OAuthAuthorizationCode>();
    public DbSet<UserLink> UserLinks => Set<UserLink>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        ConfigureIdentityTables(modelBuilder);

        modelBuilder.Entity<ExternalApp>(entity =>
        {
            entity.ToTable("ExternalApps");
            entity.HasKey(app => app.Id);
            entity.Property(app => app.Name).HasMaxLength(120).IsRequired();
            entity.Property(app => app.Domain).HasMaxLength(253).IsRequired();
            entity.Property(app => app.ClientId).HasMaxLength(80).IsRequired();
            entity.Property(app => app.ClientSecretHash).HasMaxLength(128).IsRequired();
            entity.Property(app => app.RedirectUri).HasMaxLength(500).IsRequired();
            entity.Property(app => app.ApiKeyHash).HasMaxLength(128).IsRequired();
            entity.Property(app => app.ApiKeyPreview).HasMaxLength(32).IsRequired();
            entity.HasIndex(app => app.ClientId).IsUnique();
            entity.HasIndex(app => app.ApiKeyHash).IsUnique();
            entity.HasIndex(app => new { app.OwnerUserId, app.Name }).IsUnique();
            entity.HasOne(app => app.OwnerUser)
                .WithMany(user => user.OwnedApps)
                .HasForeignKey(app => app.OwnerUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<UserApp>(entity =>
        {
            entity.ToTable("UserApps");
            entity.HasKey(userApp => new { userApp.UserId, userApp.ExternalAppId });
            entity.HasOne(userApp => userApp.User)
                .WithMany(user => user.UserApps)
                .HasForeignKey(userApp => userApp.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(userApp => userApp.ExternalApp)
                .WithMany(app => app.UserApps)
                .HasForeignKey(userApp => userApp.ExternalAppId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<UserLink>(entity =>
        {
            entity.ToTable("UserLinks");
            entity.HasKey(link => link.Id);
            entity.Property(link => link.ExternalUserId).HasMaxLength(256).IsRequired();
            entity.Property(link => link.NormalizedExternalUserId).HasMaxLength(256).IsRequired();
            entity.HasIndex(link => new { link.ExternalAppId, link.NormalizedExternalUserId }).IsUnique();
            entity.HasIndex(link => link.PlatformUserId);
            entity.HasOne(link => link.ExternalApp)
                .WithMany(app => app.UserLinks)
                .HasForeignKey(link => link.ExternalAppId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(link => link.PlatformUser)
                .WithMany(user => user.UserLinks)
                .HasForeignKey(link => link.PlatformUserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Notification>(entity =>
        {
            entity.ToTable("Notifications");
            entity.HasKey(notification => notification.Id);
            entity.Property(notification => notification.Type).HasMaxLength(50).IsRequired();
            entity.Property(notification => notification.Title).HasMaxLength(160).IsRequired();
            entity.Property(notification => notification.Message).HasMaxLength(1000).IsRequired();
            entity.Property(notification => notification.SourceAppName).HasMaxLength(120);
            entity.Property(notification => notification.MetadataJson).HasMaxLength(2000);
            entity.HasIndex(notification => new { notification.UserId, notification.CreatedAt });
            entity.HasOne(notification => notification.User)
                .WithMany(user => user.Notifications)
                .HasForeignKey(notification => notification.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<PasswordResetToken>(entity =>
        {
            entity.ToTable("PasswordResetTokens");
            entity.HasKey(token => token.Id);
            entity.Property(token => token.ExternalUserId).HasMaxLength(256);
            entity.Property(token => token.NormalizedExternalUserId).HasMaxLength(256);
            entity.Property(token => token.TokenHash).HasMaxLength(128).IsRequired();
            entity.Property(token => token.Salt).HasMaxLength(64).IsRequired();
            entity.HasIndex(token => new { token.UserId, token.ExpiresAt });
            entity.HasIndex(token => new { token.ExternalAppId, token.NormalizedExternalUserId, token.ExpiresAt });
            entity.HasOne(token => token.User)
                .WithMany(user => user.PasswordResetTokens)
                .HasForeignKey(token => token.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(token => token.ExternalApp)
                .WithMany()
                .HasForeignKey(token => token.ExternalAppId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<AuditLog>(entity =>
        {
            entity.ToTable("AuditLogs");
            entity.HasKey(log => log.Id);
            entity.Property(log => log.Action).HasMaxLength(80).IsRequired();
            entity.Property(log => log.AppName).HasMaxLength(120);
            entity.Property(log => log.ExternalUserId).HasMaxLength(256);
            entity.Property(log => log.PlatformEmail).HasMaxLength(256);
            entity.Property(log => log.MetadataJson).HasMaxLength(2000);
            entity.HasIndex(log => new { log.UserId, log.CreatedAt });
            entity.HasIndex(log => new { log.ExternalAppId, log.CreatedAt });
            entity.HasOne(log => log.User)
                .WithMany()
                .HasForeignKey(log => log.UserId)
                .OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(log => log.ExternalApp)
                .WithMany()
                .HasForeignKey(log => log.ExternalAppId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<OAuthAuthorizationCode>(entity =>
        {
            entity.ToTable("OAuthAuthorizationCodes");
            entity.HasKey(code => code.Id);
            entity.Property(code => code.CodeHash).HasMaxLength(128).IsRequired();
            entity.Property(code => code.RedirectUri).HasMaxLength(500).IsRequired();
            entity.Property(code => code.Scope).HasMaxLength(500).IsRequired();
            entity.Property(code => code.Nonce).HasMaxLength(160);
            entity.Property(code => code.CodeChallenge).HasMaxLength(160);
            entity.Property(code => code.CodeChallengeMethod).HasMaxLength(20);
            entity.HasIndex(code => code.CodeHash).IsUnique();
            entity.HasIndex(code => new { code.UserId, code.ExternalAppId, code.ExpiresAt });
            entity.HasOne(code => code.User)
                .WithMany()
                .HasForeignKey(code => code.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(code => code.ExternalApp)
                .WithMany(app => app.AuthorizationCodes)
                .HasForeignKey(code => code.ExternalAppId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }

    private static void ConfigureIdentityTables(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ApplicationUser>(entity =>
        {
            entity.ToTable("Users");
            entity.Property(user => user.DisplayName).HasMaxLength(120).IsRequired();
            entity.Property(user => user.CreatedAt).IsRequired();
            entity.Property(user => user.Email).HasMaxLength(256).IsRequired();
            entity.Property(user => user.UserName).HasMaxLength(256).IsRequired();
        });

        modelBuilder.Entity<IdentityRole<Guid>>().ToTable("Roles");
        modelBuilder.Entity<IdentityUserRole<Guid>>().ToTable("UserRoles");
        modelBuilder.Entity<IdentityUserClaim<Guid>>().ToTable("UserClaims");
        modelBuilder.Entity<IdentityUserLogin<Guid>>().ToTable("UserLogins");
        modelBuilder.Entity<IdentityRoleClaim<Guid>>().ToTable("RoleClaims");
        modelBuilder.Entity<IdentityUserToken<Guid>>().ToTable("UserTokens");
    }
}
