using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CentralAuthNotificationPlatform.Migrations
{
    /// <inheritdoc />
    public partial class AddUserLinks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "UserLinks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ExternalAppId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ExternalEmail = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    NormalizedExternalEmail = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    PlatformUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserLinks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserLinks_ExternalApps_ExternalAppId",
                        column: x => x.ExternalAppId,
                        principalTable: "ExternalApps",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserLinks_Users_PlatformUserId",
                        column: x => x.PlatformUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UserLinks_ExternalAppId_NormalizedExternalEmail",
                table: "UserLinks",
                columns: new[] { "ExternalAppId", "NormalizedExternalEmail" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserLinks_PlatformUserId",
                table: "UserLinks",
                column: "PlatformUserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UserLinks");
        }
    }
}
