using AuthDemo.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AuthDemo.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20261106233000_RemoveOperationalHospitalColumns")]
public partial class RemoveOperationalHospitalColumns : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Refuse to drop any legacy data on a database that differs from the inspected PMSDB10.
        foreach (var table in new[] { "Inventories", "Medicines", "StockTransactions", "PharmacyNotifications" })
        {
            migrationBuilder.Sql($@"IF EXISTS (SELECT 1 FROM [dbo].[{table}] WHERE [HospitalId] IS NOT NULL)
                THROW 51044, '{table}.HospitalId contains data; migration cancelled.', 1;");
        }

        migrationBuilder.DropIndex(name: "IX_PharmacyNotifications_HospitalId_BranchId_UserId", table: "PharmacyNotifications");

        migrationBuilder.DropColumn(name: "HospitalId", table: "Inventories");
        migrationBuilder.DropColumn(name: "HospitalId", table: "Medicines");
        migrationBuilder.DropColumn(name: "HospitalId", table: "StockTransactions");
        migrationBuilder.DropColumn(name: "HospitalId", table: "PharmacyNotifications");

        migrationBuilder.CreateIndex(name: "IX_PharmacyNotifications_BranchId_UserId", table: "PharmacyNotifications", columns: new[] { "BranchId", "UserId" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(name: "IX_PharmacyNotifications_BranchId_UserId", table: "PharmacyNotifications");

        migrationBuilder.AddColumn<int>(name: "HospitalId", table: "Inventories", type: "int", nullable: true);
        migrationBuilder.AddColumn<int>(name: "HospitalId", table: "Medicines", type: "int", nullable: true);
        migrationBuilder.AddColumn<int>(name: "HospitalId", table: "StockTransactions", type: "int", nullable: true);
        migrationBuilder.AddColumn<int>(name: "HospitalId", table: "PharmacyNotifications", type: "int", nullable: true);

        migrationBuilder.CreateIndex(name: "IX_PharmacyNotifications_HospitalId_BranchId_UserId", table: "PharmacyNotifications", columns: new[] { "HospitalId", "BranchId", "UserId" });
    }
}
