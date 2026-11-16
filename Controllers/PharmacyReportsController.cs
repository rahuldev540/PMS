using AuthDemo.Data;
using AuthDemo.Helpers;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AuthDemo.Controllers;

[ApiController]
[Route("api/pharmacy")]
[Authorize(Roles = "PharmacyAdmin")]
public class PharmacyReportsController : PharmacyApiControllerBase
{
    private readonly AppDbContext _db;
    private readonly PharmacyPermissionChecker _permissionChecker;

    public PharmacyReportsController(
        AppDbContext db,
        PharmacyPermissionChecker permissionChecker)
    {
        _db = db;
        _permissionChecker = permissionChecker;
    }

    // =====================================================
    // COMMON PERMISSION CHECK
    // =====================================================

    private async Task<IActionResult?> CheckPermission(
        string module,
        string action)
    {
        var allowed =
            await _permissionChecker.HasPermissionAsync(
                UserId,
                module,
                action);

        if (!allowed)
        {
            return StatusCode(
                StatusCodes.Status403Forbidden,
                new
                {
                    Success = false,
                    Message =
                        $"You do not have {action} permission for {module}."
                });
        }

        return null;
    }

    // =====================================================
    // PHARMACY ADMIN DASHBOARD
    // GET /api/pharmacy/dashboard
    //
    // Dashboard -> View
    // =====================================================

    // Pharmacy-wide by default. branchId is the PUBLIC, pharmacy-scoped branch number.
    // Range uses UTC calendar dates, consistently with BillDate timestamps.
    [HttpGet("dashboard")]
    public async Task<IActionResult> Dashboard(
        [FromQuery] string range = "today",
        [FromQuery] int? branchId = null,
        CancellationToken cancellationToken = default)
    {
        var permissionError = await CheckPermission("Dashboard", "View");
        if (permissionError != null) return permissionError;

        range = range.Trim().ToLowerInvariant();
        if (range != "today" && range != "7d" && range != "30d")
            return BadRequest(new { success = false, message = "range must be today, 7d or 30d." });
        if (branchId.HasValue && branchId.Value <= 0)
            return BadRequest(new { success = false, message = "branchId must be positive." });

        var pharmacyId = PharmacyId;
        // Resolve tenant from the logged-in Admin's DB assignment as well as JWT.
        if (!await _db.Users.AsNoTracking().AnyAsync(u => u.Id == UserId &&
                u.PharmacyId == pharmacyId && u.Role == "PharmacyAdmin", cancellationToken))
            return StatusCode(403, new { success = false, message = "Pharmacy assignment is invalid." });

        int? internalBranchId = null;
        if (branchId.HasValue)
        {
            internalBranchId = await _db.PharmacyBranchIdentities.AsNoTracking()
                .Where(x => x.PharmacyId == pharmacyId && x.BranchId == branchId.Value)
                .Select(x => (int?)x.ExistingBranchId).FirstOrDefaultAsync(cancellationToken);
            if (!internalBranchId.HasValue || !await _db.Branches.AsNoTracking()
                    .AnyAsync(x => x.Id == internalBranchId.Value && x.PharmacyId == pharmacyId, cancellationToken))
                return NotFound(new { success = false, message = "Branch not found in your pharmacy." });
        }

        var today = DateTime.UtcNow.Date;
        var days = range == "today" ? 1 : range == "7d" ? 7 : 30;
        var from = today.AddDays(1 - days);
        var until = today.AddDays(1);
        var previousFrom = from.AddDays(-days);
        var billQuery = _db.PharmacyBills.AsNoTracking().Where(x =>
            x.PharmacyId == pharmacyId && (!internalBranchId.HasValue || x.BranchId == internalBranchId.Value) &&
            x.PaymentStatus == "Paid");
        var currentBills = billQuery.Where(x => x.BillDate >= from && x.BillDate < until);
        var previousBills = billQuery.Where(x => x.BillDate >= previousFrom && x.BillDate < from);
        var periodSales = await currentBills.SumAsync(x => (decimal?)x.GrandTotal, cancellationToken) ?? 0m;
        var previousSales = await previousBills.SumAsync(x => (decimal?)x.GrandTotal, cancellationToken) ?? 0m;
        decimal? salesChange = previousSales == 0m ? null :
            Math.Round((periodSales - previousSales) * 100m / previousSales, 2);
        var todaySales = await billQuery.Where(x => x.BillDate >= today && x.BillDate < until)
            .SumAsync(x => (decimal?)x.GrandTotal, cancellationToken) ?? 0m;

        var inventory = _db.Inventories.AsNoTracking().Where(x =>
            x.PharmacyId == pharmacyId && (!internalBranchId.HasValue || x.BranchId == internalBranchId.Value));
        var lowStock = await inventory.CountAsync(x => x.CurrentStock > 0 &&
            x.CurrentStock <= x.ReorderLevel, cancellationToken);
        var outOfStock = await inventory.CountAsync(x => x.CurrentStock <= 0, cancellationToken);
        var stockUnits = await inventory.SumAsync(x => (int?)x.CurrentStock, cancellationToken) ?? 0;

        var batches = _db.MedicineBatches.AsNoTracking().Where(x =>
            x.Inventory.PharmacyId == pharmacyId &&
            (!internalBranchId.HasValue || x.Inventory.BranchId == internalBranchId.Value) &&
            x.RemainingQuantity > 0 && !x.IsQuarantined && !x.IsRecalled);
        var settings = await _db.PharmacyBranchSettings.AsNoTracking()
            .Where(x => x.PharmacyId == pharmacyId &&
                (!internalBranchId.HasValue || x.BranchId == internalBranchId.Value))
            .Select(x => new { x.BranchId, x.ExpiryAlertHorizonDays })
            .ToListAsync(cancellationToken);
        // Use each branch's configured horizon; 30 days is the default.
        var branchHorizons = settings.ToDictionary(x => x.BranchId,
            x => Math.Clamp(x.ExpiryAlertHorizonDays, 0, 365));
        var expiringCandidates = await batches.Where(x => x.ExpiryDate >= today &&
                x.ExpiryDate < today.AddDays(366))
            .Select(x => new { x.Inventory.BranchId, x.ExpiryDate })
            .ToListAsync(cancellationToken);
        var expiryAlerts = expiringCandidates.Count(x => x.ExpiryDate <
            today.AddDays(branchHorizons.GetValueOrDefault(x.BranchId, 30) + 1));
        var expiredBatches = await batches.CountAsync(x => x.ExpiryDate < today, cancellationToken);

        var groupedSales = await currentBills.GroupBy(x => x.BillDate.Date)
            .Select(g => new { Date = g.Key, Sales = g.Sum(x => x.GrandTotal), Transactions = g.Count() })
            .ToListAsync(cancellationToken);
        var salesOverview = Enumerable.Range(0, days).Select(i =>
        {
            var date = from.AddDays(i);
            var point = groupedSales.FirstOrDefault(x => x.Date == date);
            return new { Date = date, Sales = point?.Sales ?? 0m, Transactions = point?.Transactions ?? 0 };
        }).ToList();

        var topMedicines = await _db.PharmacyBillItems.AsNoTracking()
            .Where(x => x.PharmacyBill.PharmacyId == pharmacyId &&
                (!internalBranchId.HasValue || x.PharmacyBill.BranchId == internalBranchId.Value) &&
                x.PharmacyBill.PaymentStatus == "Paid" &&
                x.PharmacyBill.BillDate >= from && x.PharmacyBill.BillDate < until)
            .GroupBy(x => new { x.MedicineId, x.MedicineName })
            .Select(g => new { g.Key.MedicineId, MedicineName = g.Key.MedicineName,
                QuantitySold = g.Sum(x => x.Quantity), Revenue = g.Sum(x => x.Total) })
            .OrderByDescending(x => x.QuantitySold).Take(10).ToListAsync(cancellationToken);

        var recentStock = await _db.StockTransactions.AsNoTracking()
            .Where(x => x.PharmacyId == pharmacyId &&
                (!internalBranchId.HasValue || x.BranchId == internalBranchId.Value))
            .OrderByDescending(x => x.TransactionDate).ThenByDescending(x => x.Id)
            .Take(10).Select(x => new { x.MedicineId, x.MedicineBatchId,
                x.TransactionType, x.Quantity, x.BalanceStock, x.TransactionDate })
            .ToListAsync(cancellationToken);

        var recentBatchIds = recentStock.Where(x => x.MedicineBatchId.HasValue)
            .Select(x => x.MedicineBatchId!.Value).Distinct().ToArray();
        var batchDetails = await _db.MedicineBatches.AsNoTracking()
            .Where(x => recentBatchIds.Contains(x.Id) && x.Inventory.PharmacyId == pharmacyId &&
                (!internalBranchId.HasValue || x.Inventory.BranchId == internalBranchId.Value))
            .Select(x => new { x.Id, x.BatchNumber, x.ExpiryDate, MedicineName = x.Inventory.Medicine.Name })
            .ToListAsync(cancellationToken);
        var recentStockDetails = recentStock.Select(x =>
        {
            var batch = batchDetails.FirstOrDefault(b => b.Id == x.MedicineBatchId);
            return new { x.MedicineId, MedicineName = batch?.MedicineName,
                BatchNumber = batch?.BatchNumber, Quantity = x.Quantity,
                ExpiryDate = batch?.ExpiryDate, Status = x.TransactionType,
                x.BalanceStock, x.TransactionDate };
        }).ToList();

        var branches = await _db.Branches.AsNoTracking().Where(x => x.PharmacyId == pharmacyId)
            .Join(_db.PharmacyBranchIdentities.AsNoTracking().Where(x => x.PharmacyId == pharmacyId),
                b => b.Id, i => i.ExistingBranchId,
                (b, i) => new { BranchId = i.BranchId, BranchName = b.Name })
            .OrderBy(x => x.BranchId).ToListAsync(cancellationToken);

        return Ok(new
        {
            success = true, pharmacyId, branchId, range,
            availableBranches = branches,
            todaySales, periodSales, salesChange,
            lowStock, lowStockChange = (int?)null,
            expiryAlerts, expiryChange = (int?)null,
            salesOverview, topMedicines, recentStock = recentStockDetails,
            reports = Array.Empty<object>(),
            summary = new { todaySales, periodSales, lowStockMedicines = lowStock,
                outOfStockMedicines = outOfStock, totalStockUnits = stockUnits,
                nearExpiryBatches = expiryAlerts, expiredBatches,
                totalMedicines = await inventory.CountAsync(cancellationToken),
                todayBills = await billQuery.CountAsync(x => x.BillDate >= today && x.BillDate < until, cancellationToken) }
        });
    }

    // =====================================================
    // SALES REPORT
    // GET /api/pharmacy/reports/sales
    //
    // Reports -> View
    // =====================================================

    [HttpGet("reports/sales")]
    public async Task<IActionResult> Sales(
        [FromQuery] DateTime from,
        [FromQuery] DateTime to)
    {
        var permissionError =
            await CheckPermission(
                "Reports",
                "View");

        if (permissionError != null)
            return permissionError;

        if (to.Date < from.Date)
        {
            return BadRequest(new
            {
                Success = false,
                Message =
                    "End date cannot be earlier than start date."
            });
        }

        var endDate =
            to.Date.AddDays(1);

        var items =
            await _db.PharmacyBills
                .AsNoTracking()
                .Where(x =>
                    x.PharmacyId == PharmacyId &&
                    x.BranchId == BranchId &&
                    x.BillDate >= from &&
                    x.BillDate < endDate)
                .GroupBy(x =>
                    x.BillDate.Date)
                .Select(g => new
                {
                    Date =
                        g.Key,

                    Bills =
                        g.Count(),

                    Gross =
                        g.Sum(x =>
                            x.SubTotal),

                    GST =
                        g.Sum(x =>
                            x.GSTAmount),

                    Discount =
                        g.Sum(x =>
                            x.Discount),

                    Net =
                        g.Sum(x =>
                            x.GrandTotal)
                })
                .OrderBy(x =>
                    x.Date)
                .ToListAsync();

        return Ok(new
        {
            Success = true,
            From = from.Date,
            To = to.Date,
            Items = items
        });
    }

    // =====================================================
    // STOCK SUMMARY REPORT
    // GET /api/pharmacy/reports/stock-summary
    //
    // Reports -> View
    // =====================================================

    [HttpGet("reports/stock-summary")]
    public async Task<IActionResult> StockSummary()
    {
        var permissionError =
            await CheckPermission(
                "Reports",
                "View");

        if (permissionError != null)
            return permissionError;

        var items =
            await _db.Inventories
                .AsNoTracking()
                .Where(x =>
                    x.PharmacyId == PharmacyId &&
                    x.BranchId == BranchId)
                .Select(x => new
                {
                    x.MedicineId,

                    x.Medicine.MedicineCode,

                    x.Medicine.Name,

                    x.CurrentStock,

                    x.AvailableStock,

                    x.ReservedStock,

                    x.ReorderLevel,

                    Status =
                        x.CurrentStock <= 0
                            ? "OutOfStock"
                            : x.CurrentStock <=
                              x.ReorderLevel
                                ? "LowStock"
                                : "Available"
                })
                .OrderBy(x =>
                    x.Name)
                .ToListAsync();

        return Ok(new
        {
            Success = true,
            Items = items
        });
    }

    // =====================================================
    // STOCK MOVEMENT REPORT
    // GET /api/pharmacy/reports/stock-movement
    //
    // Reports -> View
    // =====================================================

    [HttpGet("reports/stock-movement")]
    public async Task<IActionResult> StockMovement(
        [FromQuery] DateTime from,
        [FromQuery] DateTime to)
    {
        var permissionError =
            await CheckPermission(
                "Reports",
                "View");

        if (permissionError != null)
            return permissionError;

        if (to.Date < from.Date)
        {
            return BadRequest(new
            {
                Success = false,
                Message =
                    "End date cannot be earlier than start date."
            });
        }

        var endDate =
            to.Date.AddDays(1);

        var items =
            await _db.StockTransactions
                .AsNoTracking()
                .Where(x =>
                    x.PharmacyId == PharmacyId &&
                    x.BranchId == BranchId &&
                    x.TransactionDate >= from &&
                    x.TransactionDate < endDate)
                .GroupBy(x => new
                {
                    x.MedicineId,
                    x.TransactionType
                })
                .Select(g => new
                {
                    g.Key.MedicineId,

                    g.Key.TransactionType,

                    Quantity =
                        g.Sum(x =>
                            x.Quantity)
                })
                .OrderBy(x =>
                    x.MedicineId)
                .ToListAsync();

        return Ok(new
        {
            Success = true,
            From = from.Date,
            To = to.Date,
            Items = items
        });
    }

    // =====================================================
    // EXPIRY REPORT
    // GET /api/pharmacy/reports/expiry
    //
    // Reports -> View
    // =====================================================

    [HttpGet("reports/expiry")]
    public async Task<IActionResult> Expiry(
        [FromQuery] int days = 30)
    {
        var permissionError =
            await CheckPermission(
                "Reports",
                "View");

        if (permissionError != null)
            return permissionError;

        days =
            Math.Clamp(
                days,
                1,
                365);

        var today =
            DateTime.UtcNow.Date;

        var until =
            today.AddDays(days);

        var items =
            await _db.MedicineBatches
                .AsNoTracking()
                .Where(x =>
                    x.Inventory.PharmacyId ==
                    PharmacyId &&
                    x.Inventory.BranchId ==
                    BranchId &&
                    x.RemainingQuantity > 0 &&
                    x.ExpiryDate <= until)
                .Select(x => new
                {
                    x.Inventory.MedicineId,

                    x.Inventory.Medicine.Name,

                    x.BatchNumber,

                    x.ExpiryDate,

                    x.RemainingQuantity,

                    Quantity = x.RemainingQuantity,

                    DaysLeft = EF.Functions.DateDiffDay(today, x.ExpiryDate),

                    Status =
                        x.ExpiryDate <= today
                            ? "Expired"
                            : "NearExpiry"
                })
                .OrderBy(x =>
                    x.ExpiryDate)
                .ToListAsync();

        return Ok(new
        {
            Success = true,
            Days = days,
            Items = items
        });
    }

    // =====================================================
    // PURCHASE REPORT
    // GET /api/pharmacy/reports/purchases
    //
    // Reports -> View
    // =====================================================

    [HttpGet("reports/purchases")]
    public async Task<IActionResult> Purchases(
        [FromQuery] DateTime from,
        [FromQuery] DateTime to)
    {
        var permissionError =
            await CheckPermission(
                "Reports",
                "View");

        if (permissionError != null)
            return permissionError;

        if (to.Date < from.Date)
        {
            return BadRequest(new
            {
                Success = false,
                Message =
                    "End date cannot be earlier than start date."
            });
        }

        var endDate =
            to.Date.AddDays(1);

        var items =
            await _db.PurchaseOrders
                .AsNoTracking()
                .Where(x =>
                    x.PharmacyId == PharmacyId &&
                    x.BranchId == BranchId &&
                    x.OrderDate >= from &&
                    x.OrderDate < endDate)
                .Select(x => new
                {
                    x.Id,

                    x.OrderNumber,

                    Supplier =
                        x.Supplier.Name,

                    x.OrderDate,

                    x.Status,

                    Total =
                        x.Items.Sum(i =>
                            i.OrderedQuantity *
                            i.UnitCost)
                })
                .OrderByDescending(x =>
                    x.OrderDate)
                .ToListAsync();

        return Ok(new
        {
            Success = true,
            From = from.Date,
            To = to.Date,
            Items = items
        });
    }

    // =====================================================
    // TOP SELLING REPORT
    // GET /api/pharmacy/reports/top-selling
    //
    // Reports -> View
    // =====================================================

    [HttpGet("reports/top-selling")]
    public async Task<IActionResult> TopSelling(
        [FromQuery] DateTime from,
        [FromQuery] DateTime to,
        [FromQuery] int take = 20)
    {
        var permissionError =
            await CheckPermission(
                "Reports",
                "View");

        if (permissionError != null)
            return permissionError;

        if (to.Date < from.Date)
        {
            return BadRequest(new
            {
                Success = false,
                Message =
                    "End date cannot be earlier than start date."
            });
        }

        take =
            Math.Clamp(
                take,
                1,
                100);

        var endDate =
            to.Date.AddDays(1);

        var items =
            await _db.PharmacyBillItems
                .AsNoTracking()
                .Where(x =>
                    x.PharmacyBill.PharmacyId ==
                    PharmacyId &&
                    x.PharmacyBill.BranchId ==
                    BranchId &&
                    x.PharmacyBill.BillDate >=
                    from &&
                    x.PharmacyBill.BillDate <
                    endDate)
                .GroupBy(x => new
                {
                    x.MedicineId,
                    x.MedicineName
                })
                .Select(g => new
                {
                    g.Key.MedicineId,

                    g.Key.MedicineName,

                    Quantity =
                        g.Sum(x =>
                            x.Quantity),

                    Sales =
                        g.Sum(x =>
                            x.Total)
                })
                .OrderByDescending(x =>
                    x.Quantity)
                .Take(take)
                .ToListAsync();

        return Ok(new
        {
            Success = true,
            From = from.Date,
            To = to.Date,
            Items = items
        });
    }

    // =====================================================
    // PAYMENT HISTORY
    // GET /api/pharmacy/payments
    //
    // Sales -> View
    // =====================================================

    [HttpGet("payments")]
    public async Task<IActionResult> Payments(
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to)
    {
        var permissionError =
            await CheckPermission(
                "Sales",
                "View");

        if (permissionError != null)
            return permissionError;

        if (from.HasValue &&
            to.HasValue &&
            to.Value.Date <
            from.Value.Date)
        {
            return BadRequest(new
            {
                Success = false,
                Message =
                    "End date cannot be earlier than start date."
            });
        }

        var query =
            _db.PharmacyPayments
                .AsNoTracking()
                .Where(x =>
                    x.PharmacyId == PharmacyId &&
                    x.BranchId == BranchId);

        if (from.HasValue)
        {
            query =
                query.Where(x =>
                    x.PaymentDate >=
                    from.Value);
        }

        if (to.HasValue)
        {
            var endDate =
                to.Value.Date.AddDays(1);

            query =
                query.Where(x =>
                    x.PaymentDate <
                    endDate);
        }

        var items =
            await query
                .OrderByDescending(x =>
                    x.PaymentDate)
                .ToListAsync();

        // Payments store internal BranchId as a foreign key. Convert only the
        // outbound response; do not mutate tracked entities or database values.
        var publicBranchId = await PharmacyPublicBranchMapper.GetPublicBranchIdAsync(
            _db, PharmacyId, BranchId);
        var publicItems = items.Select(x => new
        {
            x.Id,
            x.PharmacyId,
            BranchId = publicBranchId,
            x.PharmacyBillId,
            x.PaymentMode,
            x.TransactionId,
            x.PaidAmount,
            x.RefundedAmount,
            x.Status,
            x.ReceivedByUserId,
            x.PaymentDate
        }).ToList();

        return Ok(new
        {
            Success = true,
            Items = publicItems
        });
    }

    // =====================================================
    // DASHBOARD ALERTS
    // GET /api/pharmacy/alerts
    //
    // Dashboard -> View
    // =====================================================

    [HttpGet("alerts")]
    public async Task<IActionResult> Alerts()
    {
        var permissionError =
            await CheckPermission(
                "Dashboard",
                "View");

        if (permissionError != null)
            return permissionError;

        var today =
            DateTime.UtcNow.Date;

        var expiryLimit =
            today.AddDays(30);

        var low =
            await _db.Inventories
                .AsNoTracking()
                .Where(x =>
                    x.PharmacyId == PharmacyId &&
                    x.BranchId == BranchId &&
                    x.CurrentStock <=
                    x.ReorderLevel)
                .Select(x => new
                {
                    x.MedicineId,

                    Medicine =
                        x.Medicine.Name,

                    x.CurrentStock,

                    x.ReorderLevel
                })
                .ToListAsync();

        var expiry =
            await _db.MedicineBatches
                .AsNoTracking()
                .Where(x =>
                    x.Inventory.PharmacyId ==
                    PharmacyId &&
                    x.Inventory.BranchId ==
                    BranchId &&
                    x.RemainingQuantity > 0 &&
                    x.ExpiryDate <=
                    expiryLimit)
                .Select(x => new
                {
                    x.Inventory.MedicineId,

                    Medicine =
                        x.Inventory.Medicine.Name,

                    x.BatchNumber,

                    x.ExpiryDate
                })
                .ToListAsync();

        var result =
            low
                .Select(x => new
                {
                    Type =
                        "LowStock",

                    x.MedicineId,

                    x.Medicine,

                    Message =
                        $"Current stock {x.CurrentStock}; reorder level {x.ReorderLevel}"
                })
                .Cast<object>()
                .Concat(
                    expiry.Select(x => new
                    {
                        Type =
                            x.ExpiryDate <= today
                                ? "Expired"
                                : "Expiry",

                        x.MedicineId,

                        x.Medicine,

                        Message =
                            $"Batch {x.BatchNumber} expires {x.ExpiryDate:yyyy-MM-dd}"
                    }))
                .ToList();

        return Ok(new
        {
            Success = true,
            Total = result.Count,
            Items = result
        });
    }

    // =====================================================
    // PHARMACY ADMIN AUDIT LOGS
    // GET /api/pharmacy/audit-logs
    //
    // No Audit Logs module exists in the shared
    // Pharmacy Admin permission matrix, so this remains
    // protected by PharmacyAdmin role only.
    // =====================================================

    [HttpGet("audit-logs")]
    public async Task<IActionResult> AuditLogs(
        [FromQuery] int? userId,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to)
    {
        if (from.HasValue &&
            to.HasValue &&
            to.Value.Date <
            from.Value.Date)
        {
            return BadRequest(new
            {
                Success = false,
                Message =
                    "End date cannot be earlier than start date."
            });
        }

        var query =
            _db.AuditLogs
                .AsNoTracking()
                .Where(x =>
                    x.BranchId == BranchId &&
                    _db.Users.Any(u =>
                        u.Id == x.UserId &&
                        u.PharmacyId == PharmacyId));

        if (userId.HasValue)
        {
            query =
                query.Where(x =>
                    x.UserId ==
                    userId.Value);
        }

        if (from.HasValue)
        {
            query =
                query.Where(x =>
                    x.Timestamp >=
                    from.Value);
        }

        if (to.HasValue)
        {
            var endDate =
                to.Value.Date.AddDays(1);

            query =
                query.Where(x =>
                    x.Timestamp <
                    endDate);
        }

        var items =
            await query
                .OrderByDescending(x =>
                    x.Timestamp)
                .Take(1000)
                .ToListAsync();

        return Ok(new
        {
            Success = true,
            Total = items.Count,
            Items = items
        });
    }
}