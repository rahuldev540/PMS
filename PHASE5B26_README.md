# Phase 5B26 — Batched standalone audit-log cleanup

## Updated files
- Controllers/PharmacySuperAdminController.cs
- DTOs/PharmacyModuleDtos.cs

## Changes
- Removed the obsolete `hospitalId` query parameter from both Super Admin audit-log endpoints (`audit-logs` and `audit-logs/summary`).
- Removed hospital-based filtering of audit records from their shared query builder.
- Removed legacy CMS hospital existence validation from the shared audit filter validator.
- Branch validation now checks for a branch associated with a Pharmacy (`PharmacyId` present), rather than a Hospital.
- Preserved date, branch, activity type, role, action, search, online, pagination and loginOnly filters.
- Cleaned outdated HospitalId documentation in pharmacy DTOs.

## API compatibility note
The two audit-log endpoints no longer advertise or apply `hospitalId`. Remove that filter from the frontend. `branchId` remains an internal database branch identifier in these audit endpoints; do not confuse it with pharmacy-scoped public branch IDs.

## Not changed
- The separate legacy revenue report builder still contains HospitalId and CMS-specific queries. It is NOT yet converted.
- Database entities, columns, and migrations remain unchanged.
- No database migration is needed for this phase.

## Local checks
```
dotnet restore .\Pharmacy.sln
dotnet build .\Pharmacy.sln
dotnet run --project .\PharmacyOnly.Api.csproj
```
Test Super Admin audit logs list and summary with branchId/date/activity filters, and ensure no cross-pharmacy data is accidentally exposed. Compilation is not verified in the packaging environment.
