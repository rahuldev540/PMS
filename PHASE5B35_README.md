# Phase 5B35 - Hospital/CMS dependency inventory and audit model compatibility guard

Built from user-confirmed successful Phase 5B34. This phase does **not** drop HospitalId columns or change EF mappings. It documents the AuditLog legacy FK as a temporary compatibility bridge, adds a detailed read-only inventory of remaining Hospital/CMS source dependencies (separates active code from historical migrations), and adds a guard for AuditLog mapping properties so later edits do not silently break the EF model.

## Windows commands

```powershell
python .\scripts\Report-LegacyHospitalDependencies.py
.\scripts\Check-Standalone.cmd
```

Or run `dotnet restore .\Pharmacy.sln` and `dotnet build .\Pharmacy.sln`.

No migration required. No API route or runtime business logic changed. This is preparatory work for a coordinated EF schema migration, **not** completion of standalone conversion. The ZIP and Python source guard were checked here, but the .NET SDK was unavailable for compilation.
