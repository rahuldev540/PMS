-- Read-only SQL Server inventory
SELECT s.name AS SchemaName,t.name AS TableName,c.name AS ColumnName
FROM sys.columns c JOIN sys.tables t ON t.object_id=c.object_id
JOIN sys.schemas s ON s.schema_id=t.schema_id
WHERE c.name LIKE '%Hospital%' OR t.name LIKE '%Hospital%' OR t.name LIKE '%CMS%'
ORDER BY s.name,t.name,c.name;
SELECT fk.name AS ForeignKeyName, OBJECT_NAME(fk.parent_object_id) AS ParentTable,
OBJECT_NAME(fk.referenced_object_id) AS ReferencedTable
FROM sys.foreign_keys fk
WHERE fk.name LIKE '%Hospital%' OR OBJECT_NAME(fk.referenced_object_id) LIKE '%Hospital%';
