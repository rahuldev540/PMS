using AuthDemo.Data;
using AuthDemo.DTOs;
using AuthDemo.Models;
using AuthDemo.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace AuthDemo.Services;

public class PharmacyService : IPharmacyService
{
    private readonly AppDbContext _context;
    private readonly IPharmacyRealtimeNotifier _realtime;

    public PharmacyService(AppDbContext context, IPharmacyRealtimeNotifier realtime)
    {
        _context = context; _realtime = realtime;
    }

    // =====================================================
    // PHARMACY / BRANCH SCOPE
    // =====================================================

    private async Task<Branch> GetActiveBranchAsync(
        int pharmacyId,
        int branchId)
    {
        if (pharmacyId <= 0)
            throw new UnauthorizedAccessException(
                "A valid pharmacy assignment is required.");

        if (branchId <= 0)
            throw new UnauthorizedAccessException(
                "A valid branch selection is required.");

        var branch =
            await _context.Branches
                .AsNoTracking()
                .FirstOrDefaultAsync(x =>
                    x.Id == branchId &&
                    x.PharmacyId == pharmacyId &&
                    x.IsActive);

        if (branch == null)
        {
            throw new UnauthorizedAccessException(
                "The selected branch is inactive, invalid, or does not belong to the assigned pharmacy.");
        }

        return branch;
    }

    // =====================================================
    // DISPENSE PRESCRIPTION
    // =====================================================

    public async Task<string> DispensePrescriptionAsync(
        DispensePrescriptionDto dto,
        int pharmacyId,
        int branchId,
        int userId)
    {
        var scopedBranch =
            await GetActiveBranchAsync(
                pharmacyId,
                branchId);

        if (dto.Medicines == null ||
            dto.Medicines.Count == 0)
        {
            throw new Exception(
                "At least one medicine is required.");
        }

        if (dto.Medicines.Any(x =>
                x.Quantity <= 0))
        {
            throw new Exception(
                "Dispense quantity must be greater than zero.");
        }

        var duplicateItems =
            dto.Medicines
                .GroupBy(x =>
                    x.PrescriptionItemId)
                .Where(x =>
                    x.Count() > 1)
                .Select(x =>
                    x.Key)
                .ToList();

        if (duplicateItems.Count > 0)
        {
            throw new Exception(
                "Duplicate prescription items are not allowed.");
        }

        await using var transaction =
            await _context.Database
                .BeginTransactionAsync();

        try
        {
            var prescription =
                await _context.Prescriptions
                    .Include(x =>
                        x.Medicines)
                    .ThenInclude(x =>
                        x.Medicine)
                    .FirstOrDefaultAsync(x =>
                        x.Id ==
                        dto.PrescriptionId &&
                        x.PharmacyId == pharmacyId &&
                        x.BranchId ==
                        branchId);

            if (prescription == null)
            {
                throw new Exception(
                    "Prescription not found.");
            }

            if (prescription.ValidUntil.HasValue && prescription.ValidUntil.Value.Date < DateTime.UtcNow.Date)
                throw new Exception("Prescription has expired and cannot be dispensed.");

            if (prescription.Status ==
                Prescription.StatusDispensed)
            {
                throw new Exception(
                    "Prescription already dispensed.");
            }

            if (prescription.Status ==
                Prescription.StatusCancelled)
            {
                throw new Exception(
                    "Cancelled prescription cannot be dispensed.");
            }

            foreach (var item in dto.Medicines)
            {
                var prescriptionItem =
                    prescription.Medicines
                        .FirstOrDefault(x =>
                            x.Id ==
                            item.PrescriptionItemId);

                if (prescriptionItem == null)
                {
                    throw new Exception(
                        "Prescription item not found.");
                }

                if (prescriptionItem.Status ==
                    "Dispensed")
                {
                    throw new Exception(
                        $"{prescriptionItem.Medicine.Name} has already been dispensed.");
                }

                var remainingPrescribed = prescriptionItem.Quantity - prescriptionItem.DispensedQuantity;
                if (item.Quantity > remainingPrescribed)
                {
                    throw new Exception(
                        $"Dispense quantity cannot exceed prescribed quantity for {prescriptionItem.Medicine.Name}.");
                }

                var inventory =
                    await _context.Inventories
                        .FirstOrDefaultAsync(x =>
                            x.MedicineId ==
                            prescriptionItem.MedicineId &&
                            x.BranchId ==
                            branchId);

                if (inventory == null)
                {
                    throw new Exception(
                        $"Inventory not found for Medicine Id {prescriptionItem.MedicineId}.");
                }

                var reservedForItem = Math.Min(prescriptionItem.ReservedQuantity, item.Quantity);
                if (reservedForItem > 0)
                {
                    inventory.ReservedStock = Math.Max(0, inventory.ReservedStock - reservedForItem);
                    inventory.AvailableStock += reservedForItem;
                    prescriptionItem.ReservedQuantity -= reservedForItem;
                }

                if (inventory.AvailableStock <
                    item.Quantity)
                {
                    throw new Exception(
                        $"Insufficient stock for {prescriptionItem.Medicine.Name}.");
                }

                var today =
                    DateTime.UtcNow.Date;

                var batches =
                    await _context.MedicineBatches
                        .Where(x =>
                            x.InventoryId ==
                            inventory.Id &&
                            x.RemainingQuantity > 0 &&
                            !x.IsQuarantined &&
                            !x.IsRecalled &&
                            !x.Inventory.Medicine.IsRecalled &&
                            x.ExpiryDate >= today)
                        .OrderBy(x =>
                            x.ExpiryDate)
                        .ThenBy(x =>
                            x.Id)
                        .ToListAsync();

                if (batches.Sum(x =>
                        x.RemainingQuantity) <
                    item.Quantity)
                {
                    throw new Exception(
                        $"Insufficient usable batch stock for {prescriptionItem.Medicine.Name}.");
                }

                var remainingToDispense =
                    item.Quantity;

                var runningBalance =
                    inventory.CurrentStock;

                foreach (var batch in batches)
                {
                    if (remainingToDispense <= 0)
                        break;

                    var issued =
                        Math.Min(
                            batch.RemainingQuantity,
                            remainingToDispense);

                    batch.RemainingQuantity -=
                        issued;

                    remainingToDispense -=
                        issued;

                    runningBalance -=
                        issued;

                    _context.StockTransactions.Add(
                        new StockTransaction
                        {
                            PharmacyId =
                                pharmacyId,

                            BranchId =
                                branchId,

                            MedicineId =
                                prescriptionItem.MedicineId,

                            MedicineBatchId =
                                batch.Id,

                            TransactionType =
                                "Dispense",

                            Quantity =
                                issued,

                            BalanceStock =
                                runningBalance,

                            Remarks =
                                $"Prescription #{prescription.Id}; Batch {batch.BatchNumber}",

                            ReferenceType =
                                "Prescription",

                            ReferenceId =
                                prescription.Id,

                            CreatedByUserId =
                                userId,

                            TransactionDate =
                                DateTime.UtcNow
                        });
                }

                inventory.AvailableStock -=
                    item.Quantity;

                inventory.CurrentStock -=
                    item.Quantity;

                inventory.LastUpdated =
                    DateTime.UtcNow;

                prescriptionItem.DispensedQuantity += item.Quantity;
                prescriptionItem.Status = prescriptionItem.DispensedQuantity >= prescriptionItem.Quantity
                    ? "Dispensed" : "PartiallyDispensed";
            }

            prescription.Status =
                prescription.Medicines
                    .All(x => x.Status == "Dispensed")
                    ? Prescription.StatusDispensed
                    : Prescription.StatusPartiallyDispensed;

            await _context.SaveChangesAsync();

            await transaction.CommitAsync();
            await _realtime.PublishAsync(
                pharmacyId,
                branchId,
                "InventoryChanged",
                new { PrescriptionId = prescription.Id });

            await _realtime.PublishAsync(
                pharmacyId,
                branchId,
                "PrescriptionUpdated",
                new { PrescriptionId = prescription.Id, prescription.Status });

            return "Medicines dispensed successfully.";
        }
        catch (DbUpdateConcurrencyException)
        {
            await transaction.RollbackAsync();
            throw new InvalidOperationException("Stock changed while this prescription was being dispensed. Refresh inventory and retry.");
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    // =====================================================
    // PENDING PRESCRIPTIONS
    // =====================================================

    public async Task<List<PendingPrescriptionDto>>
        GetPendingPrescriptionsAsync(
            int pharmacyId,
            int branchId)
    {
        var scopedBranch =
            await GetActiveBranchAsync(
                pharmacyId,
                branchId);

        return await _context.Prescriptions
            .AsNoTracking()
            .Include(x =>
                x.Patient)
            .Include(x =>
                x.Appointment)
            .ThenInclude(x =>
                x.Doctor)
            .Include(x =>
                x.Medicines)
            .Where(x =>
                x.PharmacyId == pharmacyId &&
                x.BranchId == branchId &&
                x.Status == Prescription.StatusPending)
            .OrderByDescending(x =>
                x.PrescriptionDate)
            .Select(x =>
                new PendingPrescriptionDto
                {
                    PrescriptionId =
                        x.Id,

                    AppointmentId =
                        x.AppointmentId,

                    PatientId =
                        x.PatientId,

                    PatientName =
                        x.Patient.Name,

                    PatientMobile =
                        x.Patient.Phone,

                    DoctorName =
                        x.Appointment.Doctor.Name,

                    PrescriptionDate =
                        x.PrescriptionDate,

                    TotalMedicines =
                        x.Medicines.Count,

                    Status =
                        x.Status
                })
            .ToListAsync();
    }

    // =====================================================
    // GET PRESCRIPTION DETAILS
    // =====================================================

    public async Task<PharmacyPrescriptionDto?>
        GetPrescriptionAsync(
            int prescriptionId,
            int pharmacyId,
            int branchId)
    {
        var scopedBranch =
            await GetActiveBranchAsync(
                pharmacyId,
                branchId);

        var prescription =
            await _context.Prescriptions
                .AsNoTracking()
                .Include(x =>
                    x.Patient)
                .Include(x =>
                    x.Doctor)
                .Include(x =>
                    x.Medicines)
                .ThenInclude(x =>
                    x.Medicine)
                .FirstOrDefaultAsync(x =>
                    x.Id ==
                    prescriptionId &&
                    x.PharmacyId == pharmacyId &&
                    x.BranchId ==
                    branchId);

        if (prescription == null)
            return null;

        var result =
            new PharmacyPrescriptionDto
            {
                PrescriptionId =
                    prescription.Id,

                PatientName =
                    prescription.Patient.Name,

                PatientMobile =
                    prescription.Patient.Phone,

                DoctorName =
                    prescription.Doctor.Name,

                Diagnosis =
                    prescription.Diagnosis,

                Instructions =
                    prescription.Instructions,

                PrescriptionDate =
                    prescription.PrescriptionDate,

                Status =
                    prescription.Status
            };

        foreach (var item in
                 prescription.Medicines)
        {
            var inventory =
                await _context.Inventories
                    .AsNoTracking()
                    .FirstOrDefaultAsync(x =>
                        x.MedicineId ==
                        item.MedicineId &&
                        x.BranchId ==
                        branchId);

            result.Medicines.Add(
                new PharmacyPrescriptionItemDto
                {
                    PrescriptionItemId =
                        item.Id,

                    MedicineId =
                        item.MedicineId,

                    MedicineName =
                        item.Medicine.Name,

                    Dosage =
                        item.Dosage,

                    Instructions =
                        item.Instructions,

                    Morning =
                        item.Morning,

                    Afternoon =
                        item.Afternoon,

                    Night =
                        item.Night,

                    Days =
                        item.Days,

                    Quantity =
                        item.Quantity,

                    AvailableStock =
                        inventory?.AvailableStock ??
                        0,

                    Status =
                        item.Status,

                    Notes =
                        item.Notes
                });
        }

        return result;
    }

    // =====================================================
    // GENERATE PHARMACY BILL
    // =====================================================

    public async Task<int> GenerateBillAsync(
        GeneratePharmacyBillDto dto,
        int pharmacyId,
        int branchId)
    {
        var scopedBranch =
            await GetActiveBranchAsync(
                pharmacyId,
                branchId);

        if (dto.PrescriptionId <= 0)
        {
            throw new Exception(
                "A valid prescription is required.");
        }

        if (dto.Discount < 0)
        {
            throw new Exception(
                "Discount cannot be negative.");
        }

        await using var transaction =
            await _context.Database
                .BeginTransactionAsync();

        try
        {
            var prescription =
                await _context.Prescriptions
                    .Include(x =>
                        x.Medicines)
                    .ThenInclude(x =>
                        x.Medicine)
                    .FirstOrDefaultAsync(x =>
                        x.Id ==
                        dto.PrescriptionId &&
                        x.PharmacyId == pharmacyId &&
                        x.BranchId ==
                        branchId);

            if (prescription == null)
            {
                throw new Exception(
                    "Prescription not found.");
            }

            if (prescription.Status !=
                Prescription.StatusDispensed)
            {
                throw new Exception(
                    "Prescription must be fully dispensed before billing.");
            }

            // Prevent duplicate bill creation.
            var existingBillId =
                await _context.PharmacyBills
                    .AsNoTracking()
                    .Where(x =>
                        x.PrescriptionId ==
                        prescription.Id &&
                        x.BranchId ==
                        branchId &&
                        x.PaymentStatus !=
                        "Cancelled")
                    .Select(x =>
                        (int?)x.Id)
                    .FirstOrDefaultAsync();

            if (existingBillId.HasValue)
            {
                throw new Exception(
                    $"A bill already exists for this prescription. Bill Id: {existingBillId.Value}.");
            }

            decimal subTotal = 0m;
            decimal gstAmount = 0m;

            var bill =
                new PharmacyBill
                {
                    PharmacyId =
                        pharmacyId,

                    BranchId =
                        branchId,

                    PatientId =
                        prescription.PatientId,

                    PrescriptionId =
                        prescription.Id,

                    Discount =
                        dto.Discount,

                    PaymentStatus =
                        "Pending",

                    BillDate =
                        DateTime.UtcNow
                };

            foreach (var item in
                     prescription.Medicines)
            {
                var unitPrice =
                    item.Medicine.SellingPrice;

                if (unitPrice < 0)
                {
                    throw new Exception(
                        $"Invalid selling price for {item.Medicine.Name}.");
                }

                var total =
                    unitPrice *
                    item.Quantity;

                var gst =
                    total *
                    item.Medicine.GST /
                    100m;

                subTotal +=
                    total;

                gstAmount +=
                    gst;

                bill.BillItems.Add(
                    new PharmacyBillItem
                    {
                        MedicineId =
                            item.MedicineId,

                        MedicineName =
                            item.Medicine.Name,

                        Quantity =
                            item.Quantity,

                        Rate =
                            unitPrice,

                        GST =
                            gst,

                        Total =
                            total + gst
                    });
            }

            var beforeDiscount =
                subTotal +
                gstAmount;

            if (dto.Discount >
                beforeDiscount)
            {
                throw new Exception(
                    "Discount cannot exceed the bill amount.");
            }

            bill.SubTotal =
                subTotal;

            bill.GSTAmount =
                gstAmount;

            bill.GrandTotal =
                beforeDiscount -
                dto.Discount;

            _context.PharmacyBills.Add(
                bill);

            await _context.SaveChangesAsync();

            await transaction.CommitAsync();

            return bill.Id;
        }
        catch (DbUpdateConcurrencyException)
        {
            await transaction.RollbackAsync();
            throw new InvalidOperationException("Stock changed while this prescription was being dispensed. Refresh inventory and retry.");
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    // =====================================================
    // PAY PHARMACY BILL
    // =====================================================

    public async Task<string> PayBillAsync(
        PayPharmacyBillDto dto,
        int pharmacyId,
        int branchId)
    {
        var scopedBranch =
            await GetActiveBranchAsync(
                pharmacyId,
                branchId);

        if (dto.BillId <= 0)
        {
            throw new Exception(
                "A valid BillId is required.");
        }

        if (dto.PaidAmount <= 0)
        {
            throw new Exception(
                "Paid amount must be greater than zero.");
        }

        if (string.IsNullOrWhiteSpace(
                dto.PaymentMode))
        {
            throw new Exception(
                "Payment mode is required.");
        }

        await using var transaction =
            await _context.Database
                .BeginTransactionAsync();

        try
        {
            var bill =
                await _context.PharmacyBills
                    .FirstOrDefaultAsync(x =>
                        x.Id ==
                        dto.BillId &&
                        x.BranchId ==
                        branchId);

            if (bill == null)
            {
                throw new Exception(
                    "Bill not found.");
            }

            if (bill.PaymentStatus ==
                "Paid")
            {
                throw new Exception(
                    "Bill already paid.");
            }

            if (bill.PaymentStatus ==
                "Cancelled")
            {
                throw new Exception(
                    "Cancelled bill cannot be paid.");
            }

            if (bill.PaymentStatus ==
                    "Refunded" ||
                bill.PaymentStatus ==
                    "PartiallyRefunded")
            {
                throw new Exception(
                    "Refunded bill cannot be paid again.");
            }

            if (dto.PaidAmount !=
                bill.GrandTotal)
            {
                throw new Exception(
                    "Paid amount must equal the bill total.");
            }

            var existingPaidPayment =
                await _context.PharmacyPayments
                    .AsNoTracking()
                    .AnyAsync(x =>
                        x.PharmacyBillId ==
                        bill.Id &&
                        x.BranchId ==
                        branchId &&
                        x.Status ==
                        "Paid");

            if (existingPaidPayment)
            {
                throw new Exception(
                    "A successful payment already exists for this bill.");
            }

            _context.PharmacyPayments.Add(
                new PharmacyPayment
                {
                    PharmacyId =
                        pharmacyId,

                    BranchId =
                        branchId,

                    PharmacyBillId =
                        bill.Id,

                    PaymentMode =
                        dto.PaymentMode.Trim(),

                    TransactionId =
                        dto.TransactionId?.Trim(),

                    PaidAmount =
                        dto.PaidAmount,

                    Status =
                        "Paid",

                    PaymentDate =
                        DateTime.UtcNow
                });

            bill.PaymentStatus =
                "Paid";

            await _context.SaveChangesAsync();

            await transaction.CommitAsync();

            return
                "Payment completed successfully.";
        }
        catch (DbUpdateConcurrencyException)
        {
            await transaction.RollbackAsync();
            throw new InvalidOperationException("Stock changed while this prescription was being dispensed. Refresh inventory and retry.");
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    // =====================================================
    // GET PHARMACY INVOICE
    // =====================================================

    public async Task<PharmacyInvoiceDto?>
        GetInvoiceAsync(
            int billId,
            int pharmacyId,
            int branchId)
    {
        var scopedBranch =
            await GetActiveBranchAsync(
                pharmacyId,
                branchId);

        return await _context.PharmacyBills
            .AsNoTracking()
            .Where(x =>
                x.Id ==
                billId &&
                x.BranchId ==
                branchId)
            .Select(x =>
                new PharmacyInvoiceDto
                {
                    BillId =
                        x.Id,

                    InvoiceNumber =
                        $"PH-{x.Id:D6}",

                    BillDate =
                        x.BillDate,

                    PatientName =
                        x.Prescription!.Patient.Name,

                    DoctorName =
                        x.Prescription.Doctor.Name ??
                        string.Empty,

                    PaymentStatus =
                        x.PaymentStatus,

                    SubTotal =
                        x.SubTotal,

                    GST =
                        x.GSTAmount,

                    Discount =
                        x.Discount,

                    GrandTotal =
                        x.GrandTotal,

                    Medicines =
                        x.BillItems
                            .Select(i =>
                                new PharmacyInvoiceItemDto
                                {
                                    MedicineName =
                                        i.MedicineName,

                                    Quantity =
                                        i.Quantity,

                                    Rate =
                                        i.Rate,

                                    GST =
                                        i.GST,

                                    Total =
                                        i.Total
                                })
                            .ToList()
                })
            .FirstOrDefaultAsync();
    }

    // =====================================================
    // GET DAILY SALES REPORT
    // =====================================================

    public async Task<DailySalesReportDto>
        GetDailySalesReportAsync(
            DateTime date,
            int pharmacyId,
            int branchId)
    {
        var scopedBranch =
            await GetActiveBranchAsync(
                pharmacyId,
                branchId);

        var startDate =
            date.Date;

        var endDate =
            startDate.AddDays(1);

        // Revenue/sales should use successful paid bills.
        var bills =
            await _context.PharmacyBills
                .AsNoTracking()
                .Where(x =>
                    x.BranchId ==
                    branchId &&
                    x.BillDate >=
                    startDate &&
                    x.BillDate <
                    endDate &&
                    x.PaymentStatus ==
                    "Paid")
                .ToListAsync();

        return new DailySalesReportDto
        {
            Date =
                startDate,

            TotalBills =
                bills.Count,

            TotalSales =
                bills.Sum(x =>
                    x.SubTotal),

            TotalGST =
                bills.Sum(x =>
                    x.GSTAmount),

            TotalDiscount =
                bills.Sum(x =>
                    x.Discount),

            NetSales =
                bills.Sum(x =>
                    x.GrandTotal)
        };
    }
}