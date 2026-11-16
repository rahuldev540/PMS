PHASE 5A2 — STANDALONE AUTHENTICATION RESPONSE CLEANUP

Based on Phase 5A1. Targeted changes:
- Removed unnecessary Hospital navigation eager-loading from UnifiedAuthController and PharmacyAuthService login queries.
- Removed obsolete CMS/hospital fields from the pharmacist login response DTO and its initializer (they were already JsonIgnore-hidden).
- Updated misleading hospital comments in authentication and base controller.

No database entities, EF mappings, or migrations changed. No migration needed.
IMPORTANT: Other CMS/HospitalId dependencies remain. This is not final standalone conversion.
BUILD STATUS: dotnet SDK unavailable in packaging environment; run dotnet restore/build locally.
Test login for all roles, pharmacist permission response, and pharmacy/branch assignment.
