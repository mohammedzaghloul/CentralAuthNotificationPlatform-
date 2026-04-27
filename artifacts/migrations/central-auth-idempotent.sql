IF OBJECT_ID(N'[__EFMigrationsHistory]') IS NULL
BEGIN
    CREATE TABLE [__EFMigrationsHistory] (
        [MigrationId] nvarchar(150) NOT NULL,
        [ProductVersion] nvarchar(32) NOT NULL,
        CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
    );
END;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260426161627_InitialCreate'
)
BEGIN
    CREATE TABLE [Users] (
        [Id] uniqueidentifier NOT NULL,
        [Email] nvarchar(256) NOT NULL,
        [NormalizedEmail] nvarchar(256) NOT NULL,
        [DisplayName] nvarchar(120) NOT NULL,
        [PasswordHash] nvarchar(512) NOT NULL,
        [IsActive] bit NOT NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        CONSTRAINT [PK_Users] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260426161627_InitialCreate'
)
BEGIN
    CREATE TABLE [ExternalApps] (
        [Id] uniqueidentifier NOT NULL,
        [OwnerUserId] uniqueidentifier NOT NULL,
        [Name] nvarchar(120) NOT NULL,
        [ApiKeyHash] nvarchar(128) NOT NULL,
        [ApiKeyPreview] nvarchar(32) NOT NULL,
        [IsActive] bit NOT NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        [LastUsedAt] datetimeoffset NULL,
        CONSTRAINT [PK_ExternalApps] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ExternalApps_Users_OwnerUserId] FOREIGN KEY ([OwnerUserId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260426161627_InitialCreate'
)
BEGIN
    CREATE TABLE [Notifications] (
        [Id] uniqueidentifier NOT NULL,
        [UserId] uniqueidentifier NOT NULL,
        [Type] nvarchar(50) NOT NULL,
        [Title] nvarchar(160) NOT NULL,
        [Message] nvarchar(1000) NOT NULL,
        [SourceAppName] nvarchar(120) NULL,
        [MetadataJson] nvarchar(2000) NULL,
        [IsRead] bit NOT NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        [ReadAt] datetimeoffset NULL,
        CONSTRAINT [PK_Notifications] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Notifications_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260426161627_InitialCreate'
)
BEGIN
    CREATE TABLE [PasswordResetTokens] (
        [Id] uniqueidentifier NOT NULL,
        [UserId] uniqueidentifier NOT NULL,
        [ExternalAppId] uniqueidentifier NULL,
        [OtpHash] nvarchar(128) NOT NULL,
        [Salt] nvarchar(64) NOT NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        [ExpiresAt] datetimeoffset NOT NULL,
        [Attempts] int NOT NULL,
        [IsUsed] bit NOT NULL,
        [UsedAt] datetimeoffset NULL,
        CONSTRAINT [PK_PasswordResetTokens] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_PasswordResetTokens_ExternalApps_ExternalAppId] FOREIGN KEY ([ExternalAppId]) REFERENCES [ExternalApps] ([Id]) ON DELETE SET NULL,
        CONSTRAINT [FK_PasswordResetTokens_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260426161627_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_ExternalApps_ApiKeyHash] ON [ExternalApps] ([ApiKeyHash]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260426161627_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_ExternalApps_OwnerUserId_Name] ON [ExternalApps] ([OwnerUserId], [Name]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260426161627_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Notifications_UserId_CreatedAt] ON [Notifications] ([UserId], [CreatedAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260426161627_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_PasswordResetTokens_ExternalAppId] ON [PasswordResetTokens] ([ExternalAppId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260426161627_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_PasswordResetTokens_UserId_ExpiresAt] ON [PasswordResetTokens] ([UserId], [ExpiresAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260426161627_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Users_NormalizedEmail] ON [Users] ([NormalizedEmail]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260426161627_InitialCreate'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260426161627_InitialCreate', N'10.0.7');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260426163359_UpgradeToIdentityOAuth'
)
BEGIN
    DROP INDEX [IX_Users_NormalizedEmail] ON [Users];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260426163359_UpgradeToIdentityOAuth'
)
BEGIN
    EXEC sp_rename N'[PasswordResetTokens].[OtpHash]', N'TokenHash', 'COLUMN';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260426163359_UpgradeToIdentityOAuth'
)
BEGIN
    DECLARE @var nvarchar(max);
    SELECT @var = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Users]') AND [c].[name] = N'PasswordHash');
    IF @var IS NOT NULL EXEC(N'ALTER TABLE [Users] DROP CONSTRAINT ' + @var + ';');
    ALTER TABLE [Users] ALTER COLUMN [PasswordHash] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260426163359_UpgradeToIdentityOAuth'
)
BEGIN
    DECLARE @var1 nvarchar(max);
    SELECT @var1 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Users]') AND [c].[name] = N'NormalizedEmail');
    IF @var1 IS NOT NULL EXEC(N'ALTER TABLE [Users] DROP CONSTRAINT ' + @var1 + ';');
    ALTER TABLE [Users] ALTER COLUMN [NormalizedEmail] nvarchar(256) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260426163359_UpgradeToIdentityOAuth'
)
BEGIN
    ALTER TABLE [Users] ADD [AccessFailedCount] int NOT NULL DEFAULT 0;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260426163359_UpgradeToIdentityOAuth'
)
BEGIN
    ALTER TABLE [Users] ADD [ConcurrencyStamp] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260426163359_UpgradeToIdentityOAuth'
)
BEGIN
    ALTER TABLE [Users] ADD [EmailConfirmed] bit NOT NULL DEFAULT CAST(0 AS bit);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260426163359_UpgradeToIdentityOAuth'
)
BEGIN
    ALTER TABLE [Users] ADD [LockoutEnabled] bit NOT NULL DEFAULT CAST(0 AS bit);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260426163359_UpgradeToIdentityOAuth'
)
BEGIN
    ALTER TABLE [Users] ADD [LockoutEnd] datetimeoffset NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260426163359_UpgradeToIdentityOAuth'
)
BEGIN
    ALTER TABLE [Users] ADD [NormalizedUserName] nvarchar(256) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260426163359_UpgradeToIdentityOAuth'
)
BEGIN
    ALTER TABLE [Users] ADD [PhoneNumber] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260426163359_UpgradeToIdentityOAuth'
)
BEGIN
    ALTER TABLE [Users] ADD [PhoneNumberConfirmed] bit NOT NULL DEFAULT CAST(0 AS bit);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260426163359_UpgradeToIdentityOAuth'
)
BEGIN
    ALTER TABLE [Users] ADD [SecurityStamp] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260426163359_UpgradeToIdentityOAuth'
)
BEGIN
    ALTER TABLE [Users] ADD [TwoFactorEnabled] bit NOT NULL DEFAULT CAST(0 AS bit);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260426163359_UpgradeToIdentityOAuth'
)
BEGIN
    ALTER TABLE [Users] ADD [UserName] nvarchar(256) NOT NULL DEFAULT N'';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260426163359_UpgradeToIdentityOAuth'
)
BEGIN
    ALTER TABLE [ExternalApps] ADD [ClientId] nvarchar(80) NOT NULL DEFAULT N'';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260426163359_UpgradeToIdentityOAuth'
)
BEGIN
    ALTER TABLE [ExternalApps] ADD [ClientSecretHash] nvarchar(128) NOT NULL DEFAULT N'';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260426163359_UpgradeToIdentityOAuth'
)
BEGIN
    ALTER TABLE [ExternalApps] ADD [RedirectUri] nvarchar(500) NOT NULL DEFAULT N'';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260426163359_UpgradeToIdentityOAuth'
)
BEGIN
    CREATE TABLE [OAuthAuthorizationCodes] (
        [Id] uniqueidentifier NOT NULL,
        [UserId] uniqueidentifier NOT NULL,
        [ExternalAppId] uniqueidentifier NOT NULL,
        [CodeHash] nvarchar(128) NOT NULL,
        [RedirectUri] nvarchar(500) NOT NULL,
        [Scope] nvarchar(500) NOT NULL,
        [Nonce] nvarchar(160) NULL,
        [CodeChallenge] nvarchar(160) NULL,
        [CodeChallengeMethod] nvarchar(20) NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        [ExpiresAt] datetimeoffset NOT NULL,
        [IsUsed] bit NOT NULL,
        [UsedAt] datetimeoffset NULL,
        CONSTRAINT [PK_OAuthAuthorizationCodes] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_OAuthAuthorizationCodes_ExternalApps_ExternalAppId] FOREIGN KEY ([ExternalAppId]) REFERENCES [ExternalApps] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_OAuthAuthorizationCodes_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260426163359_UpgradeToIdentityOAuth'
)
BEGIN
    CREATE TABLE [Roles] (
        [Id] uniqueidentifier NOT NULL,
        [Name] nvarchar(256) NULL,
        [NormalizedName] nvarchar(256) NULL,
        [ConcurrencyStamp] nvarchar(max) NULL,
        CONSTRAINT [PK_Roles] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260426163359_UpgradeToIdentityOAuth'
)
BEGIN
    CREATE TABLE [UserApps] (
        [UserId] uniqueidentifier NOT NULL,
        [ExternalAppId] uniqueidentifier NOT NULL,
        [ConsentedAt] datetimeoffset NOT NULL,
        CONSTRAINT [PK_UserApps] PRIMARY KEY ([UserId], [ExternalAppId]),
        CONSTRAINT [FK_UserApps_ExternalApps_ExternalAppId] FOREIGN KEY ([ExternalAppId]) REFERENCES [ExternalApps] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_UserApps_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260426163359_UpgradeToIdentityOAuth'
)
BEGIN
    CREATE TABLE [UserClaims] (
        [Id] int NOT NULL IDENTITY,
        [UserId] uniqueidentifier NOT NULL,
        [ClaimType] nvarchar(max) NULL,
        [ClaimValue] nvarchar(max) NULL,
        CONSTRAINT [PK_UserClaims] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_UserClaims_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260426163359_UpgradeToIdentityOAuth'
)
BEGIN
    CREATE TABLE [UserLogins] (
        [LoginProvider] nvarchar(450) NOT NULL,
        [ProviderKey] nvarchar(450) NOT NULL,
        [ProviderDisplayName] nvarchar(max) NULL,
        [UserId] uniqueidentifier NOT NULL,
        CONSTRAINT [PK_UserLogins] PRIMARY KEY ([LoginProvider], [ProviderKey]),
        CONSTRAINT [FK_UserLogins_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260426163359_UpgradeToIdentityOAuth'
)
BEGIN
    CREATE TABLE [UserTokens] (
        [UserId] uniqueidentifier NOT NULL,
        [LoginProvider] nvarchar(450) NOT NULL,
        [Name] nvarchar(450) NOT NULL,
        [Value] nvarchar(max) NULL,
        CONSTRAINT [PK_UserTokens] PRIMARY KEY ([UserId], [LoginProvider], [Name]),
        CONSTRAINT [FK_UserTokens_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260426163359_UpgradeToIdentityOAuth'
)
BEGIN
    CREATE TABLE [RoleClaims] (
        [Id] int NOT NULL IDENTITY,
        [RoleId] uniqueidentifier NOT NULL,
        [ClaimType] nvarchar(max) NULL,
        [ClaimValue] nvarchar(max) NULL,
        CONSTRAINT [PK_RoleClaims] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_RoleClaims_Roles_RoleId] FOREIGN KEY ([RoleId]) REFERENCES [Roles] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260426163359_UpgradeToIdentityOAuth'
)
BEGIN
    CREATE TABLE [UserRoles] (
        [UserId] uniqueidentifier NOT NULL,
        [RoleId] uniqueidentifier NOT NULL,
        CONSTRAINT [PK_UserRoles] PRIMARY KEY ([UserId], [RoleId]),
        CONSTRAINT [FK_UserRoles_Roles_RoleId] FOREIGN KEY ([RoleId]) REFERENCES [Roles] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_UserRoles_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260426163359_UpgradeToIdentityOAuth'
)
BEGIN
    CREATE INDEX [EmailIndex] ON [Users] ([NormalizedEmail]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260426163359_UpgradeToIdentityOAuth'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [UserNameIndex] ON [Users] ([NormalizedUserName]) WHERE [NormalizedUserName] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260426163359_UpgradeToIdentityOAuth'
)
BEGIN
    CREATE UNIQUE INDEX [IX_ExternalApps_ClientId] ON [ExternalApps] ([ClientId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260426163359_UpgradeToIdentityOAuth'
)
BEGIN
    CREATE UNIQUE INDEX [IX_OAuthAuthorizationCodes_CodeHash] ON [OAuthAuthorizationCodes] ([CodeHash]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260426163359_UpgradeToIdentityOAuth'
)
BEGIN
    CREATE INDEX [IX_OAuthAuthorizationCodes_ExternalAppId] ON [OAuthAuthorizationCodes] ([ExternalAppId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260426163359_UpgradeToIdentityOAuth'
)
BEGIN
    CREATE INDEX [IX_OAuthAuthorizationCodes_UserId_ExternalAppId_ExpiresAt] ON [OAuthAuthorizationCodes] ([UserId], [ExternalAppId], [ExpiresAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260426163359_UpgradeToIdentityOAuth'
)
BEGIN
    CREATE INDEX [IX_RoleClaims_RoleId] ON [RoleClaims] ([RoleId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260426163359_UpgradeToIdentityOAuth'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [RoleNameIndex] ON [Roles] ([NormalizedName]) WHERE [NormalizedName] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260426163359_UpgradeToIdentityOAuth'
)
BEGIN
    CREATE INDEX [IX_UserApps_ExternalAppId] ON [UserApps] ([ExternalAppId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260426163359_UpgradeToIdentityOAuth'
)
BEGIN
    CREATE INDEX [IX_UserClaims_UserId] ON [UserClaims] ([UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260426163359_UpgradeToIdentityOAuth'
)
BEGIN
    CREATE INDEX [IX_UserLogins_UserId] ON [UserLogins] ([UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260426163359_UpgradeToIdentityOAuth'
)
BEGIN
    CREATE INDEX [IX_UserRoles_RoleId] ON [UserRoles] ([RoleId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260426163359_UpgradeToIdentityOAuth'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260426163359_UpgradeToIdentityOAuth', N'10.0.7');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260426173516_AddUserLinks'
)
BEGIN
    CREATE TABLE [UserLinks] (
        [Id] uniqueidentifier NOT NULL,
        [ExternalAppId] uniqueidentifier NOT NULL,
        [ExternalEmail] nvarchar(256) NOT NULL,
        [NormalizedExternalEmail] nvarchar(256) NOT NULL,
        [PlatformUserId] uniqueidentifier NOT NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        CONSTRAINT [PK_UserLinks] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_UserLinks_ExternalApps_ExternalAppId] FOREIGN KEY ([ExternalAppId]) REFERENCES [ExternalApps] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_UserLinks_Users_PlatformUserId] FOREIGN KEY ([PlatformUserId]) REFERENCES [Users] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260426173516_AddUserLinks'
)
BEGIN
    CREATE UNIQUE INDEX [IX_UserLinks_ExternalAppId_NormalizedExternalEmail] ON [UserLinks] ([ExternalAppId], [NormalizedExternalEmail]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260426173516_AddUserLinks'
)
BEGIN
    CREATE INDEX [IX_UserLinks_PlatformUserId] ON [UserLinks] ([PlatformUserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260426173516_AddUserLinks'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260426173516_AddUserLinks', N'10.0.7');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260426182324_AddAuditLogs'
)
BEGIN
    CREATE TABLE [AuditLogs] (
        [Id] uniqueidentifier NOT NULL,
        [Action] nvarchar(80) NOT NULL,
        [UserId] uniqueidentifier NULL,
        [ExternalAppId] uniqueidentifier NULL,
        [AppName] nvarchar(120) NULL,
        [ExternalEmail] nvarchar(256) NULL,
        [PlatformEmail] nvarchar(256) NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        [MetadataJson] nvarchar(2000) NULL,
        CONSTRAINT [PK_AuditLogs] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_AuditLogs_ExternalApps_ExternalAppId] FOREIGN KEY ([ExternalAppId]) REFERENCES [ExternalApps] ([Id]) ON DELETE SET NULL,
        CONSTRAINT [FK_AuditLogs_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE SET NULL
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260426182324_AddAuditLogs'
)
BEGIN
    CREATE INDEX [IX_AuditLogs_ExternalAppId_CreatedAt] ON [AuditLogs] ([ExternalAppId], [CreatedAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260426182324_AddAuditLogs'
)
BEGIN
    CREATE INDEX [IX_AuditLogs_UserId_CreatedAt] ON [AuditLogs] ([UserId], [CreatedAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260426182324_AddAuditLogs'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260426182324_AddAuditLogs', N'10.0.7');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260426185018_DeveloperIntegrationGenericExternalApps'
)
BEGIN
    EXEC sp_rename N'[UserLinks].[NormalizedExternalEmail]', N'NormalizedExternalUserId', 'COLUMN';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260426185018_DeveloperIntegrationGenericExternalApps'
)
BEGIN
    EXEC sp_rename N'[UserLinks].[ExternalEmail]', N'ExternalUserId', 'COLUMN';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260426185018_DeveloperIntegrationGenericExternalApps'
)
BEGIN
    EXEC sp_rename N'[UserLinks].[IX_UserLinks_ExternalAppId_NormalizedExternalEmail]', N'IX_UserLinks_ExternalAppId_NormalizedExternalUserId', 'INDEX';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260426185018_DeveloperIntegrationGenericExternalApps'
)
BEGIN
    EXEC sp_rename N'[AuditLogs].[ExternalEmail]', N'ExternalUserId', 'COLUMN';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260426185018_DeveloperIntegrationGenericExternalApps'
)
BEGIN
    ALTER TABLE [ExternalApps] ADD [Domain] nvarchar(253) NOT NULL DEFAULT N'localhost';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260426185018_DeveloperIntegrationGenericExternalApps'
)
BEGIN
    ALTER TABLE [ExternalApps] ADD [IsApiKeyActive] bit NOT NULL DEFAULT CAST(1 AS bit);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260426185018_DeveloperIntegrationGenericExternalApps'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260426185018_DeveloperIntegrationGenericExternalApps', N'10.0.7');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260427192737_AddExternalResetTokenIntegration'
)
BEGIN
    DROP INDEX [IX_PasswordResetTokens_ExternalAppId] ON [PasswordResetTokens];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260427192737_AddExternalResetTokenIntegration'
)
BEGIN
    ALTER TABLE [PasswordResetTokens] ADD [ExternalUserId] nvarchar(256) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260427192737_AddExternalResetTokenIntegration'
)
BEGIN
    ALTER TABLE [PasswordResetTokens] ADD [NormalizedExternalUserId] nvarchar(256) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260427192737_AddExternalResetTokenIntegration'
)
BEGIN
    CREATE INDEX [IX_PasswordResetTokens_ExternalAppId_NormalizedExternalUserId_ExpiresAt] ON [PasswordResetTokens] ([ExternalAppId], [NormalizedExternalUserId], [ExpiresAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260427192737_AddExternalResetTokenIntegration'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260427192737_AddExternalResetTokenIntegration', N'10.0.7');
END;

COMMIT;
GO

