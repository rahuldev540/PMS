# Phase 5B51 – comprehensive hospital dependency verification

This is **not** a completed standalone conversion. The project source remains on the Phase 5B50 baseline.

The prior PMSDB10 checks established zero non-null HospitalId values in Patients, Branches, Doctors, Appointments and Prescriptions, and zero rows in Hospitals. They did **not** check every other table.

Run `scripts/check_all_hospital_links.sql` in SSMS against the correct PMS database. It dynamically checks **every existing table with a HospitalId column**, then lists all hospital-named columns/tables and foreign keys. It is read-only.

Do not drop schema objects manually or run an EF migration from this archive. A coordinated source+snapshot+guarded migration change is still required.
