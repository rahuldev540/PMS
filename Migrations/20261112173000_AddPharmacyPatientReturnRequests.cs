using System;
using AuthDemo.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable
namespace AuthDemo.Migrations;

/// <summary>
/// Strictly additive: creates two new tables, leaving existing patients,
/// PharmacyReturns, bills, stock and inventory untouched.
/// </summary>
[DbContext(typeof(AppDbContext))]
[Migration("20261112173000_AddPharmacyPatientReturnRequests")]
public sealed class AddPharmacyPatientReturnRequests : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "PharmacyPatientReturnRequests",
            columns: table => new
            {
                Id = table.Column<int>(type: "int", nullable: false)
                    .Annotation("SqlServer:Identity", "1, 1"),
                PharmacyId = table.Column<int>(type: "int", nullable: false),
                BranchId = table.Column<int>(type: "int", nullable: false),
                PharmacyBillId = table.Column<int>(type: "int", nullable: false),
                PatientName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                Reason = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                Notes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                VerifiedForRestocking = table.Column<bool>(type: "bit", nullable: false),
                CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_PharmacyPatientReturnRequests", x => x.Id);
            });
        migrationBuilder.CreateTable(
            name: "PharmacyPatientReturnRequestLines",
            columns: table => new
            {
                Id = table.Column<int>(type: "int", nullable: false)
                    .Annotation("SqlServer:Identity", "1, 1"),
                RequestId = table.Column<int>(type: "int", nullable: false),
                PharmacyReturnId = table.Column<int>(type: "int", nullable: false),
                OriginalBatchNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                Condition = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_PharmacyPatientReturnRequestLines", x => x.Id);
                table.ForeignKey("FK_PharmacyPatientReturnRequestLines_PharmacyPatientReturnRequests_RequestId",
                    x => x.RequestId, "PharmacyPatientReturnRequests", "Id", onDelete: ReferentialAction.Cascade);
                table.ForeignKey("FK_PharmacyPatientReturnRequestLines_PharmacyReturns_PharmacyReturnId",
                    x => x.PharmacyReturnId, "PharmacyReturns", "Id", onDelete: ReferentialAction.Restrict);
            });
        migrationBuilder.CreateIndex(
            name: "IX_PharmacyPatientReturnRequests_PharmacyId_BranchId_CreatedAt",
            table: "PharmacyPatientReturnRequests",
            columns: new[] { "PharmacyId", "BranchId", "CreatedAt" });
        migrationBuilder.CreateIndex(
            name: "IX_PharmacyPatientReturnRequestLines_RequestId",
            table: "PharmacyPatientReturnRequestLines", column: "RequestId");
        migrationBuilder.CreateIndex(
            name: "IX_PharmacyPatientReturnRequestLines_PharmacyReturnId",
            table: "PharmacyPatientReturnRequestLines", column: "PharmacyReturnId", unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "PharmacyPatientReturnRequestLines");
        migrationBuilder.DropTable(name: "PharmacyPatientReturnRequests");
    }
}
