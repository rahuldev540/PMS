# Phase 13S - Standalone PMS Razorpay gateway extension (additive source patch)

## Basis and preservation

Based on user-provided `PMS_STANDALONE_PHASE13R_SWAGGER_500_HOTFIX(2).zip`.
**The source already contained a Razorpay gateway.** This update extends and hardens that implementation. It does not create a parallel payment ledger, does not delete or change existing database records, and does not add or modify an EF migration. Every original ZIP entry is included.

Existing files **extended**:
- `Controllers/PharmacyGatewayPaymentsController.cs`: signed/verified recovery of captured checkout and QR payments; safeguards for duplicate captured payment ID and unmatched webhook event; check Test/Live merchant key mismatch before order/QR creation.
- `Controllers/BranchPaymentSettingsController.cs`: block environment changes while payment attempts are pending and reject Test/Live Key ID mismatches for enabled online payments.

New files:
- `Controllers/BranchGatewayConnectivityController.cs`: read-only branch merchant status and credential verification.
- `Controllers/PharmacyPaymentReportsController.cs`: authorized branch and pharmacy-wide collection summaries from existing `PharmacyPayments` entries.
- This README.

### No new Phase 13S migration

The gateway **schema** already comes from `20261112140000_AddStandaloneDispensingImagesAndBranchPayments`. The later Phase 13R patient-return migration is also retained. If the existing database has not had the earlier additive migrations applied, review the SQL migration script and apply them after backing up (see below). The Phase 13S patch itself introduces no new tables, drops or data conversions.

## Merchant model

Each branch of a pharmacy has its own Razorpay account (pending Razorpay onboarding approval). Admin uses a *public branch ID* such as 1 or 2. The backend securely converts it to an internal branch ID. Payment attempts are tied to the **bill's** Pharmacy/Branch and original merchant configuration. Admin sees all of their pharmacy's collection totals; Pharmacists see their assigned branch with Reports.View permission. Never send arbitrary PharmacyId or internal BranchId from the frontend.

## Existing payment APIs retained

| Method | Route | Workflow |
|---|---|---|
| GET | `/api/payment-settings/branches` | Admin branch settings list |
| GET/PUT | `/api/payment-settings/branches/{publicBranchId}` | Admin merchant credentials, methods, partial payments |
| POST | `/api/payments/gateway/create-order` | Razorpay checkout order for UPI app/card |
| POST | `/api/payments/gateway/create-qr` | Single-use fixed-price UPI QR (QR Codes must be enabled for merchant) |
| POST | `/api/payments/gateway/verify` | Check Razorpay checkout signature then captured payment at gateway |
| POST | `/api/payments/gateway/webhook/{merchantKey}` | Signed Razorpay server webhook for payment capture/QR credit |
| GET | `/api/payments/gateway/attempts/{id}/status` | Check local attempt status |
| GET | `/api/payments/bills/{billId}/status` | Total, paid and balance |
| GET | `/api/payments/bills/{billId}/history` | Existing bill payments |
| GET | `/api/payments/bills/{billId}/invoice-pdf` | Invoice PDF |
| POST | `/api/payments/manual` | Authorized counter cash |
| POST | `/api/payments/bank-transfers` | Submit UTR, not credited yet |
| POST | `/api/payments/bank-transfers/{id}/confirm` | Admin verifies bank receipt, confirms or rejects |
| GET | `/api/payments/transactions?branchId=2` | Branch payment ledger |
| GET | `/api/payments/transactions/export?branchId=2` | CSV/Excel export |

## NEW Phase 13S APIs

| Method | Route | Authorization | Effect |
|---|---|---|---|
| GET | `/api/payment-settings/branches/{publicBranchId}/status` | Admin, Settings.View | Lists configured methods, Test/Live match, webhook readiness; no credentials disclosed |
| POST | `/api/payment-settings/branches/{publicBranchId}/verify` | Admin, Settings.Edit | Read-only authenticated GET of one Razorpay order-list page (count=1); **does not charge money** |
| POST | `/api/payments/gateway/attempts/{attemptId}/reconcile` | Admin or assigned Pharmacist, Bills.Create | Fetches payment history from **the original merchant account**, rechecks captured amount/currency/order and applies existing idempotent settlement logic |
| GET | `/api/payments/reports/branch/{publicBranchId}` | Admin or assigned Pharmacist, Reports.View | Total collected, refunded, net, payment count for one branch |
| GET | `/api/payments/reports/pharmacy` | Admin, Reports.View | Pharmacy total plus public-ID branch breakdown; flags any payment branch missing a public mapping |

### Example 1 - Admin configures Getha public Branch ID 2

`PUT /api/payment-settings/branches/2`

```json
{
  "merchantName": "Getha Pharmacy",
  "environment": "Test",
  "razorpayKeyId": "rzp_test_YOUR_KEY_ID",
  "razorpayKeySecret": "YOUR_TEST_SECRET_HERE",
  "razorpayWebhookSecret": "YOUR_INDEPENDENT_WEBHOOK_SECRET",
  "cash": true,
  "upi": true,
  "card": true,
  "bankTransfer": true,
  "allowPartialPayments": true
}
```

**Do not check real secrets into the ZIP, frontend files or Git.** This PUT body is only for the authenticated Admin over HTTPS. The backend encrypts secrets using `PaymentGateway:CredentialEncryptionKey` (32-byte base64). It must remain stable and be backed up securely. Credentials are NOT returned in API responses.

Run `GET /api/payment-settings/branches/2/status` to check configuration flags. Run `POST /api/payment-settings/branches/2/verify` with `{}` to test read-only merchant API authentication. A successful credential verification does **not** prove QR activation, webhook delivery, settlement activation or production suitability.

To set up the local encryption key from the extracted project root in **PowerShell** (do this on the running API machine, once):

```powershell
$key = [Convert]::ToBase64String([Security.Cryptography.RandomNumberGenerator]::GetBytes(32))
dotnet user-secrets set "PaymentGateway:CredentialEncryptionKey" "$key" --project .\PharmacyOnly.Api.csproj
```

**Do not regenerate this key on every run**: that would make existing encrypted branch credentials unreadable. Production should use a managed secret store, backed-up key and a documented rotation procedure.

### Example 2 - Customer pays INR 500 of a INR 1000 Getha bill (bill ID is illustrative)

`POST /api/payments/gateway/create-order`

```json
{"billId":24,"amount":500.00,"method":"upi","idempotencyKey":"bill24-upi-1"}
```

`201` returns `data.paymentAttemptId`, `data.razorpayOrderId`, `data.razorpayKeyId`, `data.amountPaise` and `status: Pending`. Frontend opens Razorpay Checkout with this **branch's public Key ID**, server-issued order ID, amount `50000` paise and INR currency; Razorpay Secret Key must never be sent to the browser. The callback supplies:

```json
{"paymentAttemptId":101,"razorpayOrderId":"order_EXAMPLE","razorpayPaymentId":"pay_EXAMPLE","razorpaySignature":"SIGNATURE_FROM_RAZORPAY"}
```

`POST /api/payments/gateway/verify` validates the checkout signature, fetches provider status through the branch credentials and only credits a **captured** payment. Expected if no earlier payments: INR 500 paid, INR 500 due, bill `PartiallyPaid`. A subsequent validated INR 500 payment makes it `Paid`. Retry with the **same** idempotency key; do not invent a fresh key on a network retry.

If the checkout success callback is missed, call `POST /api/payments/gateway/attempts/101/reconcile` (empty body) to query captured Razorpay payments **using original branch merchant credentials**. It can also recover QR payments (from the QR payment-list endpoint). No successful captured payment -> status Pending and no bill credit. Manual investigation is required for multiple captures or excess balance. Webhooks handle the normal asynchronous path.

### Example 3 - UPI QR

`POST /api/payments/gateway/create-qr`

```json
{"billId":24,"amount":300.00,"method":"upi","idempotencyKey":"bill24-qr-1"}
```

Returns `qrImageUrl` from Razorpay and a `paymentAttemptId`. **QR Code product access may require Razorpay activation per branch.** Once the customer scans and pays, Razorpay signs `qr_code.credited`/`payment.captured` webhook events. Your API checks original raw payload against the merchant-specific webhook secret, fetches provider payment details and credits exactly once. Frontend polls `GET /api/payments/gateway/attempts/{id}/status`. Neither scanning the QR nor a screenshot proves payment.

### Example 4 - Cash + bank transfer

`POST /api/payments/manual`

```json
{"billId":24,"amount":200.00,"idempotencyKey":"bill24-cash-1","notes":"Received at branch counter"}
```

`POST /api/payments/bank-transfers`

```json
{"billId":24,"amount":300.00,"utr":"UTR_EXAMPLE_123","bankName":"Example Bank"}
```

Bank transfer stays `PendingVerification` until an authorized Admin independently checks the branch bank statement and uses `POST /api/payments/bank-transfers/{id}/confirm`:

```json
{"decision":"Confirm","notes":"Matched with branch bank statement"}
```

Cash and bank transfer do **not** call Razorpay checkout. Do not use the bank-transfer confirmation endpoint as an automated online-payment success callback.

### Example 5 - Reports

`GET /api/payments/reports/branch/2`:

```json
{"data":{"branchId":2,"currency":"INR","transactionCount":3,"grossCollected":1000.00,"refunded":0.00,"netCollected":1000.00}}
```

`GET /api/payments/reports/pharmacy`:

```json
{"data":{"currency":"INR","transactionCount":3,"grossCollected":1000.00,"refunded":0.00,"netCollected":1000.00,"branchesWithoutPublicMapping":0,"items":[{"branchId":2,"branchName":"Getha","transactionCount":3,"grossCollected":1000.00,"refunded":0.00,"netCollected":1000.00}]}}
```

Figures are illustrative. Reports show the existing PMS *collected payments*, not Razorpay merchant settlement bank deposits or total billed revenue. Legacy payments from unmapped branches are included in pharmacy totals and flagged for public-ID backfill.

## Existing flow and operational warnings

- **Do not run both old and new billing payment endpoints for the same payment.** The ZIP still includes older `/api/pharmacy` payment and split-payment flows. Some legacy operations accept a manually supplied payment mode without verifying Razorpay. The frontend must use only the new `api/payments` checkout/verification path for Razorpay and the explicit new `manual` route for cash. Audit/deprecate legacy online modes before production.
- Stock is reduced **only** by the existing `POST /api/pharmacy/dispense` workflow. Payment events never automatically deduct inventory.
- The source already has a development-only QR simulator. It explicitly does **not** mark bills as paid.
- Customer-facing anonymous payment links, actual payouts/settlement reconciliation, live refunds, chargeback handling and automatic background replay of unmatched webhooks are NOT included. A dedicated production-hardening phase is needed for those workflows.
- The updated webhook returns a non-success status for unmatched captured payments to allow Razorpay retries. Configure provider webhooks for every independent branch account. Review webhook delivery failures and handle exhausted retries manually; a webhook retry is not guaranteed to arrive indefinitely.
- Use HTTPS for browser callbacks and webhooks. Do not publish live credentials, encryption keys or access tokens. Use test credentials until authorization, signing, accounting reconciliation, QR activation, partial-payment concurrency and branch separation pass validation.
- The original ZIP includes application configuration containing credential-like values; review and **rotate any real embedded credentials** before public deployment. This patch does not change that file to preserve the supplied project configuration.

## Build, migrate (if necessary), test

1. Back up the database and the previous project. Extract this ZIP to a *new, clean directory*. No nested wrapper folder is in the archive.
2. Stop the old API instance. In Visual Studio 2022 Developer PowerShell from the extracted directory:

```powershell
dotnet restore .\Pharmacy.sln
dotnet build .\Pharmacy.sln
```

3. **No NEW migration is required for Phase 13S**. Before running anything against an existing DB, compare `dotnet ef migrations list --context AppDbContext` and the `__EFMigrationsHistory` SQL table. If the earlier Phase 13/Phase 13R additive migrations are pending, back up and review a migration SQL script, then run:

```powershell
dotnet tool restore
dotnet ef migrations list --context AppDbContext
dotnet ef migrations script --idempotent --context AppDbContext -o pms_pending_migrations_review.sql
# AFTER review and backup only:
dotnet ef database update --context AppDbContext
```

4. Start `dotnet run --project .\PharmacyOnly.Api.csproj`, open **local** Swagger, log in as Admin, set Branch 1 and Branch 2 credentials (test-mode accounts), then verify both separately. Set webhooks in both Razorpay dashboards to their respective `webhookPath` on a publicly reachable backend HTTPS host; `localhost` is not remotely reachable.
5. In Razorpay test mode: (a) successful checkout, (b) declined checkout, (c) partial payment, (d) QR confirmation if enabled, (e) duplicate webhook, (f) two competing payment attempts, (g) cross-branch 403, (h) bank verification, (i) cash, (j) pharmacy and branch reports. Ensure the same provider payment ID only produces one `PharmacyPayments` credit.

## Verification limitations

This package was generated by source inspection and static route/archive checks. This execution environment does NOT provide a .NET 8 SDK, SQL Server or Razorpay merchant credentials. Therefore **no compiled build, migration execution, Razorpay API connection, real payment or Swagger runtime test is claimed**. First compile locally in a copy and test in a backed-up test database before enabling live money collection.
