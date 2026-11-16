# Validation response update

`POST /api/medicine-import/validate` now returns `data.pharmacyId` and `data.branchId` (pharmacy-scoped branch ID) in addition to the existing fields. The branch ID is resolved from the existing internal branch FK via `PharmacyBranchIdentities`.

No migration is needed. Build and restart the API, then upload a file selecting branch 2 and verify the response. Existing imports, commit and inventory behavior are unchanged.

This ZIP has not been .NET build-tested in this environment.
