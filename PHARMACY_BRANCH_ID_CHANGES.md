# Pharmacy-scoped database BranchId (CMS unchanged)

New SQL table `PharmacyBranchIdentities` uses composite primary key `(PharmacyId, BranchId)`.
The same BranchId is used by standalone pharmacy branch APIs; each pharmacy starts at 1.
Existing `Branches.Id` remains a global primary key and legacy foreign key for CMS and existing transaction tables.
This is a compatibility design, **not** a conversion of `Branches.Id` itself into a repeated number.

Changes:
- Pharmacy registration inserts main-branch identity 1 in the existing transaction.
- Branch creation inserts the next identity in the existing serializable transaction.
- PharmacyController branch list/get/create/update/status/delete use pharmacy-scoped IDs for standalone pharmacies.
- CMS-integrated pharmacy branch API continues using original IDs.
- Branch deletion checks existing dependent tables using the legacy internal key and removes the identity.
- New migration backfills existing standalone branch identities.

## Apply
Back up your SQL database. In Visual Studio Package Manager Console, choose the correct startup and default project, then run:

    Get-Migration
    Update-Database

Do not Drop-Database. After applying, verify:

    SELECT PharmacyId, BranchId, ExistingBranchId
    FROM dbo.PharmacyBranchIdentities ORDER BY PharmacyId, BranchId;

## Important remaining work
Other controllers (including pharmacist assignment and Super Admin GET admin by ID) still expose/accept legacy Branch.Id and need coordinated mapping to the pharmacy-scoped ID. This ZIP is a partial implementation and has not been compiled or integration-tested in this environment. Do not deploy as complete or production-ready.
