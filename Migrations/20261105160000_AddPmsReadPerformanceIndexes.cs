using AuthDemo.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AuthDemo.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20261105160000_AddPmsReadPerformanceIndexes")]
public partial class AddPmsReadPerformanceIndexes : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateIndex(
            name: "IX_Medicines_PharmacyId_BranchId_CreatedAt",
            table: "Medicines",
            columns: new[] { "PharmacyId", "BranchId", "CreatedAt" });

        migrationBuilder.CreateIndex(
            name: "IX_PurchaseOrders_PharmacyId_BranchId_OrderDate",
            table: "PurchaseOrders",
            columns: new[] { "PharmacyId", "BranchId", "OrderDate" });

        migrationBuilder.CreateIndex(
            name: "IX_StockTransfers_PharmacyId_FromBranchId_RequestedAt",
            table: "StockTransfers",
            columns: new[] { "PharmacyId", "FromBranchId", "RequestedAt" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(name: "IX_Medicines_PharmacyId_BranchId_CreatedAt", table: "Medicines");
        migrationBuilder.DropIndex(name: "IX_PurchaseOrders_PharmacyId_BranchId_OrderDate", table: "PurchaseOrders");
        migrationBuilder.DropIndex(name: "IX_StockTransfers_PharmacyId_FromBranchId_RequestedAt", table: "StockTransfers");
    }
}
