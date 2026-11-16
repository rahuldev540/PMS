# Phase 5B31 — Remove hospital argument from live Super Admin audit helper

Based on user-confirmed successful 5B30. This phase removes the obsolete hospitalId argument from the active AddAudit helper and updates all eight four-argument call sites to named branchId arguments. Legacy nullable AuditLog.ClinicId is still assigned null for database compatibility; no schema change. Active audit filters, pharmacy revenue endpoints, branch isolation and existing business logic are preserved. Added a regression assertion for the helper signature. This is a targeted functional cleanup, not complete HospitalId removal.

From the project root run `scripts\Check-Standalone.cmd` or `dotnet restore .\Pharmacy.sln` followed by `dotnet build .\Pharmacy.sln`. No migration required. .NET compilation unavailable in packaging environment.
