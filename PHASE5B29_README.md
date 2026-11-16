# Phase 5B29: Swagger wording and safer build guards

Based on Phase 5B28 (user-confirmed successful build).

Changes: correct 5 misleading legacy CMS/hospital and old import labels in Swagger operation summaries; improve PowerShell regression checks to require method declarations rather than mere call sites; add optional Python regression checker for systems blocking PowerShell scripts.

No routes, service logic, authorization, entity models, database schema, or migrations changed. Legacy CMS/HospitalId fields are still present and need coordinated migration later.

Build: `dotnet restore .\Pharmacy.sln` then `dotnet build .\Pharmacy.sln`.
Optional: `python .\scripts\check_standalone.py`.
No migration. Package integrity checked; .NET SDK not installed in packaging environment.
