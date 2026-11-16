BEGIN TRANSACTION;
GO


-- Fail safely if any remaining hospital identifier is populated.
DECLARE @check nvarchar(max) = N'';
SELECT @check += N'IF EXISTS (SELECT 1 FROM ' + QUOTENAME(s.name) + N'.' + QUOTENAME(t.name)
    + N' WHERE [HospitalId] IS NOT NULL) THROW 51101, ''HospitalId contains linked records; aborting standalone cleanup'', 1;' + CHAR(10)
FROM sys.columns c JOIN sys.tables t ON c.object_id=t.object_id
JOIN sys.schemas s ON t.schema_id=s.schema_id WHERE c.name=N'HospitalId';
EXEC sys.sp_executesql @check;
IF OBJECT_ID(N'dbo.Hospitals', N'U') IS NOT NULL
    EXEC sys.sp_executesql N'IF EXISTS (SELECT 1 FROM dbo.Hospitals) THROW 51102, ''Hospitals contains records; aborting standalone cleanup'', 1;';

-- Abort on unsupported dependencies rather than making destructive guesses.
IF EXISTS (
 SELECT 1 FROM sys.columns c JOIN sys.tables t ON t.object_id=c.object_id
 WHERE c.name=N'HospitalId' AND (c.is_computed=1 OR t.temporal_type<>0)
) THROW 51103, 'Computed/temporal HospitalId dependency requires manual review', 1;
IF EXISTS (
 SELECT 1 FROM sys.indexes i JOIN sys.index_columns ic ON ic.object_id=i.object_id AND ic.index_id=i.index_id
 JOIN sys.columns c ON c.object_id=ic.object_id AND c.column_id=ic.column_id
 WHERE c.name=N'HospitalId' AND (i.is_primary_key=1 OR i.is_unique_constraint=1)
) THROW 51104, 'HospitalId participates in a primary key or unique constraint; manual review required', 1;
IF EXISTS (
 SELECT 1 FROM sys.check_constraints cc JOIN sys.columns c ON c.object_id=cc.parent_object_id
 WHERE c.name=N'HospitalId' AND (cc.parent_column_id=c.column_id OR cc.definition LIKE N'%HospitalId%')
) THROW 51105, 'Check constraint involving HospitalId requires manual review', 1;
IF EXISTS (
 SELECT 1 FROM sys.sql_expression_dependencies d JOIN sys.columns c
 ON c.object_id=d.referenced_id AND c.column_id=d.referenced_minor_id
 WHERE c.name=N'HospitalId' AND d.is_schema_bound_reference=1
) THROW 51106, 'Schema-bound dependency on HospitalId requires manual review', 1;

-- Conservative dependency guards: refuse unsafe or ambiguous schema shapes.
-- A composite foreign key could encode a pharmacy-scoped relationship too.
IF EXISTS (
 SELECT 1 FROM sys.foreign_keys f
 WHERE (f.referenced_object_id=OBJECT_ID(N'dbo.Hospitals')
 OR EXISTS (SELECT 1 FROM sys.foreign_key_columns fc
 JOIN sys.columns c ON c.object_id=fc.parent_object_id AND c.column_id=fc.parent_column_id
 WHERE fc.constraint_object_id=f.object_id AND c.name=N'HospitalId'))
 AND (SELECT COUNT(*) FROM sys.foreign_key_columns fc WHERE fc.constraint_object_id=f.object_id)>1
) THROW 51107, 'Composite hospital foreign key requires manual review', 1;

-- Do not silently remove a composite index that may serve pharmacy lookups.
IF EXISTS (
 SELECT 1 FROM sys.indexes i
 WHERE i.name IS NOT NULL AND i.is_hypothetical=0
 AND EXISTS (SELECT 1 FROM sys.index_columns ic JOIN sys.columns c
 ON c.object_id=ic.object_id AND c.column_id=ic.column_id
 WHERE ic.object_id=i.object_id AND ic.index_id=i.index_id AND c.name=N'HospitalId')
 AND (SELECT COUNT(*) FROM sys.index_columns ic WHERE ic.object_id=i.object_id
 AND ic.index_id=i.index_id AND ic.key_ordinal>0)>1
) THROW 51108, 'Composite index containing HospitalId requires manual review', 1;

-- Exclude dbo.Hospitals because this empty legacy table
-- and its indexes will be removed by this migration.
-- Continue protecting filtered indexes on other tables.
IF EXISTS (
 SELECT 1
 FROM sys.indexes i
 JOIN sys.tables t ON t.object_id = i.object_id
 WHERE i.has_filter = 1
   AND i.filter_definition LIKE N'%HospitalId%'
   AND i.object_id <> OBJECT_ID(N'dbo.Hospitals')
)
THROW 51109, 'Filtered index references HospitalId; manual review required', 1;

-- Check SQL modules, including non-schema-bound views, functions, triggers and procedures.
-- Conservative text search can report false positives, but avoids deleting live dependencies.
IF EXISTS (
 SELECT 1 FROM sys.sql_modules m JOIN sys.objects o ON o.object_id=m.object_id
 WHERE (m.definition LIKE N'%HospitalId%' OR m.definition LIKE N'%Hospitals%')
 AND o.is_ms_shipped=0
) THROW 51110, 'SQL module references hospital schema; manual review required', 1;

-- Unreadable/encrypted SQL modules cannot be safely audited.
IF EXISTS (
 SELECT 1 FROM sys.sql_modules m JOIN sys.objects o ON o.object_id=m.object_id
 WHERE m.definition IS NULL AND o.is_ms_shipped=0
) THROW 51111, 'Encrypted SQL module requires manual review', 1;

-- Prevent accidental removal of non-nullable columns or other Hospitals-table inbound references.
IF EXISTS (SELECT 1 FROM sys.columns c JOIN sys.tables t ON t.object_id=c.object_id
 WHERE c.name=N'HospitalId' AND c.is_nullable=0)
 THROW 51112, 'Non-nullable HospitalId column requires manual review', 1;
IF OBJECT_ID(N'dbo.Hospitals',N'U') IS NOT NULL AND EXISTS (
 SELECT 1 FROM sys.foreign_keys f WHERE f.referenced_object_id=OBJECT_ID(N'dbo.Hospitals')
 AND NOT EXISTS (SELECT 1 FROM sys.foreign_key_columns fc JOIN sys.columns c
 ON c.object_id=fc.parent_object_id AND c.column_id=fc.parent_column_id
 WHERE fc.constraint_object_id=f.object_id AND c.name=N'HospitalId')
) THROW 51113, 'Hospitals referenced by non-HospitalId foreign key; manual review required', 1;

-- Remove all FKs depending on obsolete HospitalId columns or Hospitals table.
DECLARE @sql nvarchar(max) = N'';
SELECT @sql += N'ALTER TABLE ' + QUOTENAME(SCHEMA_NAME(t.schema_id)) + N'.' + QUOTENAME(t.name)
 + N' DROP CONSTRAINT ' + QUOTENAME(f.name) + N';' + CHAR(10)
FROM sys.foreign_keys f JOIN sys.tables t ON f.parent_object_id=t.object_id
WHERE f.referenced_object_id=OBJECT_ID(N'dbo.Hospitals')
 OR EXISTS (SELECT 1 FROM sys.foreign_key_columns fc JOIN sys.columns c
 ON fc.parent_object_id=c.object_id AND fc.parent_column_id=c.column_id
 WHERE fc.constraint_object_id=f.object_id AND c.name=N'HospitalId');
EXEC sys.sp_executesql @sql;

-- Drop indexes involving HospitalId before dropping the columns.
SET @sql=N'';
SELECT @sql += N'DROP INDEX ' + QUOTENAME(i.name) + N' ON '
 + QUOTENAME(SCHEMA_NAME(t.schema_id)) + N'.' + QUOTENAME(t.name) + N';' + CHAR(10)
FROM sys.indexes i JOIN sys.tables t ON i.object_id=t.object_id
WHERE i.is_primary_key=0 AND i.is_unique_constraint=0 AND i.name IS NOT NULL
AND EXISTS (SELECT 1 FROM sys.index_columns ic JOIN sys.columns c
 ON c.object_id=ic.object_id AND c.column_id=ic.column_id
 WHERE ic.object_id=i.object_id AND ic.index_id=i.index_id AND c.name=N'HospitalId');
EXEC sys.sp_executesql @sql;

-- SQL Server requires default constraints to be removed before DROP COLUMN.
SET @sql=N'';
SELECT @sql += N'ALTER TABLE ' + QUOTENAME(SCHEMA_NAME(t.schema_id)) + N'.' + QUOTENAME(t.name)
 + N' DROP CONSTRAINT ' + QUOTENAME(dc.name) + N';' + CHAR(10)
FROM sys.default_constraints dc JOIN sys.tables t ON t.object_id=dc.parent_object_id
JOIN sys.columns c ON c.object_id=dc.parent_object_id AND c.column_id=dc.parent_column_id
WHERE c.name=N'HospitalId';
EXEC sys.sp_executesql @sql;

-- Remove obsolete nullable HospitalId columns from all remaining tables.
SET @sql=N'';
SELECT @sql += N'ALTER TABLE ' + QUOTENAME(s.name) + N'.' + QUOTENAME(t.name)
 + N' DROP COLUMN [HospitalId];' + CHAR(10)
FROM sys.columns c JOIN sys.tables t ON c.object_id=t.object_id
JOIN sys.schemas s ON t.schema_id=s.schema_id WHERE c.name=N'HospitalId';
EXEC sys.sp_executesql @sql;
IF OBJECT_ID(N'dbo.Hospitals', N'U') IS NOT NULL DROP TABLE dbo.Hospitals;

GO

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20261109010000_ConsolidatedStandaloneHospitalCleanup', N'8.0.26');
GO

COMMIT;
GO

