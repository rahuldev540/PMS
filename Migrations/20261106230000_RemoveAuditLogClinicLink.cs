using AuthDemo.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AuthDemo.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20261106230000_RemoveAuditLogClinicLink")]
public partial class RemoveAuditLogClinicLink : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // A different database may contain CMS-linked records even if PMSDB10 does not.
        // Refuse to discard that data rather than silently removing the relationship.
        migrationBuilder.Sql(@"IF EXISTS (SELECT 1 FROM [dbo].[AuditLogs] WHERE [ClinicId] IS NOT NULL)
            THROW 51043, 'AuditLogs.ClinicId contains values; review and migrate them before removing the column.', 1;");

        migrationBuilder.DropForeignKey(
            name: "FK_AuditLogs_Hospitals_ClinicId",
            table: "AuditLogs");

        migrationBuilder.DropIndex(
            name: "IX_AuditLogs_ClinicId",
            table: "AuditLogs");

        migrationBuilder.DropColumn(
            name: "ClinicId",
            table: "AuditLogs");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>(
            name: "ClinicId",
            table: "AuditLogs",
            type: "int",
            nullable: true);

        migrationBuilder.CreateIndex(
            name: "IX_AuditLogs_ClinicId",
            table: "AuditLogs",
            column: "ClinicId");

        migrationBuilder.AddForeignKey(
            name: "FK_AuditLogs_Hospitals_ClinicId",
            table: "AuditLogs",
            column: "ClinicId",
            principalTable: "Hospitals",
            principalColumn: "Id");
    }
}
