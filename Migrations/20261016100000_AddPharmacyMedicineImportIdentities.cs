using AuthDemo.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable
namespace AuthDemo.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20261016100000_AddPharmacyMedicineImportIdentities")]
public partial class AddPharmacyMedicineImportIdentities : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "PharmacyMedicineImportIdentities",
            columns: table => new
            {
                PharmacyId = table.Column<int>(type: "int", nullable: false),
                ImportId = table.Column<int>(type: "int", nullable: false),
                ExistingImportId = table.Column<int>(type: "int", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_PharmacyMedicineImportIdentities", x => new { x.PharmacyId, x.ImportId });
                table.ForeignKey("FK_PharmacyMedicineImportIdentities_MedicineImports_ExistingImportId", x => x.ExistingImportId,
                    "MedicineImports", "Id", onDelete: ReferentialAction.Restrict);
            });
        migrationBuilder.CreateIndex(name: "IX_PharmacyMedicineImportIdentities_ExistingImportId",
            table: "PharmacyMedicineImportIdentities", column: "ExistingImportId", unique: true);
        migrationBuilder.Sql(@"INSERT INTO dbo.PharmacyMedicineImportIdentities (PharmacyId, ImportId, ExistingImportId)
SELECT mi.PharmacyId, ROW_NUMBER() OVER (PARTITION BY mi.PharmacyId ORDER BY mi.Id), mi.Id
FROM dbo.MedicineImports mi WHERE mi.PharmacyId IS NOT NULL;");
    }

    protected override void Down(MigrationBuilder migrationBuilder) =>
        migrationBuilder.DropTable(name: "PharmacyMedicineImportIdentities");
}
