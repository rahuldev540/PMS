# Pharmacy Admin Dashboard update

GET /api/pharmacy-admin/dashboard now aggregates inventory, medicines, prescriptions, sales and alerts across all branches belonging to the authenticated admin pharmacy. BranchPerformance reports sales by branch with pharmacy-scoped public branch IDs. TotalBranches added. The BranchId at the root remains the authenticated admin assigned branch (public ID). Pending pharmacy assignment flow and pharmacist dashboard unchanged. Super Admin financial privacy changes retained.

Important: verify PharmacyBranchIdentities rows and public branch IDs in your database; this update does not renumber existing identity mappings. The 30-day expiry alerts and UTC-based daily sales behavior are unchanged. Build and integration test before deployment.
