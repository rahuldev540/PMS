using AuthDemo.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AuthDemo.Helpers;

namespace AuthDemo.Controllers;

[ApiController]
[Route("api/pharmacy-admin/dashboard")]
[Authorize(Roles = "PharmacyAdmin")]
public sealed class PharmacyAdminDashboardController : PharmacyApiControllerBase
{
    private readonly AppDbContext _db;
    private readonly PharmacyPermissionChecker _permissionChecker;

    public PharmacyAdminDashboardController(AppDbContext db, PharmacyPermissionChecker permissionChecker)
    {
        _db = db;
        _permissionChecker = permissionChecker;
    }

    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken cancellationToken)
    {
        if (!await _permissionChecker.HasPermissionAsync(UserId, "Dashboard", "View"))
            return StatusCode(StatusCodes.Status403Forbidden, new { Success = false, Message = "You do not have View permission for Dashboard." });

        if (!IsPharmacyAssigned)
        {
            var pendingPharmacists = await _db.Users
                .AsNoTracking()
                .Where(x => x.Role == "Pharmacist" && x.CreatedByUserId == UserId)
                .OrderByDescending(x => x.CreatedAt)
                .Select(x => new
                {
                    x.Id,
                    x.Name,
                    x.Email,
                    x.MobileNumber,
                    x.AccountStatus,
                    x.CreatedAt
                })
                .ToListAsync(cancellationToken);

            return Ok(new
            {
                Success = true,
                Role = "PharmacyAdmin",
                AccountStatus = "PendingPharmacyAssignment",
                IsPharmacyAssigned = false,
                CanAccessPharmacyOperations = false,
                Message = "Create your pharmacy to activate pharmacy operations.",
                CreatedPharmacists = pendingPharmacists,
                AvailableOperations = new[]
                {
                    "Login and change password",
                    "Check assignment status",
                    "Create pending Pharmacists",
                    "View Pharmacists created by this Admin"
                }
            });
        }

        var pharmacyId = PharmacyId;
        var branchId = BranchId;
        // Verify the assigned branch, then aggregate operational data across the admin's pharmacy.
        if (!await _db.Branches.AsNoTracking().AnyAsync(
                b => b.Id == branchId && b.PharmacyId == pharmacyId, cancellationToken))
            return StatusCode(StatusCodes.Status403Forbidden,
                new { Success = false, Message = "Assigned branch does not belong to this pharmacy." });

        var today = DateTime.UtcNow.Date;

        var inventory = _db.Inventories.AsNoTracking()
            .Where(x => x.PharmacyId == pharmacyId);
        var todayBills = _db.PharmacyBills.AsNoTracking()
            .Where(x => x.PharmacyId == pharmacyId && x.BillDate >= today);

        var lowStock = await inventory
            .Where(x => x.CurrentStock <= x.ReorderLevel)
            .OrderBy(x => x.CurrentStock)
            .Take(10)
            .Select(x => new
            {
                x.MedicineId,
                Medicine = x.Medicine.Name,
                x.CurrentStock,
                x.AvailableStock,
                x.ReorderLevel
            })
            .ToListAsync(cancellationToken);

        var recentPrescriptions = await _db.Prescriptions
            .AsNoTracking()
            .Where(x => x.PharmacyId == pharmacyId)
            .OrderByDescending(x => x.PrescriptionDate)
            .Take(10)
            .Select(x => new
            {
                x.Id,
                x.PatientId,
                PatientName = x.Patient != null ? x.Patient.Name : null,
                x.DoctorId,
                DoctorName = x.Doctor != null ? x.Doctor.Name : null,
                x.Status,
                x.PrescriptionDate
            })
            .ToListAsync(cancellationToken);

        var salesOverview = await todayBills
            .Where(x => x.PaymentStatus == "Paid")
            .GroupBy(x => x.BillDate.Hour)
            .Select(g => new
            {
                Hour = g.Key,
                Sales = g.Sum(x => x.GrandTotal),
                Bills = g.Count()
            })
            .OrderBy(x => x.Hour)
            .ToListAsync(cancellationToken);

        var topMedicines = await _db.PharmacyBillItems
            .AsNoTracking()
            .Where(x => x.PharmacyBill.PharmacyId == pharmacyId &&
                        x.PharmacyBill.BillDate >= today &&
                        x.PharmacyBill.PaymentStatus == "Paid")
            .GroupBy(x => new { x.MedicineId, x.MedicineName })
            .Select(g => new
            {
                g.Key.MedicineId,
                Medicine = g.Key.MedicineName,
                Quantity = g.Sum(x => x.Quantity),
                Sales = g.Sum(x => x.Total)
            })
            .OrderByDescending(x => x.Quantity)
            .Take(10)
            .ToListAsync(cancellationToken);

        var expiryAlerts = await _db.MedicineBatches
            .AsNoTracking()
            .Where(x => x.Inventory.PharmacyId == pharmacyId &&
                        x.RemainingQuantity > 0 &&
                        x.ExpiryDate <= today.AddDays(30))
            .OrderBy(x => x.ExpiryDate)
            .Take(10)
            .Select(x => new
            {
                x.Inventory.MedicineId,
                Medicine = x.Inventory.Medicine.Name,
                x.BatchNumber,
                Quantity = x.RemainingQuantity,
                x.ExpiryDate,
                DaysLeft = EF.Functions.DateDiffDay(today, x.ExpiryDate),
                Status = x.ExpiryDate <= today ? "Expired" : "NearExpiry"
            })
            .ToListAsync(cancellationToken);

        // Resolve branch IDs from the pharmacy-specific identity table, not global DB IDs.
        var branchSales = await _db.PharmacyBills.AsNoTracking()
            .Where(x => x.PharmacyId == pharmacyId && x.PaymentStatus == "Paid")
            .GroupBy(x => x.BranchId)
            .Select(g => new { InternalBranchId = g.Key, TotalSales = g.Sum(x => x.GrandTotal), TotalBills = g.Count() })
            .ToListAsync(cancellationToken);
        var branchRows = await _db.Branches.AsNoTracking()
            .Where(x => x.PharmacyId == pharmacyId)
            .Select(x => new { x.Id, x.Name })
            .ToListAsync(cancellationToken);
        var branchIdentityRows = await _db.PharmacyBranchIdentities.AsNoTracking()
            .Where(x => x.PharmacyId == pharmacyId)
            .Select(x => new { x.ExistingBranchId, x.BranchId })
            .ToListAsync(cancellationToken);
        var branchPerformance = branchRows.Select(branch =>
        {
            var publicBranch = branchIdentityRows.SingleOrDefault(x => x.ExistingBranchId == branch.Id)
                ?? throw new InvalidOperationException($"Missing public branch identity for internal branch {branch.Id}.");
            var sales = branchSales.FirstOrDefault(x => x.InternalBranchId == branch.Id);
            return new { BranchId = publicBranch.BranchId, BranchName = branch.Name,
                TotalSales = sales?.TotalSales ?? 0m, TotalBills = sales?.TotalBills ?? 0 };
        }).OrderBy(x => x.BranchId).ToList();

        return Ok(new
        {
            Success = true,
            Role = "PharmacyAdmin",
            AccountStatus = "Active",
            IsPharmacyAssigned = true,
            PharmacyId = pharmacyId,
            BranchId = await PharmacyPublicBranchMapper.GetPublicBranchIdAsync(_db, pharmacyId, branchId, cancellationToken),
            TotalBranches = await _db.Branches.CountAsync(x => x.PharmacyId == pharmacyId, cancellationToken),
            BranchPerformance = branchPerformance,
            Summary = new
            {
                TotalPharmacists = await _db.Users.CountAsync(x =>
                    x.Role == "Pharmacist" && x.PharmacyId == pharmacyId, cancellationToken),
                PendingPharmacistsCreatedByMe = await _db.Users.CountAsync(x =>
                    x.Role == "Pharmacist" && x.CreatedByUserId == UserId &&
                    x.AccountStatus == "PendingPharmacyAssignment", cancellationToken),
                TotalMedicines = await _db.Medicines.CountAsync(x =>
                    x.PharmacyId == pharmacyId && x.IsActive, cancellationToken),
                TotalStockUnits = await inventory.Select(x => (int?)x.CurrentStock).SumAsync(cancellationToken) ?? 0,
                LowStockMedicines = await inventory.CountAsync(x =>
                    x.CurrentStock > 0 && x.CurrentStock <= x.ReorderLevel, cancellationToken),
                OutOfStockMedicines = await inventory.CountAsync(x => x.CurrentStock <= 0, cancellationToken),
                PendingPrescriptions = await _db.Prescriptions.CountAsync(x =>
                    x.PharmacyId == pharmacyId && x.Status == "Completed", cancellationToken),
                TodayBills = await todayBills.CountAsync(cancellationToken),
                TodaySales = await todayBills.Where(x => x.PaymentStatus == "Paid")
                    .Select(x => (decimal?)x.GrandTotal).SumAsync(cancellationToken) ?? 0,
                PendingPurchaseOrders = await _db.PurchaseOrders.CountAsync(x =>
                    x.PharmacyId == pharmacyId && x.Status == "Pending", cancellationToken),
                Prescriptions = await _db.Prescriptions.CountAsync(x =>
                    x.PharmacyId == pharmacyId, cancellationToken),
                ExpiryAlerts = await _db.MedicineBatches.CountAsync(x =>
                    x.Inventory.PharmacyId == pharmacyId &&
                    x.RemainingQuantity > 0 && x.ExpiryDate <= today.AddDays(30), cancellationToken)
            },
            SalesOverview = salesOverview,
            TopMedicines = topMedicines,
            ExpiryAlerts = expiryAlerts,
            LowStock = lowStock,
            RecentPrescriptions = recentPrescriptions,
            AvailableOperations = new[]
            {
                "Manage Pharmacists and permissions",
                "Manage medicines, suppliers and purchase orders",
                "Manage inventory, batches, adjustments and transfers",
                "View prescriptions, bills, payments, reports and audit logs"
            },
            CrudEndpoints = new
            {
                Pharmacists = "/api/pharmacy-admin/pharmacists",
                Medicines = "/api/medicine",
                Suppliers = "/api/suppliers",
                PurchaseOrders = "/api/purchase-orders",
                Inventory = "/api/inventory",
                InventoryOperations = "/api/inventory",
                Reports = "/api/pharmacy/reports"
            }
        });
    }
}

[ApiController]
[Route("api/pharmacist/dashboard")]
[Authorize(Roles = "Pharmacist")]
public sealed class PharmacistDashboardController : PharmacyApiControllerBase
{
    private readonly AppDbContext _db;

    public PharmacistDashboardController(AppDbContext db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken cancellationToken)
    {
        var dashboardPermission = await _db.UserPermissions
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.UserId == UserId && x.Module == "Dashboard", cancellationToken);

        if (dashboardPermission?.CanView != true)
            return StatusCode(StatusCodes.Status403Forbidden, new { Success = false, Message = "You do not have View permission for Dashboard." });

        if (!IsPharmacyAssigned)
        {
            return Ok(new
            {
                Success = true,
                Role = "Pharmacist",
                AccountStatus = "PendingPharmacyAssignment",
                IsPharmacyAssigned = false,
                CanAccessPharmacyOperations = false,
                Message = "Your Pharmacy Admin must assign you to a pharmacy branch.",
                AvailableOperations = new[]
                {
                    "Login and change password",
                    "Check assignment status"
                }
            });
        }

        var pharmacyId = PharmacyId;
        var branchId = BranchId;
        if (!await _db.Branches.AsNoTracking().AnyAsync(
                b => b.Id == branchId && b.PharmacyId == pharmacyId, cancellationToken))
            return StatusCode(StatusCodes.Status403Forbidden,
                new { Success = false, Message = "Assigned branch does not belong to this pharmacy." });

        var today = DateTime.UtcNow.Date;

        var permissions = await _db.UserPermissions
            .AsNoTracking()
            .Where(x => x.UserId == UserId)
            .OrderBy(x => x.Module)
            .Select(x => new
            {
                x.Module,
                x.CanView,
                x.CanCreate,
                x.CanEdit,
                x.CanDelete
            })
            .ToListAsync(cancellationToken);

        var pendingPrescriptions = await _db.Prescriptions
            .AsNoTracking()
            .Where(x => x.PharmacyId == pharmacyId && x.BranchId == branchId && x.Status == "Completed")
            .OrderByDescending(x => x.PrescriptionDate)
            .Take(10)
            .Select(x => new
            {
                x.Id,
                x.PatientId,
                PatientName = x.Patient != null ? x.Patient.Name : null,
                x.DoctorId,
                DoctorName = x.Doctor != null ? x.Doctor.Name : null,
                MedicineCount = x.Medicines.Count,
                x.PrescriptionDate,
                x.Status
            })
            .ToListAsync(cancellationToken);

        var inventory = _db.Inventories.AsNoTracking()
            .Where(x => x.PharmacyId == pharmacyId && x.BranchId == branchId);

        return Ok(new
        {
            Success = true,
            Role = "Pharmacist",
            AccountStatus = "Active",
            IsPharmacyAssigned = true,
            PharmacyId = pharmacyId,
            BranchId = await PharmacyPublicBranchMapper.GetPublicBranchIdAsync(_db, pharmacyId, branchId, cancellationToken),
            Summary = new
            {
                PendingPrescriptions = await _db.Prescriptions.CountAsync(x =>
                    x.PharmacyId == pharmacyId && x.BranchId == branchId && x.Status == "Completed", cancellationToken),
                PartiallyDispensedPrescriptions = await _db.Prescriptions.CountAsync(x =>
                    x.PharmacyId == pharmacyId && x.BranchId == branchId && x.Status == "PartiallyDispensed", cancellationToken),
                TodayDispenseTransactions = await _db.StockTransactions.CountAsync(x =>
                    x.PharmacyId == pharmacyId && x.BranchId == branchId &&
                    x.CreatedByUserId == UserId && x.TransactionType == "Dispense" &&
                    x.TransactionDate >= today, cancellationToken),
                TodayDispensedUnits = await _db.StockTransactions
                    .Where(x => x.PharmacyId == pharmacyId && x.BranchId == branchId &&
                                x.CreatedByUserId == UserId && x.TransactionType == "Dispense" &&
                                x.TransactionDate >= today)
                    .Select(x => (int?)x.Quantity).SumAsync(cancellationToken) ?? 0,
                AvailableMedicines = await inventory.CountAsync(x => x.AvailableStock > 0, cancellationToken),
                LowStockMedicines = await inventory.CountAsync(x =>
                    x.CurrentStock > 0 && x.CurrentStock <= x.ReorderLevel, cancellationToken),
                OutOfStockMedicines = await inventory.CountAsync(x => x.CurrentStock <= 0, cancellationToken)
            },
            Permissions = permissions,
            PendingPrescriptions = pendingPrescriptions,
            AvailableOperations = new[]
            {
                "View available medicines",
                "View and dispense pending prescriptions",
                "Generate bills and record payments",
                "View invoices and daily sales"
            },
            CrudEndpoints = new
            {
                AvailableMedicines = "GET /api/medicine/available",
                PendingPrescriptions = "GET /api/pharmacy/pending",
                Prescription = "GET /api/pharmacy/prescriptions/{id}",
                Dispense = "POST /api/pharmacy/dispense",
                GenerateBill = "POST /api/pharmacy/generate-bill",
                Payment = "POST /api/pharmacy/payment",
                Invoice = "GET /api/pharmacy/invoice/{billId}"
            }
        });
    }
}