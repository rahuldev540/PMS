using AuthDemo.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable
namespace AuthDemo.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20261107003000_RemoveUserPermissionHospitalColumn")]
public partial class RemoveUserPermissionHospitalColumn : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(@"IF EXISTS (SELECT 1 FROM [dbo].[UserPermissions] WHERE [HospitalId] IS NOT NULL)
            THROW 51047, 'UserPermissions.HospitalId contains data; migration cancelled.', 1;");
        migrationBuilder.DropForeignKey(name: "FK_UserPermissions_Hospitals_HospitalId", table: "UserPermissions");
        migrationBuilder.DropIndex(name: "IX_UserPermissions_HospitalId", table: "UserPermissions");
        migrationBuilder.DropColumn(name: "HospitalId", table: "UserPermissions");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>(name: "HospitalId", table: "UserPermissions", type: "int", nullable: true);
        migrationBuilder.CreateIndex(name: "IX_UserPermissions_HospitalId", table: "UserPermissions", column: "HospitalId");
        migrationBuilder.AddForeignKey(name: "FK_UserPermissions_Hospitals_HospitalId", table: "UserPermissions", column: "HospitalId", principalTable: "Hospitals", principalColumn: "Id");
    }
}
