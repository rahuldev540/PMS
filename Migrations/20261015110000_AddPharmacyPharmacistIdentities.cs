using AuthDemo.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable
namespace AuthDemo.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20261015110000_AddPharmacyPharmacistIdentities")]
public partial class AddPharmacyPharmacistIdentities : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "PharmacyPharmacistIdentities",
            columns: table => new
            {
                PharmacyId = table.Column<int>(type: "int", nullable: false),
                PharmacistId = table.Column<int>(type: "int", nullable: false),
                ExistingUserId = table.Column<int>(type: "int", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_PharmacyPharmacistIdentities", x => new { x.PharmacyId, x.PharmacistId });
                table.ForeignKey("FK_PharmacyPharmacistIdentities_Users_ExistingUserId", x => x.ExistingUserId,
                    "Users", "Id", onDelete: ReferentialAction.Restrict);
            });
        migrationBuilder.CreateIndex(name: "IX_PharmacyPharmacistIdentities_ExistingUserId",
            table: "PharmacyPharmacistIdentities", column: "ExistingUserId", unique: true);
        // Backfill existing standalone pharmacists with deterministic pharmacy-local IDs.
        migrationBuilder.Sql(@"INSERT INTO dbo.PharmacyPharmacistIdentities (PharmacyId, PharmacistId, ExistingUserId)
SELECT u.PharmacyId, ROW_NUMBER() OVER (PARTITION BY u.PharmacyId ORDER BY u.Id), u.Id
FROM dbo.Users u INNER JOIN dbo.Pharmacies p ON p.Id = u.PharmacyId
WHERE u.Role = 'Pharmacist' AND p.IsCmsIntegrated = 0;");
    }
    protected override void Down(MigrationBuilder migrationBuilder) =>
        migrationBuilder.DropTable(name: "PharmacyPharmacistIdentities");
}
