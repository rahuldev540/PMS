# Pharmacy Admin dashboard update

Endpoint: `GET /api/pharmacy/dashboard?range=today|7d|30d&branchId=PUBLIC_BRANCH_ID`

- Omit `branchId` to aggregate all branches of the authenticated pharmacy.
- `branchId` uses the pharmacy-scoped public branch number (not internal SQL identity).
- Date periods use UTC calendar dates (the existing BillDate convention); 7d and 30d include today.
- `todaySales` is always today's paid bills; `periodSales` follows range. `salesChange` compares the selected period to the preceding equal-length period. `null` means previous sales were zero.
- Low stock counts in-stock inventory records at or below reorder level; out-of-stock is separate.
- Expiry counts positive, non-quarantined, non-recalled batches within the branch's configured expiry warning horizon, default 30 days. Already expired batches are separate.
- `lowStockChange` and `expiryChange` are null: no reliable historical snapshots exist in this schema; do not manufacture trends.
- `reports` is an empty array: this source project generates reports on demand and has no persisted report history entity.
- Top medicines are from paid bill items only. Recent stock is from StockTransactions with optional batch details.
- Existing `GET /api/pharmacy-admin/dashboard` remains unchanged for backward compatibility.

## Suggested Swagger verification cases
1. Login as PharmacyAdmin, authorize; request `?range=today`, `?range=7d`, `?range=30d` and compare periods.
2. Request without branchId, then each valid public branchId; check sums and per-branch stock/expiry.
3. Try another pharmacy's branchId: should return 404 (unless same public number exists in your pharmacy, in which case it selects your own branch).
4. Invalid range should return 400; no token 401; insufficient Dashboard/View permission 403.
5. Test empty pharmacy: zero metrics, empty top medicines and stock lists; no divide-by-zero.
6. Test missing batches: zero expiry, no crash.
7. Test paid vs pending bills; only paid bills should affect sales.

**Build note:** dotnet SDK is unavailable in the packaging environment. Run `dotnet build` and `dotnet test` in your Visual Studio environment before deploying. No database migration was added.
