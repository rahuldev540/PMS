using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Infrastructure;
using AuthDemo.Data;

#nullable disable
namespace AuthDemo.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20260925100000_PharmacyMainBranchAndLocation")]
public partial class PharmacyMainBranchAndLocation : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(name:"Address",table:"Pharmacies",type:"nvarchar(max)",nullable:true);
        migrationBuilder.AddColumn<string>(name:"City",table:"Pharmacies",type:"nvarchar(max)",nullable:true);
        migrationBuilder.AddColumn<string>(name:"Country",table:"Pharmacies",type:"nvarchar(max)",nullable:true);
        migrationBuilder.AddColumn<string>(name:"District",table:"Pharmacies",type:"nvarchar(max)",nullable:true);
        migrationBuilder.AddColumn<string>(name:"Email",table:"Pharmacies",type:"nvarchar(max)",nullable:true);
        migrationBuilder.AddColumn<string>(name:"Phone",table:"Pharmacies",type:"nvarchar(max)",nullable:true);
        migrationBuilder.AddColumn<string>(name:"PostalCode",table:"Pharmacies",type:"nvarchar(max)",nullable:true);
        migrationBuilder.AddColumn<string>(name:"State",table:"Pharmacies",type:"nvarchar(max)",nullable:true);
        migrationBuilder.AddColumn<bool>(name:"IsMainBranch",table:"Branches",type:"bit",nullable:false,defaultValue:false);
        migrationBuilder.AddColumn<DateTime>(name:"UpdatedAt",table:"Branches",type:"datetime2",nullable:false,defaultValueSql:"GETUTCDATE()");
        migrationBuilder.CreateIndex(name:"IX_Branches_PharmacyId_IsMainBranch",table:"Branches",columns:new[]{"PharmacyId","IsMainBranch"});
    }
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(name:"IX_Branches_PharmacyId_IsMainBranch",table:"Branches");
        migrationBuilder.DropColumn(name:"IsMainBranch",table:"Branches");
        migrationBuilder.DropColumn(name:"UpdatedAt",table:"Branches");
        foreach(var c in new[]{"Address","City","Country","District","Email","Phone","PostalCode","State"}) migrationBuilder.DropColumn(name:c,table:"Pharmacies");
    }
}
