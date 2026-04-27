using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CentralAuthNotificationPlatform.Migrations
{
    /// <inheritdoc />
    public partial class DeveloperIntegrationGenericExternalApps : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "NormalizedExternalEmail",
                table: "UserLinks",
                newName: "NormalizedExternalUserId");

            migrationBuilder.RenameColumn(
                name: "ExternalEmail",
                table: "UserLinks",
                newName: "ExternalUserId");

            migrationBuilder.RenameIndex(
                name: "IX_UserLinks_ExternalAppId_NormalizedExternalEmail",
                table: "UserLinks",
                newName: "IX_UserLinks_ExternalAppId_NormalizedExternalUserId");

            migrationBuilder.RenameColumn(
                name: "ExternalEmail",
                table: "AuditLogs",
                newName: "ExternalUserId");

            migrationBuilder.AddColumn<string>(
                name: "Domain",
                table: "ExternalApps",
                type: "nvarchar(253)",
                maxLength: 253,
                nullable: false,
                defaultValue: "localhost");

            migrationBuilder.AddColumn<bool>(
                name: "IsApiKeyActive",
                table: "ExternalApps",
                type: "bit",
                nullable: false,
                defaultValue: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Domain",
                table: "ExternalApps");

            migrationBuilder.DropColumn(
                name: "IsApiKeyActive",
                table: "ExternalApps");

            migrationBuilder.RenameColumn(
                name: "NormalizedExternalUserId",
                table: "UserLinks",
                newName: "NormalizedExternalEmail");

            migrationBuilder.RenameColumn(
                name: "ExternalUserId",
                table: "UserLinks",
                newName: "ExternalEmail");

            migrationBuilder.RenameIndex(
                name: "IX_UserLinks_ExternalAppId_NormalizedExternalUserId",
                table: "UserLinks",
                newName: "IX_UserLinks_ExternalAppId_NormalizedExternalEmail");

            migrationBuilder.RenameColumn(
                name: "ExternalUserId",
                table: "AuditLogs",
                newName: "ExternalEmail");
        }
    }
}
