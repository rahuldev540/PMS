-- SQL Server migration for existing installations. Back up your database first.
-- Internal Branch.Id remains unchanged and globally unique.
IF COL_LENGTH('dbo.Branches', 'BranchNumber') IS NULL
    ALTER TABLE dbo.Branches ADD BranchNumber int NULL;
GO
-- Assign main branch number 1 and remaining branch numbers 2..N per pharmacy.
;WITH numbered AS (
 SELECT Id, ROW_NUMBER() OVER(PARTITION BY PharmacyId ORDER BY CASE WHEN IsMainBranch = 1 THEN 0 ELSE 1 END, Id) AS n
 FROM dbo.Branches WHERE PharmacyId IS NOT NULL
)
UPDATE b SET BranchNumber = numbered.n FROM dbo.Branches b JOIN numbered ON numbered.Id = b.Id;
GO
IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE name='IX_Branches_PharmacyId_BranchNumber' AND object_id=OBJECT_ID('dbo.Branches'))
 CREATE UNIQUE INDEX IX_Branches_PharmacyId_BranchNumber ON dbo.Branches(PharmacyId, BranchNumber) WHERE PharmacyId IS NOT NULL AND BranchNumber IS NOT NULL;
GO
