# Phase 13 – Additive dispensing wizard, image uploads, branch Razorpay payments

## Source and preservation

Based strictly on the supplied `PMS_STANDALONE_PHASE12D_NOTIFICATION_APIS(3).zip` and the two dispensing UI screenshots, with the previously supplied `Payment_Gateway_Documentation 1.pdf` adapted from IBMS invoice/tenant terminology to standalone PMS bill/branch terminology.

Existing source files, controllers, migrations and records were retained. The only existing source files extended are `Data/AppDbContext.cs`, `Migrations/AppDbContextModelSnapshot.cs` and `Program.cs` for DI and model mapping. New code lives in separate controllers, services, models and one **additive** migration. No old table or column is dropped, no existing data is updated by the migration, and no existing API route is replaced.

**Build and live SQL/Razorpay tests could not be executed in the packaging environment.** Compile and test in an isolated database before deploying. Payment code is a starting implementation, not a PCI/security certification or a production release approval.

## Dispensing wizard (Admin/Pharmacist)

| Method | Path | What it adds |
|---|---|---|
| GET | `/api/pharmacy/dispensing/patients?branchId=2&search=raj&page=1&pageSize=20` | Patient picker using public pharmacy-scoped patient IDs |
| GET | `/api/pharmacy/dispensing/medicines?branchId=2&search=cetri` | Branch medicines, strength, selling price, stock |
| POST | `/api/pharmacy/dispensing/drafts` | Save step 1 or step 2 draft |
| PUT | `/api/pharmacy/dispensing/drafts/{id}` | Save updated patient and medicine data |
| GET | `/api/pharmacy/dispensing/drafts?branchId=2` | Saved drafts for current user |
| GET | `/api/pharmacy/dispensing/drafts/{id}` | Draft with medicine rows and photo metadata |
| POST | `/api/pharmacy/dispensing/drafts/{id}/submit` | Submit as an existing-model prescription without dispensing or billing automatically |
| GET | `/api/pharmacy/dispensing/prescriptions/{id}/details` | Prescription fields and 1/2/3 per-dose units |

Example POST draft JSON (Admin sends public `branchId`; Pharmacist can omit it):

```json
{
  "branchId": 2,
  "patientId": null,
  "patientName": "Rajesh",
  "phoneNumber": "6302427033",
  "age": 20,
  "gender": "Male",
  "doctorName": "Raj",
  "prescriptionDate": "2026-11-12T00:00:00Z",
  "notes": "",
  "medicines": [
    { "medicineId": 101, "morning": 1, "afternoon": 2, "evening": 3, "durationDays": 1, "quantity": 6 }
  ]
}
```

Replace `medicineId:101` with an actual internal medicine ID returned by the medicine picker. `patientId` is the **public patient ID**, and `branchId` is the **public branch number**. For an existing patient, verify that `patientName` matches the selected ID. Price and line amount are calculated by the backend using branch medicine selling price. These are estimates; GST/discount/final bill remain in the existing billing flow. `medicines: []` is allowed when saving step 1; submit requires at least one medicine. Quantity may be manually adjusted. Dose-unit details are stored in a new companion table without modifying existing `PrescriptionItems`.

`POST /api/pharmacy/dispensing/drafts/{id}/submit` creates patient/doctor/appointment/prescription records using the existing standalone models. Then continue with **existing** `POST /api/pharmacy/dispense` and `POST /api/pharmacy/generate-bill` as appropriate to your dispensing flow. Submitting a prescription does not deduct stock or create a bill by itself. Do not pay a draft; generate a bill first.

## Uploaded prescription and receipt images (JPG, PNG, WebP, max 10 MB)

| Method | Path | Use |
|---|---|---|
| POST | `/api/pharmacy/dispensing/drafts/{draftId}/prescription-photo` | Camera/upload image from Step 1 |
| POST | `/api/pharmacy/prescriptions/{prescriptionId}/photo` | Attach photo to existing prescription |
| POST | `/api/pharmacy/bills/{billId}/receipt-photo` | Attach bank/cash/payment receipt image |
| GET | `/api/pharmacy/evidence/{evidenceId}` | Authenticated image download |
| GET | `/api/pharmacy/evidence?draftId=1` | Image metadata (also `prescriptionId` or `billId`) |

All uploads use `multipart/form-data` with field name **`file`**. The frontend's `Take Photo` button must capture a Blob/File and submit it as `FormData` after saving a draft; `Upload Image` does the same with a selected file. Do not send base64 inside JSON. The API validates file size and image signatures, stores binary image data in a **new SQL Server table**, and returns an authenticated image URL. The frontend must fetch the URL with the bearer token; the image URL is not publicly accessible. Photo bytes are not exposed in normal list responses. Uploaded images are preserved when a draft is submitted and linked to the final prescription.

## Branch merchant settings (Admin only)

- `GET /api/payment-settings/branches`
- `GET /api/payment-settings/branches/{publicBranchId}`
- `PUT /api/payment-settings/branches/{publicBranchId}`

Example PUT:

```json
{
  "merchantName": "Getha Pharmacy",
  "environment": "Test",
  "razorpayKeyId": "rzp_test_example",
  "razorpayKeySecret": "YOUR_TEST_SECRET",
  "razorpayWebhookSecret": "YOUR_WEBHOOK_SECRET",
  "cash": true,
  "upi": true,
  "card": true,
  "bankTransfer": true,
  "allowPartialPayments": true
}
```

`razorpayKeySecret` and `razorpayWebhookSecret` are write-only. Use an application secret store, **not** source-controlled appsettings, for the encryption key. In Windows PowerShell:

```powershell
$key = [Convert]::ToBase64String([Security.Cryptography.RandomNumberGenerator]::GetBytes(32))
dotnet user-secrets set "PaymentGateway:CredentialEncryptionKey" "$key" --project .\PharmacyOnly.Api.csproj
```

Keep the key backed up securely: losing it makes existing encrypted gateway credentials unusable. Key rotation requires a planned re-encryption procedure. For Razorpay test and live environments use matching credentials; **test mode is strongly recommended first**. Register the returned webhook route with that branch's Razorpay merchant and configure its independent webhook signing secret.

## Payment APIs

| Method | Endpoint | Notes |
|---|---|---|
| POST | `/api/payments/gateway/create-order` | Razorpay order; `method: upi|card`, bill ID, amount, idempotency key |
| POST | `/api/payments/gateway/create-qr` | Single-use fixed-amount UPI QR; merchant eligibility required |
| POST | `/api/payments/gateway/verify` | HMAC signature + Razorpay payment fetch; credit only if captured |
| POST | `/api/payments/gateway/webhook/{merchantKey}` | Anonymous route but HMAC-verified using merchant-specific webhook secret |
| GET | `/api/payments/gateway/attempts/{id}/status` | Authenticated polling of payment status |
| GET | `/api/payments/bills/{billId}/status` | Total, paid, due and payment status |
| GET | `/api/payments/gateway/invoice-status/{billId}` | PDF-documentation-style alias (bill ID, no tenant ID) |
| GET | `/api/payments/bills/{billId}/history` | Bill payment records |
| GET | `/api/payments/bills/{billId}/invoice-pdf` | Generate current bill PDF (QuestPDF license configuration required) |
| POST | `/api/payments/manual` | Cash; records paid amount once per idempotency key |
| POST | `/api/payments/bank-transfers` | UTR submission, **not** credited immediately |
| POST | `/api/payments/bank-transfers/{id}/confirm` | Admin-only verification; confirm/reject |
| GET | `/api/payments/transactions?branchId=2` | Branch transaction list |
| GET | `/api/payments/transactions/export?branchId=2` | UTF-8 CSV export readable in Excel |
| GET | `/api/payments/gateway/export-excel?branchId=2` | Export alias |
| POST | `/api/payments/gateway/simulate-qr-payment/{billId}` | Development only, does **not** credit any bill |

Create order/QR example:

```json
{ "billId": 24, "amount": 500.00, "method": "upi", "idempotencyKey": "bill24-attempt1" }
```

Verify example:

```json
{ "paymentAttemptId": 1, "razorpayOrderId": "order_example", "razorpayPaymentId": "pay_example", "razorpaySignature": "signature_from_checkout" }
```

Cash example:

```json
{ "billId": 24, "amount": 200.00, "idempotencyKey": "cash-bill24-001", "notes": "Counter cash" }
```

Bank transfer example:

```json
{ "billId": 24, "amount": 300.00, "utr": "EXAMPLEUTR123", "bankName": "Example Bank" }
```

The existing `PharmacyPayments` ledger and `PharmacyBills.PaymentStatus` are updated only when the new payment APIs are successfully invoked; the migration itself does not change historical rows. No stock deduction is performed by any new payment API. No direct client-side success message may mark a gateway payment as paid.

## Security and deployment cautions

1. Back up your SQL Server database and existing source before applying migration. Migration `20261112140000_AddStandaloneDispensingImagesAndBranchPayments` creates **eight new tables only**. It has no destructive Down operation.
2. The existing backend has other payment endpoints. Do not let the frontend call both old and new payment APIs for the same collection; reconcile transaction history and permission rules before going live.
3. Real gateway refunds, customer-facing public payment links, automatic merchant settlement reconciliation, branch credential rotation with pending attempts, and detailed audit logs require a separate production-hardening phase. The original IBMS PDF describes customer payment links, but a secure public link/payment-token flow is **not implemented here**.
4. This source has **not been compiled or exercised against Razorpay** in this environment. Test all endpoints with Razorpay Test mode, signed webhooks, duplicate deliveries, two concurrent payments, cross-branch access, account deactivation, and partial refunds before production. A QR API may be unavailable for some Razorpay accounts.
5. QuestPDF PDF generation may require explicit license configuration appropriate for your organization. Do not deploy the invoice PDF endpoint without verifying the license.
6. Keep existing pharmacy JWT claims and permission assignments. The new APIs use existing `Bills`, `Prescriptions`, `Medicines`, `Reports`, and `Settings` permissions; no extra Pharmacist roles are introduced.
7. Frontend must use the new endpoints explicitly; replacing the backend ZIP alone will not change the React screens.

## Windows extraction, build and migration

```powershell
dotnet restore .\Pharmacy.sln
dotnet build .\Pharmacy.sln
dotnet tool restore
dotnet ef migrations list --context AppDbContext
dotnet ef migrations script --context AppDbContext -o phase13_review.sql
# Review the script and back up SQL Server before proceeding.
dotnet ef database update --context AppDbContext
dotnet run --project .\PharmacyOnly.Api.csproj
```

If `dotnet ef migrations list` shows pending migrations other than Phase 13, inspect them before updating. Test new endpoints in Swagger with a test pharmacy and separate test branch merchants.
