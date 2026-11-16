-- Phase 5B38: READ ONLY. Run against a backed-up PMS database in SSMS.
-- Identifies actual columns, FK constraints and indexes before any EF migration.
SET NOCOUNT ON;
SELECT DB_NAME() AS DatabaseName, GETDATE() AS InspectedAt;
SELECT s.name AS SchemaName, t.name AS TableName, c.name AS ColumnName,
       ty.name AS DataType, c.is_nullable AS IsNullable
FROM sys.columns c
JOIN sys.tables t ON t.object_id=c.object_id
JOIN sys.schemas s ON s.schema_id=t.schema_id
JOIN sys.types ty ON ty.user_type_id=c.user_type_id
WHERE c.name IN ('HospitalId','ClinicId','ExternalHospitalId','CmsHospitalId')
ORDER BY s.name,t.name,c.name;
SELECT fk.name AS ForeignKeyName, OBJECT_SCHEMA_NAME(fk.parent_object_id) AS SchemaName,
       OBJECT_NAME(fk.parent_object_id) AS ParentTable,
       pc.name AS ParentColumn, OBJECT_NAME(fk.referenced_object_id) AS ReferencedTable,
       rc.name AS ReferencedColumn, fk.delete_referential_action_desc AS DeleteAction
FROM sys.foreign_keys fk
JOIN sys.foreign_key_columns fkc ON fkc.constraint_object_id=fk.object_id
JOIN sys.columns pc ON pc.object_id=fkc.parent_object_id AND pc.column_id=fkc.parent_column_id
JOIN sys.columns rc ON rc.object_id=fkc.referenced_object_id AND rc.column_id=fkc.referenced_column_id
WHERE pc.name IN ('HospitalId','ClinicId','ExternalHospitalId','CmsHospitalId')
   OR OBJECT_NAME(fk.referenced_object_id)='Hospitals'
ORDER BY ParentTable, ForeignKeyName;
SELECT OBJECT_SCHEMA_NAME(i.object_id) AS SchemaName, OBJECT_NAME(i.object_id) AS TableName,
       i.name AS IndexName, c.name AS ColumnName, ic.key_ordinal
FROM sys.indexes i
JOIN sys.index_columns ic ON ic.object_id=i.object_id AND ic.index_id=i.index_id
JOIN sys.columns c ON c.object_id=ic.object_id AND c.column_id=ic.column_id
WHERE c.name IN ('HospitalId','ClinicId','ExternalHospitalId','CmsHospitalId')
ORDER BY TableName,IndexName,ic.key_ordinal;
-- No ALTER, DROP, UPDATE, INSERT, or DELETE statements in this script.
