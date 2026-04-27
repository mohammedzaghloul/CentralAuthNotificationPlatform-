using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CentralAuthNotificationPlatform.Migrations
{
    /// <inheritdoc />
    public partial class AddExternalResetTokenIntegration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PasswordResetTokens_ExternalAppId",
                table: "PasswordResetTokens");

            migrationBuilder.AddColumn<string>(
                name: "ExternalUserId",
                table: "PasswordResetTokens",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NormalizedExternalUserId",
                table: "PasswordResetTokens",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_PasswordResetTokens_ExternalAppId_NormalizedExternalUserId_ExpiresAt",
                table: "PasswordResetTokens",
                columns: new[] { "ExternalAppId", "NormalizedExternalUserId", "ExpiresAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PasswordResetTokens_ExternalAppId_NormalizedExternalUserId_ExpiresAt",
                table: "PasswordResetTokens");

            migrationBuilder.DropColumn(
                name: "ExternalUserId",
                table: "PasswordResetTokens");

            migrationBuilder.DropColumn(
                name: "NormalizedExternalUserId",
                table: "PasswordResetTokens");

            migrationBuilder.CreateIndex(
                name: "IX_PasswordResetTokens_ExternalAppId",
                table: "PasswordResetTokens",
                column: "ExternalAppId");
        }
    }
}
