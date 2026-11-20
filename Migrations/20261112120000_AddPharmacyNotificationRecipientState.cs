using System;
using AuthDemo.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AuthDemo.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20261112120000_AddPharmacyNotificationRecipientState")]
public partial class AddPharmacyNotificationRecipientState : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "PharmacyNotificationUserStates",
            columns: table => new
            {
                NotificationId = table.Column<int>(type: "int", nullable: false),
                UserId = table.Column<int>(type: "int", nullable: false),
                IsRead = table.Column<bool>(type: "bit", nullable: false),
                ReadAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_PharmacyNotificationUserStates", x => new { x.NotificationId, x.UserId });
                table.ForeignKey("FK_PharmacyNotificationUserStates_PharmacyNotifications_NotificationId", x => x.NotificationId,
                    "PharmacyNotifications", "Id", onDelete: ReferentialAction.Cascade);
            });
        migrationBuilder.CreateIndex(name: "IX_PharmacyNotificationUserStates_UserId_IsDeleted",
            table: "PharmacyNotificationUserStates", columns: new[] { "UserId", "IsDeleted" });

        migrationBuilder.CreateTable(
            name: "PharmacyNotificationForwards",
            columns: table => new
            {
                NotificationId = table.Column<int>(type: "int", nullable: false),
                SourceNotificationId = table.Column<int>(type: "int", nullable: false),
                ForwardedByUserId = table.Column<int>(type: "int", nullable: false),
                ForwardedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_PharmacyNotificationForwards", x => x.NotificationId);
                table.ForeignKey("FK_PharmacyNotificationForwards_PharmacyNotifications_NotificationId", x => x.NotificationId,
                    "PharmacyNotifications", "Id", onDelete: ReferentialAction.Cascade);
            });
        migrationBuilder.CreateIndex(name: "IX_PharmacyNotificationForwards_SourceNotificationId",
            table: "PharmacyNotificationForwards", column: "SourceNotificationId");
        migrationBuilder.CreateIndex(name: "IX_PharmacyNotificationForwards_ForwardedByUserId",
            table: "PharmacyNotificationForwards", column: "ForwardedByUserId");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Rollback only additive tables; the original notification and other tables are unaffected.
        migrationBuilder.DropTable(name: "PharmacyNotificationForwards");
        migrationBuilder.DropTable(name: "PharmacyNotificationUserStates");
    }
}
