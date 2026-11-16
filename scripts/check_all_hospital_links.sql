-- PMSDB10: comprehensive read-only check of every remaining HospitalId column.
-- Does not modify any data or schema.
SET NOCOUNT ON;
DECLARE @sql nvarchar(max) = N'';
SELECT @sql = @sql + N'SELECT N''' + REPLACE(s.name + N'.' + t.name,'''','''''') + N''' AS TableName, COUNT_BIG(*) AS LinkedRows FROM ' + QUOTENAME(s.name) + N'.' + QUOTENAME(t.name) + N' WHERE [HospitalId] IS NOT NULL UNION ALL '
FROM sys.tables t JOIN sys.schemas s ON s.schema_id=t.schema_id
JOIN sys.columns c ON c.object_id=t.object_id AND c.name=N'HospitalId';
IF LEN(@sql)>0 BEGIN SET @sql=LEFT(@sql,LEN(@sql)-LEN(N' UNION ALL ')); EXEC sp_executesql @sql; END
ELSE SELECT N'No HospitalId columns remain' AS Result;
SELECT s.name AS SchemaName,t.name AS TableName,c.name AS ColumnName
FROM sys.columns c JOIN sys.tables t ON t.object_id=c.object_id JOIN sys.schemas s ON s.schema_id=t.schema_id
WHERE c.name LIKE N'%Hospital%' OR t.name LIKE N'%Hospital%' ORDER BY t.name,c.name;
SELECT fk.name AS ForeignKeyName,OBJECT_SCHEMA_NAME(fk.parent_object_id) AS ParentSchema,OBJECT_NAME(fk.parent_object_id) AS ParentTable,OBJECT_NAME(fk.referenced_object_id) AS ReferencedTable
FROM sys.foreign_keys fk WHERE OBJECT_NAME(fk.referenced_object_id) LIKE N'%Hospital%' OR OBJECT_NAME(fk.parent_object_id) LIKE N'%Hospital%';
