# Phase 13 Stock Transfer – Pharmacy Admin Multi-Branch Fix

Base: `PMS_STANDALONE_PHASE13_DISPENSING_PHOTOS_RAZORPAY_ADDITIVE (1).zip`

## Scope (source-only, no data changes)

Only existing files changed:
- `Controllers/StockTransferController.cs`
- `DTOs/PharmacyModuleDtos.cs`

No old files deleted, no existing migration changed, no new table, and no database migration required for this fix. No data was accessed or edited. The original dispensing, billing and Razorpay project files are preserved.

## Why it failed

The existing Stock Transfer Create method required the FromLocation to equal the signed-in user's branch name. The same limitation appeared in List/Get/Draft/Approve/Dispatch/Receive. Pharmacy Admin has whole-pharmacy scope and might not even have a permanent BranchId claim.

## Implemented

- Keep role restriction `[Authorize(Roles = "PharmacyAdmin")]` and per-action `Stock Transfers` permission checks.
- Pharmacy Admin can create and process transfers between any **two different active branches in their own pharmacy**.
- New optional `fromBranchId` and `toBranchId` request parameters accept **pharmacy-scoped public branch numbers**. Internal Branch PKs remain in the transfer and inventory records.
- Legacy `fromLocation` / `toLocation` request bodies still work using exact case-insensitive names, or unambiguous standard `- Main Branch` / `- Sub Branch` suffix aliases. Ambiguous or invalid branch names are rejected; no fuzzy or cross-pharmacy matching.
- Source medicine, inventory and batch are searched in the selected **source** branch.
- Lists/GET and creation responses show public branch IDs; internal branch FKs stay in the database.
- Draft editing/deletion, status updates, dispatch and receipt are accessible to the authorized Pharmacy Admin across all their own branches.
- Dispatch reduces the selected transfer source branch's inventory and batch; receipt increases the selected destination branch's inventory and batch, using the existing transactions and status transitions.
- A guard checks that both stored transfer branch FKs actually belong to the Admin's pharmacy before any transfer mutation.
- Existing requested -> approved -> dispatched -> received flow remains unchanged, and stock is **not** deducted when a transfer is first requested.

## Swagger request example

`POST /api/inventory/transfers` — Authorize as PharmacyAdmin with Stock Transfers Create allowed.

```json
{
  "fromBranchId": 1,
  "toBranchId": 2,
  "medicineName": "Paracetamol 500mg",
  "batchNumber": "PCM2026A",
  "transferQuantity": 50,
  "requestedBy": "",
  "approvedBy": ""
}
```

These IDs are *public IDs in the signed-in Admin's pharmacy*, not the database PKs. Use a medicine and batch that actually exist at the selected source branch.

Legacy request without IDs still works:

```json
{
  "fromLocation": "Nanna - Main Branch",
  "toLocation": "Getha",
  "medicineName": "Paracetamol 500mg",
  "batchNumber": "PCM2026A",
  "transferQuantity": 50
}
```

A successful create returns **201 Created**, `status = Requested`, and public `fromBranchId` / `toBranchId`. No stock changes until the existing dispatch/receive steps.

## APIs (no new routes)

- `POST /api/inventory/transfers` – create
- `GET /api/inventory/transfers` – pharmacy-wide list for authorized Admin
- `GET /api/inventory/transfers/{id}` – pharmacy-wide details
- `PUT /api/inventory/transfers/{id}` – edit Requested draft
- `DELETE /api/inventory/transfers/{id}` – delete Requested draft only (existing behavior)
- `POST /api/inventory/transfers/{id}/status` – Approved or Cancelled
- `POST /api/inventory/transfers/{id}/dispatch` – source stock reduction
- `POST /api/inventory/transfers/{id}/receive` – destination stock receipt

### Manual smoke-test path

1. Back up your **code and database**, and extract ZIP to a new folder. Do not drop or recreate your existing database.
2. From project root:
   ```powershell
   dotnet restore .\Pharmacy.sln
   dotnet build .\Pharmacy.sln
   dotnet run --project .\PharmacyOnly.Api.csproj
   ```
3. Login as an active Pharmacy Admin with Stock Transfers View/Create/Edit permission enabled. A disabled permission must still return `403`.
4. Create transfer from Main Branch public `1` to Getha public `2` using real source medicine/batch. Expect `201`, status `Requested`.
5. List transfer. Expect `fromBranchId=1` and `toBranchId=2` (not internal FK values). Verify summary includes every branch in same pharmacy.
6. Approve using POST `/api/inventory/transfers/{id}/status` with `{ "status": "Approved" }`.
7. Dispatch using POST `/api/inventory/transfers/{id}/dispatch`. Confirm **source** branch stock/batch decreases once.
8. Receive using POST `/api/inventory/transfers/{id}/receive`. Confirm **destination** branch inventory increases once.
9. Verify cross-pharmacy and unknown branches are rejected, same source/destination is rejected; test an Admin from another pharmacy cannot see that transfer.
10. Verify an account with Stock Transfers disabled receives `403`, and a Pharmacist cannot initiate transfers.

## Verification/limits

Source-level checks and ZIP integrity were performed. A .NET SDK was not available in the execution environment, therefore this artifact has **not** passed `dotnet build`, a real SQL Server migration, or live transaction tests. No migration is required for the targeted code-only change.

Note: The pre-existing destination-receipt implementation uses the transferred **source medicine's internal MedicineId** in destination Inventory. If your UI requires a separate branch-owned medicine catalog row at the destination, that is an existing data-model concern requiring a separate design review. It was intentionally not changed in this targeted fix to protect existing data and business rules.
