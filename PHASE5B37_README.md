# Phase 5B37 — Standalone schema-removal readiness inventory

Based on user-confirmed Phase 5B36. Added `--json` and `--active-only` switches to the read-only dependency scanner, plus automated regression tests of its output. No API, EF entity, database, authentication or permission behavior changed. This prepares a precise inventory for a later coordinated migration; it does NOT remove HospitalId columns.

## Test

```powershell
python .\scripts\check_standalone.py
python .\scripts\Report-LegacyHospitalDependencies.py --active-only
python .\scripts\Report-LegacyHospitalDependencies.py --json --active-only > active-legacy.json
dotnet restore .\Pharmacy.sln
dotnet build .\Pharmacy.sln
```

No migration for this phase. Do not edit historical migrations or drop HospitalId columns yet.
