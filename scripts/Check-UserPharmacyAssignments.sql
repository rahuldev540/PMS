-- Phase 5B42: READ ONLY. Classify NULL PharmacyId users before schema changes.
-- Select PMSDB10 in SSMS. Do not assign Super Admin users to pharmacies.
SET NOCOUNT ON;
SELECT DB_NAME() AS DatabaseName, SYSDATETIME() AS CheckedAt;

-- Aggregate by role and assignment status. NULL roles are reported explicitly.
SELECT COALESCE(NULLIF(LTRIM(RTRIM([Role])), ''), '(NULL/EMPTY)') AS UserRole,
       COUNT_BIG(*) AS TotalUsers,
       SUM(CASE WHEN PharmacyId IS NULL THEN 1 ELSE 0 END) AS MissingPharmacyId,
       SUM(CASE WHEN PharmacyId IS NOT NULL THEN 1 ELSE 0 END) AS AssignedPharmacyId,
       SUM(CASE WHEN BranchId IS NOT NULL THEN 1 ELSE 0 END) AS AssignedBranchId
FROM dbo.Users
GROUP BY COALESCE(NULLIF(LTRIM(RTRIM([Role])), ''), '(NULL/EMPTY)')
ORDER BY UserRole;

-- Show identifiers and assignment state only, not emails or credentials.
SELECT Id AS InternalUserId, [Role] AS UserRole, PharmacyId, BranchId, IsActive, AccountStatus,
       CASE WHEN PharmacyId IS NULL AND [Role] IN ('SuperAdmin','Super Admin','PharmacySuperAdmin')
                 THEN 'EXPECTED FOR SUPER ADMIN - VERIFY ROLE NAMING'
            WHEN PharmacyId IS NULL AND [Role] IN ('Admin','PharmacyAdmin')
                 THEN 'CHECK: ADMIN MAY BE AWAITING PHARMACY CREATION'
            WHEN PharmacyId IS NULL AND [Role] IN ('Pharmacist')
                 THEN 'REVIEW: PHARMACIST MISSING PHARMACY'
            WHEN PharmacyId IS NULL THEN 'REVIEW ROLE / ASSIGNMENT'
            ELSE 'ASSIGNED' END AS AssignmentReview
FROM dbo.Users
WHERE PharmacyId IS NULL OR (BranchId IS NOT NULL AND PharmacyId IS NULL)
ORDER BY [Role], Id;

-- Cross-pharmacy branch mismatch, including dangling branch references.
SELECT u.Id AS InternalUserId, u.[Role] AS UserRole,
       u.PharmacyId AS UserPharmacyId, u.BranchId AS UserBranchId,
       b.PharmacyId AS BranchPharmacyId,
       CASE WHEN b.Id IS NULL THEN 'BRANCH RECORD NOT FOUND'
            WHEN u.PharmacyId IS NULL THEN 'USER PHARMACY NOT SET'
            ELSE 'CROSS-PHARMACY BRANCH ASSIGNMENT' END AS Problem
FROM dbo.Users u
LEFT JOIN dbo.Branches b ON b.Id = u.BranchId
WHERE u.BranchId IS NOT NULL
  AND (b.Id IS NULL OR u.PharmacyId IS NULL OR b.PharmacyId IS NULL OR u.PharmacyId <> b.PharmacyId)
ORDER BY u.Id;

-- Branches missing or pointing to nonexistent pharmacies.
SELECT b.Id AS InternalBranchId, b.PharmacyId, b.IsMainBranch,
       CASE WHEN b.PharmacyId IS NULL THEN 'BRANCH PHARMACY NULL'
            ELSE 'PHARMACY NOT FOUND' END AS Problem
FROM dbo.Branches b
LEFT JOIN dbo.Pharmacies p ON p.Id = b.PharmacyId
WHERE b.PharmacyId IS NULL OR p.Id IS NULL
ORDER BY b.Id;

-- Read-only summary of the above structural checks.
SELECT
 (SELECT COUNT_BIG(*) FROM dbo.Users WHERE PharmacyId IS NULL) AS UsersWithoutPharmacy,
 (SELECT COUNT_BIG(*) FROM dbo.Users u LEFT JOIN dbo.Branches b ON b.Id=u.BranchId
  WHERE u.BranchId IS NOT NULL AND (b.Id IS NULL OR u.PharmacyId IS NULL OR b.PharmacyId IS NULL OR u.PharmacyId<>b.PharmacyId)) AS UserBranchMismatches,
 (SELECT COUNT_BIG(*) FROM dbo.Branches b LEFT JOIN dbo.Pharmacies p ON p.Id=b.PharmacyId
  WHERE b.PharmacyId IS NULL OR p.Id IS NULL) AS OrphanBranches;
