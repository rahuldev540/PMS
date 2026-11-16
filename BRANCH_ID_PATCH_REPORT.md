# Branch ID inspection and patch report

## Implemented
- `GET /api/pharmacy-admin/dashboard`: returns pharmacy-specific `branchId` while continuing to query using internal IDs.
- `GET /api/pharmacist/dashboard`: same response correction.
- `GET /api/pharmacy/payments`: maps the payment-history `items[].branchId` to the pharmacy-specific ID without changing stored payments.
- Added `Helpers/PharmacyPublicBranchMapper.cs`, which requires an exact mapping for the pharmacy and internal branch.

## Database impact
No schema changes or migrations. Existing data and foreign keys are preserved. Do not manually update `Branches.Id` or payment foreign keys.

## Inspection scope and limitations
Searched the controller source for common assignments of internal branch IDs. These are **candidates**, not proof that every listed API exposes an incorrect ID: some are necessary database entity assignments or filters. Other endpoints may still expose internal IDs. This is not a full API-wide remediation.

### Candidate assignments for further review
- `PharmacyAdminSettingsController.cs:132: BranchId = BranchId,`
- `PharmacySuperAdminController.cs:127: BranchId = branchId,`
- `DoctorPrescriptionController.cs:26: var p = new Prescription { HospitalId = HospitalId, BranchId = BranchId, AppointmentId = dto.AppointmentId,`
- `RealtimePharmacyOperationsController.cs:41: _db.MedicineRecalls.Add(new MedicineRecall{HospitalId=HospitalId,BranchId=BranchId,MedicineId=dto.MedicineId,MedicineBatchId=dto.BatchId,Reason=dto.Re`
- `RealtimePharmacyOperationsController.cs:46: public async Task<IActionResult> OpenShift([FromBody] OpenShiftRequest dto){var uid=ClaimInt(ClaimTypes.NameIdentifier);if(await _db.PharmacyShifts.An`
- `RealtimePharmacyOperationsController.cs:53: public async Task<IActionResult> SupplierInvoice([FromBody] SupplierInvoiceRequest dto){if(dto.TotalAmount<0||dto.PaidAmount<0||dto.PaidAmount>dto.Tot`
- `RealtimePharmacyOperationsController.cs:56: public async Task<IActionResult> SplitPayment(int billId,[FromBody] SplitPaymentRequest dto){var bill=await _db.PharmacyBills.FirstOrDefaultAsync(x=>x`
- `IntegrationController.cs:182: BranchId = branch.Id,`
- `PharmacistAdminController.cs:836: pharmacist.BranchId = targetBranch.Id;`
- `PharmacistAdminController.cs:992: pharmacist.BranchId = branch.Id;`
- `SupplierController.cs:99: BranchId = BranchId`
- `PharmacyAdminRegistrationController.cs:78: admin.BranchId = main.Id;`
- `PharmacyController.cs:208: BranchId = BranchId,`
- `PharmacyController.cs:226: BranchId = BranchId,`
- `PharmacyController.cs:241: BranchId = BranchId,`
- `PharmacyController.cs:257: BranchId = BranchId,`

## Validation
This environment has no .NET SDK, so compilation and live API tests could not be performed. Build the solution in Visual Studio and test with PharmacyId 2 / public BranchId 1 (internal BranchId 2).
