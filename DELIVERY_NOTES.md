# Required pharmacy modules delivery

This version extends the original Pharmacy Only API with the required end-to-end modules:

- Separate global `PharmacySuperAdmin`, branch `PharmacyAdmin`, and branch `Pharmacist` authentication and authorization
- Pharmacy Super Admin creation and management of one Pharmacy Admin per hospital branch
- Secure CMS backend integration for doctor medicine availability and completed prescriptions

- Pharmacist account administration, permissions, password change and logout
- Doctor medicine availability and prescription creation/completion
- Supplier management
- Purchase orders and goods receipt
- Medicine master, classifications and availability
- Aggregate and batch inventory
- FEFO dispensing with non-expired, non-quarantined batches
- Stock ledger, adjustments, disposal and quarantine
- Patient and supplier returns
- Inter-branch stock transfers
- Bills, persisted payments and refunds
- Dashboard, alerts, sales, inventory, expiry, purchase and audit reports

The database upgrade is in `Database/20260804_required_pharmacy_modules.sql`.
The complete route list is in `API_CATALOG.md` and Swagger exposes the same controllers.

## Important deployment notes

- Take a database backup before running the SQL script.
- The API may share hospital, branch, doctor, patient and appointment data with the CMS database, but uses its own Pharmacy JWT signing key.
- CMS JWTs are not accepted. The CMS backend uses the dedicated integration key for doctor medicine availability and prescription submission.
- Run `dotnet restore`, `dotnet build`, and your integration tests in the deployment environment before release.
