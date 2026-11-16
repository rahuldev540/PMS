using AuthDemo.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable
namespace AuthDemo.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20261107000000_RemoveBillingImportSettingsHospitalColumns")]
public partial class RemoveBillingImportSettingsHospitalColumns : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(@"IF EXISTS (SELECT 1 FROM [dbo].[MedicineImports] WHERE [HospitalId] IS NOT NULL)
            THROW 51046, 'MedicineImports.HospitalId contains data; migration cancelled.', 1;");
        migrationBuilder.Sql(@"IF EXISTS (SELECT 1 FROM [dbo].[PharmacyBills] WHERE [HospitalId] IS NOT NULL)
            THROW 51046, 'PharmacyBills.HospitalId contains data; migration cancelled.', 1;");
        migrationBuilder.Sql(@"IF EXISTS (SELECT 1 FROM [dbo].[PharmacyBranchSettings] WHERE [HospitalId] IS NOT NULL)
            THROW 51046, 'PharmacyBranchSettings.HospitalId contains data; migration cancelled.', 1;");
        migrationBuilder.DropColumn(name: "HospitalId", table: "MedicineImports");
        migrationBuilder.DropColumn(name: "HospitalId", table: "PharmacyBills");
        migrationBuilder.DropColumn(name: "HospitalId", table: "PharmacyBranchSettings");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>(name: "HospitalId", table: "MedicineImports", type: "int", nullable: true);
        migrationBuilder.AddColumn<int>(name: "HospitalId", table: "PharmacyBills", type: "int", nullable: true);
        migrationBuilder.AddColumn<int>(name: "HospitalId", table: "PharmacyBranchSettings", type: "int", nullable: true);
    }
}
