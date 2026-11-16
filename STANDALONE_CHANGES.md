# Standalone Pharmacy Management System

This distribution removes the public CMS integration controllers and CMS configuration endpoints.

Removed routes:
- `/api/integration/*` (external hospital, branch, medicine and prescription sync)
- `PUT /api/pharmacy/settings/cms-integration`
- `GET /api/pharmacy/settings/cms-options`

The `Integration` API key configuration is removed. Standalone prescriptions are no longer blocked by CMS mode.

**Database compatibility:** Existing CMS-related columns, historical EF migrations, internal models, and some legacy permission labels remain so the existing database schema and EF migrations do not break. This is a runtime/API decoupling, **not** a complete database/schema purge. If existing pharmacies have `IsCmsIntegrated=1`, migrate their data to standalone mode before using production workflows.

**Verification:** No dotnet SDK is installed in this execution environment; compile and integration tests have not been run. In Visual Studio: restore NuGet packages, build solution, run API, inspect Swagger and test login, pharmacy/branch creation, import, prescription and billing.
