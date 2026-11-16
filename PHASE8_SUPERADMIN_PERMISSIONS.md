# Phase 8: Super Admin permission enforcement and Swagger cleanup

Removed five actions from `PharmacySuperAdminController` (not merely hidden from Swagger):

- PUT /api/pharmacy-super-admin/branches/{id}
- PATCH /api/pharmacy-super-admin/branches/{id}/status
- DELETE /api/pharmacy-super-admin/branches/{id}
- GET /api/pharmacy-super-admin/medicines (included medicine prices)
- PATCH /api/pharmacy-super-admin/medicines/{id}/status

These routes no longer appear in Swagger and cannot be invoked through HTTP. Branch administration and medicine management remain the Pharmacy Admin's responsibility. Super Admin retains read-only branch listing and details, pharmacy overview, pharmacy-wise revenue, dashboard, and admin management.

No database migration is required. Run `dotnet build .\\Pharmacy.sln` and test Swagger; removed routes should return HTTP 404. Existing Swagger JSON should be refreshed after restarting the application.

Note: Other controllers, including any alternative routes to the same functionality, require separate end-to-end authorization testing. No build or database integration tests were executed in this environment.
