# Phase 5B1 — Standalone user/permission creation cleanup

Based on Phase 5A4. Removed explicit `HospitalId = null` object initializer entries from three C# files when creating new users and permissions. Nullable HospitalId remains in the entity schema for backward compatibility; its default is already null. Existing-user hospital clearing assignments remain unchanged. No tenant filtering was rewritten in this increment.

No EF entity or mapping changes, so no migration is required.

Build: `dotnet restore .\Pharmacy.sln` then `dotnet build .\Pharmacy.sln`. Test creation of Pharmacy Admin and Pharmacist and default permissions.

Remaining CMS/HospitalId dependencies still require later phases. .NET SDK unavailable in creation environment; build unverified.
