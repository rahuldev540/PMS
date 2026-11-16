# Phase 5B30 - Multi-file schema-compatibility clarification and build checks

Based on user-confirmed successful Phase 5B29.

Changed comments in 7 C# model files to distinguish *legacy mapped database columns* from active CMS integration. Existing HospitalId properties were intentionally NOT removed: they still participate in EF model and migration history and removing them without an EF migration can break runtime queries.

Strengthened `scripts/check_standalone.py` with declaration and call-site checks, and added `scripts/Check-Standalone.cmd` so Windows users can run checks without changing PowerShell execution policy.

No endpoint, query, business logic, authorization, DTO, EF entity property, schema, or migration changed. This is NOT the full CMS removal. The actual removal needs an audited cross-file implementation plus a tested migration.

From project root:

    scripts\Check-Standalone.cmd

Or run:

    dotnet restore .\Pharmacy.sln
    dotnet build .\Pharmacy.sln
    dotnet run --project .\PharmacyOnly.Api.csproj

No migration required. ZIP and static checks passed; .NET SDK unavailable in packaging environment.
