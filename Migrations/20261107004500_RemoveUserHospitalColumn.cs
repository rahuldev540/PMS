using AuthDemo.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable
namespace AuthDemo.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20261107004500_RemoveUserHospitalColumn")]
public partial class RemoveUserHospitalColumn : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(@"IF EXISTS (SELECT 1 FROM [dbo].[Users] WHERE [HospitalId] IS NOT NULL)
            THROW 51048, 'Users.HospitalId contains data; migration cancelled. Review records before removal.', 1;");
        migrationBuilder.DropForeignKey(name: "FK_Users_Hospitals_HospitalId", table: "Users");
        migrationBuilder.DropIndex(name: "IX_Users_HospitalId", table: "Users");
        migrationBuilder.DropColumn(name: "HospitalId", table: "Users");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>(name: "HospitalId", table: "Users", type: "int", nullable: true);
        migrationBuilder.CreateIndex(name: "IX_Users_HospitalId", table: "Users", column: "HospitalId");
        migrationBuilder.AddForeignKey(name: "FK_Users_Hospitals_HospitalId", table: "Users", column: "HospitalId", principalTable: "Hospitals", principalColumn: "Id");
    }
}
