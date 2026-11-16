Phase 5B48 - Remove Users.HospitalId (standalone PMS)

This phase removes the nullable HospitalId and Hospital navigation from User, with a guarded EF migration. Other CMS/Hospital columns still remain for later phases. Historical migrations remain intentionally intact.

BEFORE MIGRATION: Back up your database. Run in SSMS:
SELECT COUNT(*) AS LinkedUsers FROM dbo.Users WHERE HospitalId IS NOT NULL;
Must be zero. If not zero, stop and review; do not overwrite or erase data blindly.

Run from extracted project root:
dotnet restore .\Pharmacy.sln
dotnet build .\Pharmacy.sln
Unblock-File .\.config\dotnet-tools.json
dotnet tool restore
dotnet ef database update --context AppDbContext

Expected migration: 20261107004500_RemoveUserHospitalColumn
Note: ZIP structural checks passed. dotnet compiler and live SQL Server were unavailable in generation environment.
