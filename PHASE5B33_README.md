# Phase 5B33 — Remove obsolete permission service hospital arguments

Based on user-confirmed successful Phase 5B32.

- Removed unused `int? hospitalId` parameters from `UserPermissionService.CreateDefaultPermissionsAsync` and `EnsureDefaultPermissionsAsync`.
- Updated the internal forwarding call to match the new signature.
- Verified no other C# files in this source tree call these methods (before change).
- Strengthened source regression checks to ensure hospital arguments do not return.
- Preserved actual permission creation, pharmacy assignment validation, role modules, audit logs, all HTTP routes and database models.
- No schema changes and no EF migration required.

Run `dotnet restore .\Pharmacy.sln` and `dotnet build .\Pharmacy.sln` in Visual Studio Developer PowerShell.
The ZIP is integrity-checked and the Python source regression script passes. Compilation cannot be run in this environment (no dotnet SDK).
