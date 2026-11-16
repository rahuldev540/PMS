# Phase 5B36 — Remove orphan CMS Swagger metadata

Based on user-confirmed Phase 5B35. Removed the obsolete `PharmacySuperAdmin.Hospitals` Swagger title entry (there is no matching action in active Super Admin controller), updated two outdated comments in audit query logic, and added regression checks to ensure that stale metadata does not return. Existing Swagger labels, endpoints, EF models, migrations and runtime logic are unchanged.

## Test

```powershell
python .\scripts\check_standalone.py
dotnet restore .\Pharmacy.sln
dotnet build .\Pharmacy.sln
```

No database migration needed. This is a small safe cleanup, **not** the complete removal of hospital schema.
