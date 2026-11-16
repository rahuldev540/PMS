# Pharmacy-scoped pharmacist IDs — implementation notes

- Standalone pharmacy Admin pharmacist APIs use `(PharmacyId, PharmacistId)` from `PharmacyPharmacistIdentities`.
- Each pharmacy starts pharmacist numbering at 1, regardless of branch; users across different pharmacies may both have `pharmacistId=1`.
- `Users.Id` is unchanged and is used internally for authentication, JWT, permissions and other foreign keys. CMS is unchanged.
- Create and edit pharmacist requests accept the public pharmacy-specific `branchId`, which is resolved to the internal `Branches.Id`.
- The migration creates `PharmacyPharmacistIdentities` and backfills existing standalone pharmacists in ascending `Users.Id` order per pharmacy.
- Apply the existing pharmacy branch identity migration first, then the pharmacist identity migration.
- This package has NOT been compiled or integration tested (dotnet SDK is unavailable here). Back up your DB and project; review migration and test all affected Swagger endpoints before production use.
- The original `Users.Id` and `Branches.Id` columns remain globally unique. Pharmacy-local IDs live in separate database identity tables.
