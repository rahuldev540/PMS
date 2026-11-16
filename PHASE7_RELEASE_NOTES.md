# Phase 7 — Purchase order correctness and admin input safety

Based on Phase 6 standalone PMS; retains the existing database migrations, API routes, tenant scoping, and all other source files.

## Fixes

1. PurchaseOrderController: always explicitly loads purchase-order items before validating status transitions. Previously the collection was initialized to an empty list, so a `null` check never triggered loading. This could allow cancelling or resetting an order after goods had been received, or closing one with outstanding quantities. No new endpoint or schema change.
2. PharmacySuperAdminController: returns HTTP 400 for missing/blank admin name or email on update rather than throwing a NullReferenceException (HTTP 500). Existing uniqueness checks remain.

## Validation

- Run `dotnet restore .\\Pharmacy.sln` and `dotnet build .\\Pharmacy.sln`.
- Verify an order with received quantities cannot be cancelled or reset, and an order with outstanding quantities cannot be closed.
- Verify an admin update without name/email returns HTTP 400; valid updates still work.
- No new EF migration required.

This is a focused Phase 7 patch. It does not claim to fix all 37 existing nullable warnings or implement the broader inventory, billing, and dashboard enhancements. A full .NET build and integration tests against SQL Server were not available in the packaging environment.
