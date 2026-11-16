using AuthDemo.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable
namespace AuthDemo.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20261015103000_AddPharmacyBranchIdentities")]
    public partial class AddPharmacyBranchIdentities : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PharmacyBranchIdentities",
                columns: table => new
                {
                    PharmacyId = table.Column<int>(type: "int", nullable: false),
                    BranchId = table.Column<int>(type: "int", nullable: false),
                    ExistingBranchId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PharmacyBranchIdentities", x => new { x.PharmacyId, x.BranchId });
                    table.ForeignKey("FK_PharmacyBranchIdentities_Branches_ExistingBranchId",
                        x => x.ExistingBranchId, "Branches", "Id", onDelete: ReferentialAction.Restrict);
                });
            migrationBuilder.CreateIndex(name: "IX_PharmacyBranchIdentities_ExistingBranchId",
                table: "PharmacyBranchIdentities", column: "ExistingBranchId", unique: true);
            // Backfill existing standalone branches. CMS-linked branches are untouched.
            migrationBuilder.Sql(@"
INSERT INTO dbo.PharmacyBranchIdentities (PharmacyId, BranchId, ExistingBranchId)
SELECT b.PharmacyId, b.BranchNumber, b.Id
FROM dbo.Branches b
INNER JOIN dbo.Pharmacies p ON p.Id = b.PharmacyId
WHERE b.PharmacyId IS NOT NULL
  AND b.BranchNumber IS NOT NULL
  AND p.IsCmsIntegrated = 0;");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "PharmacyBranchIdentities");
        }
    }
}
