# Standalone PMS — Phase 6 security and account-access update

Changes from the user-supplied Phase 5B52C project:

1. Pharmacy Admin may access their own assignment-status (GET), change-password (POST), and logout (POST) routes when their pharmacy is inactive. The pharmacy-status recovery route remains accessible. Operational routes remain blocked for inactive pharmacies; pharmacist access remains restricted.
2. SQL Server command timeout defaults to 180 seconds and is configurable with `Database:CommandTimeoutSeconds`.
3. EF Core sensitive-data logging is disabled by default, including in Development. To enable it for a local troubleshooting session only, set `Database:EnableSensitiveDataLogging=true`. Detailed errors remain enabled in Development.
4. Existing migration history and schema remain unchanged. No new migration required.

## Build and run (Visual Studio Developer PowerShell)

```powershell
Unblock-File .\.config\dotnet-tools.json
dotnet tool restore
dotnet restore .\Pharmacy.sln
dotnet build .\Pharmacy.sln
dotnet run --project .\PharmacyOnly.Api.csproj
```

The database migration was already applied to PMSDB10. Do NOT re-create the database or add a new migration for these changes. Verify existing connection strings and secrets before running.

## Smoke tests

- Super Admin and Pharmacy Admin login
- Admin account endpoints when pharmacy inactive (authenticated Admin only)
- Pharmacist denied operational requests when pharmacy inactive
- Pharmacy branch isolation, medicine upload, inventory, prescriptions and billing regression tests

Note: Archive packaging and source changes were verified, but .NET compilation and live SQL tests could not be run in this environment.
