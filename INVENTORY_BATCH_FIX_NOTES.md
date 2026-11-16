# Inventory batch response update

## Updated
- `GET /api/inventory?branchId=<pharmacy-scoped-id>` now loads `MedicineBatches` through `MedicineBatches.InventoryId = Inventories.Id` and returns all matching batches in `batches`.
- Existing `batchId`, `batchNumber`, `quantity`, `expiryDate` are populated with the earliest-expiring usable batch (or earliest batch when none is usable), for compatibility. For accurate batch-level operations, use the full `batches` collection.
- All batch queries are scoped through the already-filtered inventory IDs for the authenticated pharmacy and branch.
- No database migration is needed for this inventory response change.

## Already present in uploaded project
- Pharmacy-specific branch mapping via `PharmacyBranchIdentities` (`PharmacyId`, `BranchId`, `ExistingBranchId`). The `Branches.Id` column remains the global internal key used by other tables. This is why SSMS `Branches.Id` differs from the pharmacy-scoped Swagger `branchId`.
- `GET /api/pharmacy/revenue` is Admin-only and reports paid bill totals by branch.
- Pharmacist inventory endpoints already restrict to their token branch; the dedicated `GET /api/pharmacist/inventory/{medicineId}/batches` endpoint provides batch details.
- Pharmacy registration already returns an updated access token.

## Not changed
- Physical `Branches.Id` values were NOT renumbered. Renumbering would require a coordinated schema/data migration across foreign keys; changing them in isolation is unsafe.
- Pharmacist inventory listing response is unchanged; use its existing batch endpoint for batch-level details.

## Validation
- No dotnet SDK is installed in this environment, so compilation and integration tests could not be run. Build and test in Visual Studio before deployment.
- Keep a database backup before any future schema migration.
