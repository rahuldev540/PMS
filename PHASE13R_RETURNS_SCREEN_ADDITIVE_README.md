# Phase 13R — Patient Medicine Returns screens (additive)

Based on `PMS_STANDALONE_PHASE13_STOCK_TRANSFER_MULTIBRANCH_FIX(1).zip`.

## Source protection

- Retains every original ZIP entry. No existing data was accessed, deleted, reset or migrated during packaging.
- Retains all five original `ReturnsController` methods, their signatures and business logic.
- Only existing files extended are `Data/AppDbContext.cs` and `Migrations/AppDbContextModelSnapshot.cs`, to register additive entities. No existing columns or records are changed.
- New files: `Models/PharmacyPatientReturnRequest.cs`, `Controllers/PharmacyPatientReturnWorkflowController.cs`, `Migrations/20261112173000_AddPharmacyPatientReturnRequests.cs`, and this README.
- **Migration required**: adds `PharmacyPatientReturnRequests` and `PharmacyPatientReturnRequestLines` only. Back up SQL Server before updating; review SQL migration script first.

## Existing APIs (UNCHANGED)

| Method | Endpoint | Use |
|---|---|---|
| POST | `/api/pharmacy/returns` | Existing single-medicine patient/supplier return |
| GET | `/api/pharmacy/returns` | Existing return list (pharmacy/branch filtered) |
| GET | `/api/pharmacy/returns/{id}` | Existing single return detail |
| POST | `/api/pharmacy/returns/{id}/approve` | Existing single return approval / stock movement |
| POST | `/api/pharmacy/returns/{id}/cancel` | Existing single-return cancellation (Admin) |

## Eight NEW APIs

| Method | Endpoint | Purpose |
|---|---|---|
| GET | `/api/pharmacy/returns/options` | Return reasons, conditions, restock rules |
| GET | `/api/pharmacy/returns/bills/{billId}/items?branchId=1&q=` | Resolve original bill, patient and returnable medicines/batches |
| GET | `/api/pharmacy/returns/eligible-medicines?billId=24&branchId=1&q=para` | Bill-restricted medicine search for autocomplete (same data contract) |
| POST | `/api/pharmacy/returns/patient` | Submit one or more medicine returns atomically as Pending |
| GET | `/api/pharmacy/returns/history?branchId=1&status=&q=&page=1&pageSize=20` | Paginated Return History including old/legacy patient returns |
| GET | `/api/pharmacy/returns/history/{requestId}` | New multi-medicine request detail and line status |
| POST | `/api/pharmacy/returns/history/{requestId}/approve` | Admin approval of pending group; restocks eligible batches in a transaction |
| POST | `/api/pharmacy/returns/history/{requestId}/cancel` | Admin cancellation of pending group, no stock movement |

### Identity and authorization

- Pharmacist works only in their assigned branch; JWT and stored User assignment must match.
- Pharmacy Admin must send **public** `branchId` for create and bill-item lookup; history can omit it to see pharmacy-wide entries.
- Backend converts public Branch ID through `PharmacyBranchIdentities` and never accepts a foreign pharmacy's branch.
- Requires `Returns.CanView`, `.CanCreate`, `.CanEdit` or `.CanDelete` accordingly. Editing group approve and cancel require PharmacyAdmin in addition to permission.
- If frontend says `Access denied for this pharmacist account`, Super Admin/Pharmacy Admin must inspect that pharmacist's `Returns` permissions and active pharmacy/branch assignment. **Do not bypass 403.** New routes cannot override a disabled permission.

### Example for pharmacist POST `/api/pharmacy/returns/patient`

```json
{
  "billId": 24,
  "patientName": "Ramesh",
  "reason": "Excess quantity",
  "notes": "Sealed, inspected at counter",
  "verifiedForRestocking": true,
  "medicines": [
    { "medicineId": 10, "originalBatch": "PCM2026A", "quantity": 5, "condition": "EligibleForRestocking" },
    { "medicineId": 11, "originalBatch": "B123", "quantity": 1, "condition": "Damaged" }
  ]
}
```

For Admin, add `"branchId": 1` (public pharmacy-scoped ID). Medicine IDs and bill IDs must come from the existing authenticated bill/medicine APIs; no hand-built conversion in frontend.

Example response (`201`):

```json
{ "success": true,
  "message": "Return request submitted for approval; stock is unchanged.",
  "data": { "requestId": 101, "billId": 24, "status": "Pending",
    "totalMedicines": 2, "totalQuantity": 6, "refundApplied": false } }
```

For status or history:

```json
{ "success": true, "data": { "items": [
  { "returnId": 50, "requestId": 101, "billId": 24,
    "patient": "Ramesh", "medicine": "Paracetamol 500mg",
    "quantity": 5, "status": "Pending", "branchId": 1 }
], "total": 1, "page": 1, "pageSize": 20, "hasMore": false } }
```

For old returns, `requestId` is null; use existing `GET /api/pharmacy/returns/{returnId}` to open those rows, and their existing approve/cancel APIs.

### Field coverage from screenshots

| Frontend field | API field / storage |
|---|---|
| Patient / Customer Name | `patientName` on submit; `patient` from original bill or request in history |
| Original Bill ID | `billId` (existing bill) |
| Reason for Return | `reason` + `GET /options` |
| Medicine Name and autocomplete | `medicineName` / `medicineId` from bill search |
| Original Batch | `originalBatch` on submit; `originalBatchOptions[].batchNumber` from bill lookup |
| Returning Quantity | `quantity`; validated against sold less earlier non-cancelled returns |
| Condition | `condition` (EligibleForRestocking, Damaged, Expired, Opened, Quarantine) |
| Restock verification checkbox | `verifiedForRestocking` |
| Notes | `notes` on group header |
| Return ID | `returnId` (existing row ID) and `requestId` (new group ID) |
| Bill ID | `billId` |
| Status | `status`: Pending / Approved / Cancelled |
| Search and pagination | `q`, `page`, `pageSize`, `total`, `hasMore` |
| Server-side restocking | Admin `POST /history/{requestId}/approve` |

### Important stock and refund rules

- POST patient return only creates pending records. **It does not adjust stock or issue cash refunds**. Relabel the frontend button to `Submit Return for Approval` or show a pending state after submission.
- Admin group approval updates `Inventories.CurrentStock`, `Inventories.AvailableStock`, `MedicineBatches.RemainingQuantity`, and inserts `StockTransactions` **only** for restock-eligible medicines. Damaged/expired/opened/quarantined items do not increase sellable stock.
- Expired, recalled or quarantined batches are rejected for restocking. Sold quantity is validated against existing returns including pending requests, to prevent double returns.
- The existing BillItem model does not store an original batch ID; a user-supplied batch is validated against the current branch inventory but cannot be independently proven to be the batch originally dispensed. Require manual batch check until dispensing records preserve original batch references.
- Financial refund is intentionally left to the existing billing/payment workflow. `RefundAmount` stays zero on these new return records to avoid double refunds.
- Original legacy `POST /api/pharmacy/returns/{id}/approve` continues to work as before, including its previously configured role permissions. New group approval is Pharmacy Admin-only.

## Deployment (Visual Studio 2022 PowerShell)

1. Back up project + SQL Server database. Extract ZIP into a fresh project folder (no nested root folder).
2. `dotnet restore .\Pharmacy.sln`
3. `dotnet build .\Pharmacy.sln`
4. `dotnet tool restore`
5. `dotnet ef migrations script --context AppDbContext -o phase13r_review.sql` (review SQL; may include any pending earlier migrations)
6. `dotnet ef database update --context AppDbContext`
7. `dotnet run --project .\PharmacyOnly.Api.csproj`
8. In Swagger: login as Pharmacist, `GET /api/pharmacy/returns/options`, GET bill items, POST a return, GET history; login as Admin with Returns Edit and approve request. Verify both branch stock before/after.

Build and live SQL tests have not been run in this environment. ZIP and source checks are not substitutes for runtime tests.
