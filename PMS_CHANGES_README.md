# PMS changes — medicine access and branch revenue

## GET /api/medicine
- PharmacyAdmin: no branchId returns all branches separately; `?branchId=1` selects public pharmacy branch ID 1.
- Pharmacist: no branchId or matching public assigned branchId returns assigned branch medicine DTOs; another branch returns 403.
- Uses internal Branch.Id when calling existing medicine service.
- Requires Medicines/View permission through the existing permission checker.

## GET /api/pharmacy/revenue?from=2026-10-01&to=2026-10-31
- PharmacyAdmin only, pharmacy scoped; optional `branchId` filters to public pharmacy branch ID.
- Uses paid PharmacyBills, grouped by branch, with zero totals for branches without paid bills.
- Date filtering uses BillDate; figures are billed revenue including GST, **not net cash flow or profit**. Refunds are NOT deducted. A production cash-revenue report should reconcile payment/refund records separately.

## Verification
- No new EF migrations required for these changes.
- Compile and test in Visual Studio; dotnet SDK is not available in this preparation environment.
- Confirm the Pharmacist JWT BranchId claim contains the **internal** Branch.Id (as assumed by the existing base controller). If it contains public branch ID, adjust the resolution.
- Confirm the desired revenue policy for refunds, partial payments, cancelled bills, and tax.
- This patch does not claim to remove every remaining CMS dependency elsewhere in the project.
