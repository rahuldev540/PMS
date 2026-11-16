-- Phase 5B39: READ ONLY. Inspect legacy CMS values before planning an EF migration.
-- Open this file in SSMS and execute against the intended PMS database (e.g. PMSDB10).
-- This script does not modify data, constraints, or schema.
SET NOCOUNT ON;
SELECT DB_NAME() AS DatabaseName, SYSDATETIME() AS InspectedAt;

-- For each actual legacy column, show total rows and populated values.
-- Dynamic identifiers come ONLY from SQL Server metadata and are safely quoted.
CREATE TABLE #LegacyColumnCounts (
    SchemaName sysname NOT NULL,
    TableName sysname NOT NULL,
    ColumnName sysname NOT NULL,
    TotalRows bigint NOT NULL,
    NonNullLegacyValues bigint NOT NULL
);
DECLARE @schema sysname, @table sysname, @column sysname, @sql nvarchar(max);
DECLARE legacy_cursor CURSOR LOCAL FAST_FORWARD FOR
SELECT s.name, t.name, c.name
FROM sys.columns AS c
JOIN sys.tables AS t ON t.object_id = c.object_id
JOIN sys.schemas AS s ON s.schema_id = t.schema_id
WHERE c.name IN (N'HospitalId', N'ClinicId', N'ExternalHospitalId', N'CmsHospitalId')
ORDER BY s.name, t.name, c.name;
OPEN legacy_cursor;
FETCH NEXT FROM legacy_cursor INTO @schema, @table, @column;
WHILE @@FETCH_STATUS = 0
BEGIN
    SET @sql = N'INSERT INTO #LegacyColumnCounts (SchemaName, TableName, ColumnName, TotalRows, NonNullLegacyValues) '
             + N'SELECT @s, @t, @c, COUNT_BIG(*), COUNT_BIG(' + QUOTENAME(@column) + N') FROM '
             + QUOTENAME(@schema) + N'.' + QUOTENAME(@table) + N';';
    EXEC sys.sp_executesql @sql,
        N'@s sysname, @t sysname, @c sysname',
        @s = @schema, @t = @table, @c = @column;
    FETCH NEXT FROM legacy_cursor INTO @schema, @table, @column;
END;
CLOSE legacy_cursor;
DEALLOCATE legacy_cursor;
SELECT SchemaName, TableName, ColumnName, TotalRows, NonNullLegacyValues,
       TotalRows - NonNullLegacyValues AS NullLegacyValues
FROM #LegacyColumnCounts
ORDER BY NonNullLegacyValues DESC, SchemaName, TableName, ColumnName;
DROP TABLE #LegacyColumnCounts; -- removes only this session's temporary report table

-- Check for active FK constraints still targeting the legacy Hospitals table.
SELECT fk.name AS ForeignKeyName,
       OBJECT_SCHEMA_NAME(fk.parent_object_id) AS SchemaName,
       OBJECT_NAME(fk.parent_object_id) AS DependentTable,
       OBJECT_NAME(fk.referenced_object_id) AS ReferencedTable,
       fk.is_disabled AS IsDisabled
FROM sys.foreign_keys AS fk
WHERE OBJECT_NAME(fk.referenced_object_id) = N'Hospitals'
ORDER BY DependentTable, ForeignKeyName;
