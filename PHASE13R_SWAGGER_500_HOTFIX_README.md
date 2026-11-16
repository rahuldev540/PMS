# Phase 13R — Swagger 500 hotfix (additive, no database change)

## Root cause
`GET /swagger/v1/swagger.json` may return HTTP 500 with Swashbuckle `SwaggerGeneratorException` because three image-upload actions declare `[FromForm] IFormFile file`. ASP.NET Core can infer form binding for a plain `IFormFile file`; the explicit `[FromForm]` attribute triggers unsupported Swagger parameter inspection in the affected Swashbuckle version.

## Exact modification
Only `Controllers/PharmacyEvidenceUploadController.cs` changed. Removed `[FromForm]` before `IFormFile file` in:
- `POST /api/pharmacy/dispensing/drafts/{draftId}/prescription-photo`
- `POST /api/pharmacy/prescriptions/{prescriptionId}/photo`
- `POST /api/pharmacy/bills/{billId}/receipt-photo`

Existing `[Consumes("multipart/form-data")]`, endpoints, authorization, upload validation, SQL image storage, old and new Returns APIs, migration files and stock-transfer behavior remain intact. All original archive files are retained, except for the targeted change in that controller. No migrations needed for this hotfix (but the additive Phase 13R Returns migration still applies if not yet run).

## Test instructions
1. Back up your project and database. Extract into a clean separate folder.
2. Stop the API; run `dotnet restore .\Pharmacy.sln` then `dotnet build .\Pharmacy.sln`.
3. Run `dotnet run --project .\PharmacyOnly.Api.csproj`.
4. Open local `https://localhost:56200/swagger/v1/swagger.json` (use the port from your console). It should return an OpenAPI JSON document, then open `/swagger/index.html`.
5. Confirm the three upload endpoints offer `multipart/form-data` with a file input.
6. If still 500, inspect the backend terminal for full `SwaggerGeneratorException` and inner exception; no local ASP.NET runtime was available in this environment.

No .NET build, SQL Server, or live Swagger run has been performed here.
