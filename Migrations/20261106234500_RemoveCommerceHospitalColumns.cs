using AuthDemo.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AuthDemo.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20261106234500_RemoveCommerceHospitalColumns")]
public partial class RemoveCommerceHospitalColumns : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Fail closed: no existing non-null CMS associations may be discarded.
        foreach (var table in new[] { "Suppliers", "PurchaseOrders", "GoodsReceipts", "PharmacyPayments" })
        {
            migrationBuilder.Sql($@"IF EXISTS (SELECT 1 FROM [dbo].[{table}] WHERE [HospitalId] IS NOT NULL)
                THROW 51045, '{table}.HospitalId contains data; migration cancelled.', 1;");
        }

        migrationBuilder.DropColumn(name: "HospitalId", table: "Suppliers");
        migrationBuilder.DropColumn(name: "HospitalId", table: "PurchaseOrders");
        migrationBuilder.DropColumn(name: "HospitalId", table: "GoodsReceipts");
        migrationBuilder.DropColumn(name: "HospitalId", table: "PharmacyPayments");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>(name: "HospitalId", table: "Suppliers", type: "int", nullable: true);
        migrationBuilder.AddColumn<int>(name: "HospitalId", table: "PurchaseOrders", type: "int", nullable: true);
        migrationBuilder.AddColumn<int>(name: "HospitalId", table: "GoodsReceipts", type: "int", nullable: true);
        migrationBuilder.AddColumn<int>(name: "HospitalId", table: "PharmacyPayments", type: "int", nullable: true);
    }
}
