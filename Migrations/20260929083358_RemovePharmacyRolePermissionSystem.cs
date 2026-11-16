using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AuthDemo.Migrations
{
    /// <inheritdoc />
    public partial class RemovePharmacyRolePermissionSystem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Users_PharmacyPermissionRoles_PharmacyPermissionRoleId",
                table: "Users");

            migrationBuilder.DropTable(
                name: "PharmacyRolePermissions");

            migrationBuilder.DropTable(
                name: "PharmacyPermissionRoles");

            migrationBuilder.DropIndex(
                name: "IX_Users_PharmacyPermissionRoleId",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "PharmacyPermissionRoleId",
                table: "Users");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "PharmacyPermissionRoleId",
                table: "Users",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "PharmacyPermissionRoles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PharmacyPermissionRoles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PharmacyRolePermissions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PharmacyPermissionRoleId = table.Column<int>(type: "int", nullable: false),
                    CanCreate = table.Column<bool>(type: "bit", nullable: false),
                    CanDelete = table.Column<bool>(type: "bit", nullable: false),
                    CanEdit = table.Column<bool>(type: "bit", nullable: false),
                    CanView = table.Column<bool>(type: "bit", nullable: false),
                    Module = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PharmacyRolePermissions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PharmacyRolePermissions_PharmacyPermissionRoles_PharmacyPermissionRoleId",
                        column: x => x.PharmacyPermissionRoleId,
                        principalTable: "PharmacyPermissionRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Users_PharmacyPermissionRoleId",
                table: "Users",
                column: "PharmacyPermissionRoleId");

            migrationBuilder.CreateIndex(
                name: "IX_PharmacyPermissionRoles_Name",
                table: "PharmacyPermissionRoles",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PharmacyRolePermissions_PharmacyPermissionRoleId_Module",
                table: "PharmacyRolePermissions",
                columns: new[] { "PharmacyPermissionRoleId", "Module" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Users_PharmacyPermissionRoles_PharmacyPermissionRoleId",
                table: "Users",
                column: "PharmacyPermissionRoleId",
                principalTable: "PharmacyPermissionRoles",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }
    }
}
