# Phase 5B42 — User tenant assignment audit

Phase 5B41 showed 6 of 18 users have NULL PharmacyId. This phase classifies them by role, checks for mismatched user/branch pharmacy assignments, and detects orphan branches. No application or database model changes are made.

1. Back up Phase 5B41 and your PMSDB10 database.
2. In Developer PowerShell: `dotnet restore .\Pharmacy.sln`, `dotnet build .\Pharmacy.sln`, `python .\scripts\check_standalone.py`.
3. In SSMS select PMSDB10 and open `scripts/Check-UserPharmacyAssignments.sql` with File > Open > File. Press F5.
4. Share the role summary, assignment review, and final mismatch summary (avoid exposing personal emails).

Do not assign PharmacyId to Super Admin accounts merely to eliminate NULL values. Admins may legitimately be pending pharmacy creation. The script is read-only and no EF migration is required.
