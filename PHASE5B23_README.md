# Phase 5B23 — Branch response DTO cleanup

Removed legacy `HospitalId` from `PharmacyBranchResponseDto` in `DTOs/PharmacyModuleDtos.cs`. Search of project C# source showed no constructor or other references to this DTO outside its declaration, so this is a narrow contract cleanup. Other hospital-based reporting and database model dependencies remain unchanged.

No database migration required.

After extraction:

```powershell
dotnet restore .\Pharmacy.sln
dotnet build .\Pharmacy.sln
dotnet run --project .\PharmacyOnly.Api.csproj
```

Verify branch listing and branch create/update responses in Swagger. The ZIP integrity was checked, but .NET compilation was not available in the packaging environment.
