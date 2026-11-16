-- Phase 5B40: READ-ONLY migration gate for standalone PMS.
-- Open in SSMS with PMSDB10 selected. NO database schema/data changes.
SET NOCOUNT ON;
SELECT DB_NAME() AS DatabaseName, SYSDATETIME() AS CheckedAt;

CREATE TABLE #LegacyGate (
 SchemaName sysname NOT NULL, TableName sysname NOT NULL,
 ColumnName sysname NOT NULL, TotalRows bigint NOT NULL,
 PopulatedRows bigint NOT NULL
);
DECLARE @s sysname, @t sysname, @c sysname, @statement nvarchar(max);
DECLARE column_cursor CURSOR LOCAL FAST_FORWARD FOR
SELECT s.name, t.name, c.name
FROM sys.columns c
JOIN sys.tables t ON t.object_id = c.object_id
JOIN sys.schemas s ON s.schema_id = t.schema_id
WHERE c.name IN (N'HospitalId', N'ClinicId', N'ExternalHospitalId', N'CmsHospitalId')
ORDER BY s.name,t.name,c.name;
OPEN column_cursor;
FETCH NEXT FROM column_cursor INTO @s,@t,@c;
WHILE @@FETCH_STATUS = 0
BEGIN
 SET @statement = N'INSERT INTO #LegacyGate (SchemaName,TableName,ColumnName,TotalRows,PopulatedRows) '
 + N'SELECT @s,@t,@c,COUNT_BIG(*),COUNT_BIG(' + QUOTENAME(@c) + N') FROM '
 + QUOTENAME(@s) + N'.' + QUOTENAME(@t) + N';';
 EXEC sys.sp_executesql @statement, N'@s sysname,@t sysname,@c sysname', @s=@s,@t=@t,@c=@c;
 FETCH NEXT FROM column_cursor INTO @s,@t,@c;
END;
CLOSE column_cursor;
DEALLOCATE column_cursor;

-- RESULT 2: migration readiness summary. READY means only data-value check passed;
-- it DOES NOT mean dropping columns is safe without EF model/migration updates.
SELECT COUNT(*) AS LegacyColumns,
       COALESCE(SUM(PopulatedRows),0) AS TotalPopulatedLegacyValues,
       SUM(CASE WHEN PopulatedRows > 0 THEN 1 ELSE 0 END) AS PopulatedColumns,
       CASE WHEN COALESCE(SUM(PopulatedRows),0)=0
         THEN 'DATA CHECK PASSED - MODEL/FK/INDEX MIGRATION STILL REQUIRED'
         ELSE 'BLOCKED - REVIEW POPULATED LEGACY VALUES BEFORE MIGRATION' END AS DataReadiness
FROM #LegacyGate;

-- RESULT 3: all legacy columns, ordered to show any populated values first.
SELECT SchemaName,TableName,ColumnName,TotalRows,PopulatedRows,
       TotalRows-PopulatedRows AS NullRows
FROM #LegacyGate
ORDER BY PopulatedRows DESC,SchemaName,TableName,ColumnName;

-- RESULT 4: dependent foreign keys and their enabled status.
SELECT fk.name AS ForeignKeyName,
 OBJECT_SCHEMA_NAME(fk.parent_object_id) AS SchemaName,
 OBJECT_NAME(fk.parent_object_id) AS DependentTable,
 OBJECT_NAME(fk.referenced_object_id) AS ReferencedTable,
 fk.is_disabled AS IsDisabled
FROM sys.foreign_keys fk
WHERE OBJECT_NAME(fk.referenced_object_id)=N'Hospitals'
ORDER BY DependentTable,ForeignKeyName;

-- RESULT 5: legacy indexes. Drop/rebuild only through reviewed EF migration.
SELECT OBJECT_SCHEMA_NAME(i.object_id) AS SchemaName,
 OBJECT_NAME(i.object_id) AS TableName,i.name AS IndexName,
 c.name AS ColumnName
FROM sys.indexes i
JOIN sys.index_columns ic ON ic.object_id=i.object_id AND ic.index_id=i.index_id
JOIN sys.columns c ON c.object_id=ic.object_id AND c.column_id=ic.column_id
WHERE c.name IN (N'HospitalId',N'ClinicId',N'ExternalHospitalId',N'CmsHospitalId')
ORDER BY TableName,IndexName,ic.key_ordinal;
DROP TABLE #LegacyGate; -- local temporary report table only
