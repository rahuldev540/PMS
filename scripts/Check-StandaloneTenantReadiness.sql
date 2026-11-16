-- Phase 5B41: READ ONLY. Verify pharmacy tenant keys before removing legacy CMS columns.
-- Run in SSMS with PMSDB10 selected. Does not modify data or schema.
SET NOCOUNT ON;
SELECT DB_NAME() AS DatabaseName, SYSDATETIME() AS CheckedAt;
SELECT t.name AS TableName, c.name AS ColumnName, ty.name AS SqlType,
       c.is_nullable AS IsNullable
FROM sys.tables t
JOIN sys.columns c ON c.object_id=t.object_id
JOIN sys.types ty ON ty.user_type_id=c.user_type_id
WHERE c.name IN ('PharmacyId','BranchId','HospitalId','ClinicId','CmsHospitalId')
ORDER BY t.name,c.name;
-- List all legacy foreign keys and their dependent tables; dropping columns before these is unsafe.
SELECT fk.name AS ForeignKeyName, OBJECT_NAME(fk.parent_object_id) AS DependentTable,
       OBJECT_NAME(fk.referenced_object_id) AS ReferencedTable,
       fk.is_disabled AS IsDisabled
FROM sys.foreign_keys fk
WHERE OBJECT_NAME(fk.referenced_object_id)='Hospitals'
   OR OBJECT_NAME(fk.parent_object_id)='Hospitals'
ORDER BY DependentTable, ForeignKeyName;
-- Identify any rows with a missing pharmacy assignment in key tenant-owned tables.
-- Only query tables that actually have a PharmacyId column.
DECLARE @sql nvarchar(max)=N'';
SELECT @sql = @sql + N'SELECT ' + QUOTENAME(t.name,'''') + N' AS TableName, COUNT_BIG(*) AS TotalRows, SUM(CASE WHEN PharmacyId IS NULL THEN CONVERT(bigint,1) ELSE CONVERT(bigint,0) END) AS MissingPharmacyId FROM '
 + QUOTENAME(s.name)+N'.'+QUOTENAME(t.name)+N' UNION ALL '
FROM sys.tables t
JOIN sys.schemas s ON s.schema_id=t.schema_id
JOIN sys.columns c ON c.object_id=t.object_id AND c.name='PharmacyId'
WHERE t.name IN ('Branches','Users','Medicines','Inventories','Prescriptions','Patients','PharmacyBills','MedicineImports','Pharmacists');
IF LEN(@sql)>0 BEGIN
 SET @sql=LEFT(@sql,LEN(@sql)-LEN(' UNION ALL '));
 EXEC sp_executesql @sql;
END;
