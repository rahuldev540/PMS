using AuthDemo.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable
namespace AuthDemo.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20261023180000_AddPharmacyPatientNumbers")]
public partial class AddPharmacyPatientNumbers : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>(name: "PharmacyPatientId", table: "Patients", type: "int", nullable: true);
        migrationBuilder.Sql(@"WITH Numbered AS (
            SELECT Id, ROW_NUMBER() OVER (PARTITION BY PharmacyId ORDER BY Id) AS N
            FROM dbo.Patients WHERE PharmacyId IS NOT NULL
        )
        UPDATE p SET PharmacyPatientId = n.N FROM dbo.Patients p
        INNER JOIN Numbered n ON n.Id = p.Id;");
        migrationBuilder.CreateIndex(name: "IX_Patients_PharmacyId_PharmacyPatientId",
            table: "Patients", columns: new[] { "PharmacyId", "PharmacyPatientId" },
            unique: true, filter: "[PharmacyId] IS NOT NULL AND [PharmacyPatientId] IS NOT NULL");
    }
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(name: "IX_Patients_PharmacyId_PharmacyPatientId", table: "Patients");
        migrationBuilder.DropColumn(name: "PharmacyPatientId", table: "Patients");
    }
}
