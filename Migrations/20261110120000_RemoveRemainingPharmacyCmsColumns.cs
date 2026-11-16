using AuthDemo.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable
namespace AuthDemo.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20261110120000_RemoveRemainingPharmacyCmsColumns")]
public partial class RemoveRemainingPharmacyCmsColumns : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(@"
IF OBJECT_ID(N'dbo.Pharmacies', N'U') IS NOT NULL
BEGIN
    DECLARE @sql nvarchar(max) = N'';
    -- Remove non-constraint indexes involving legacy CMS columns.
    SELECT @sql += N'DROP INDEX ' + QUOTENAME(i.name) + N' ON dbo.Pharmacies;' + CHAR(10)
    FROM sys.indexes i
    WHERE i.object_id = OBJECT_ID(N'dbo.Pharmacies')
      AND i.name IS NOT NULL AND i.is_primary_key = 0 AND i.is_unique_constraint = 0
      AND EXISTS (SELECT 1 FROM sys.index_columns ic
        JOIN sys.columns c ON c.object_id=ic.object_id AND c.column_id=ic.column_id
        WHERE ic.object_id=i.object_id AND ic.index_id=i.index_id
        AND c.name IN (N'IsCmsIntegrated',N'CmsHospitalId',N'CmsBranchId',N'CmsLinkedAt',N'CmsLinkedByUserId'));
    EXEC sys.sp_executesql @sql;

    -- Drop default constraints before dropping their columns.
    SET @sql=N'';
    SELECT @sql += N'ALTER TABLE dbo.Pharmacies DROP CONSTRAINT ' + QUOTENAME(dc.name) + N';' + CHAR(10)
    FROM sys.default_constraints dc
    JOIN sys.columns c ON c.object_id=dc.parent_object_id AND c.column_id=dc.parent_column_id
    WHERE dc.parent_object_id=OBJECT_ID(N'dbo.Pharmacies')
      AND c.name IN (N'IsCmsIntegrated',N'CmsHospitalId',N'CmsBranchId',N'CmsLinkedAt',N'CmsLinkedByUserId');
    EXEC sys.sp_executesql @sql;

    SET @sql=N'';
    SELECT @sql += N'ALTER TABLE dbo.Pharmacies DROP COLUMN ' + QUOTENAME(c.name) + N';' + CHAR(10)
    FROM sys.columns c
    WHERE c.object_id=OBJECT_ID(N'dbo.Pharmacies')
      AND c.name IN (N'IsCmsIntegrated',N'CmsHospitalId',N'CmsBranchId',N'CmsLinkedAt',N'CmsLinkedByUserId');
    EXEC sys.sp_executesql @sql;
END
");
    }
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        throw new System.NotSupportedException("Restore a verified backup to recover removed CMS columns.");
    }
}
