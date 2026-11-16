using AuthDemo.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable
namespace AuthDemo.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20261015064854_AddBranchNumberPerPharmacy")]
    public partial class AddBranchNumberPerPharmacy : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(name: "BranchNumber", table: "Branches", type: "int", nullable: true);
            migrationBuilder.CreateIndex(name: "IX_Branches_PharmacyId_BranchNumber", table: "Branches",
                columns: new[] { "PharmacyId", "BranchNumber" }, unique: true,
                filter: "[PharmacyId] IS NOT NULL AND [BranchNumber] IS NOT NULL");
        }
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(name: "IX_Branches_PharmacyId_BranchNumber", table: "Branches");
            migrationBuilder.DropColumn(name: "BranchNumber", table: "Branches");
        }
    }
}
