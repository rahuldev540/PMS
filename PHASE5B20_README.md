# Phase 5B20 — Disable legacy hospital notification recipient

Base: Phase 5B19.

Changed Controllers/PharmacySuperAdminController.cs: removed the Hospital option from notification RecipientType validation and removed the Hospital-specific recipient lookup/validation block. Valid types are All, Branch, User. Hospital requests now return HTTP 400.

Remaining notification logic and HospitalId schema fields are unchanged. This is **not** full standalone conversion; `All` and other paths still need auditing for hospital dependencies. Frontend must stop offering Hospital as a recipient type.

No database migration required. Run dotnet restore .\Pharmacy.sln, dotnet build .\Pharmacy.sln, then dotnet run --project .\PharmacyOnly.Api.csproj. Test notification creation for All, Branch, User and rejection of Hospital. .NET compilation unavailable in packaging environment.
