-- READ-ONLY inventory; no schema changes.
SELECT t.name AS TableName,c.name AS ColumnName,ty.name AS DataType
FROM sys.tables t JOIN sys.columns c ON c.object_id=t.object_id JOIN sys.types ty ON ty.user_type_id=c.user_type_id
WHERE c.name LIKE '%Hospital%' OR c.name LIKE '%External%' OR c.name='SourceSystem'
ORDER BY t.name,c.name;
SELECT t.name AS TableName,f.name AS ForeignKeyName
FROM sys.foreign_keys f JOIN sys.tables t ON t.object_id=f.parent_object_id
WHERE f.name LIKE '%Hospital%' OR t.name LIKE '%Hospital%'
ORDER BY t.name;
SELECT name AS TableName FROM sys.tables WHERE name LIKE '%Hospital%' OR name LIKE '%Cms%' ORDER BY name;
