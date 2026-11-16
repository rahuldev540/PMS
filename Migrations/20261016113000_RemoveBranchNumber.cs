using AuthDemo.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable
namespace AuthDemo.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20261016113000_RemoveBranchNumber")]
public partial class RemoveBranchNumber : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(name: "IX_Branches_PharmacyId_BranchNumber", table: "Branches");
        migrationBuilder.DropColumn(name: "BranchNumber", table: "Branches");
    }
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>(name: "BranchNumber", table: "Branches", type: "int", nullable: true);
        migrationBuilder.Sql(@"UPDATE b SET BranchNumber = i.BranchId FROM dbo.Branches b JOIN dbo.PharmacyBranchIdentities i ON i.ExistingBranchId = b.Id;");
        migrationBuilder.CreateIndex(name: "IX_Branches_PharmacyId_BranchNumber", table: "Branches", columns: new[] { "PharmacyId", "BranchNumber" }, unique: true, filter: "[PharmacyId] IS NOT NULL AND [BranchNumber] IS NOT NULL");
    }
}
