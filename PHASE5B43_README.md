# Phase 5B43 — First actual CMS schema removal: AuditLogs.ClinicId

This phase removes only the unused `AuditLog.ClinicId` field and `AuditLog.Clinic` navigation from the active EF Core model and model snapshot. It includes an EF migration that drops the corresponding FK, index, and column. It does NOT remove other HospitalId fields or the Hospitals table. Existing audit branch relationships remain.

The Phase 5B42 results show 1 Super Admin without PharmacyId (expected), 5 Pharmacy Admins without PharmacyId (pending), and 0 user-branch mismatches or orphan branches. Do not assign pharmacy IDs to pending users merely to eliminate NULL values.

## Mandatory steps
1. Back up the Phase 5B42 project and **back up PMSDB10 in SSMS**. Confirm you can restore it.
2. Extract this ZIP to a fresh folder.
3. Run `dotnet restore .\Pharmacy.sln`, `dotnet build .\Pharmacy.sln`, `python .\scripts\check_standalone.py`. STOP if any fails.
4. Confirm the configured connection string targets PMSDB10, not another database.
5. In SSMS, confirm `SELECT COUNT(*) AS PopulatedAuditClinicIds FROM dbo.AuditLogs WHERE ClinicId IS NOT NULL;` returns 0.
6. **Apply the included migration**: `dotnet ef database update --context AppDbContext` (or `Update-Database -Context AppDbContext` in Package Manager Console). Do NOT run `dotnet ef migrations add`; migration already exists.
7. Verify in SSMS that AuditLogs no longer has ClinicId and audit records remain; test Super Admin audit endpoints and pharmacy/branch login.

Migration includes a safety guard: it aborts if any AuditLogs.ClinicId is populated. Historical migrations are intentionally unchanged. The migration is reversible but rollback cannot restore old ClinicId values, hence backup is mandatory.

.NET compilation and live migration could not be run in the authoring environment; run and report local results before proceeding.
