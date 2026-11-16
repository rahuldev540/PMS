using AuthDemo.Data;
using AuthDemo.DTOs;
using AuthDemo.Helpers;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AuthDemo.Controllers;

[ApiController]
[Route("api/pharmacy")]
[Authorize(Roles = "PharmacyAdmin,Pharmacist")]
public class BillingOperationsController : PharmacyApiControllerBase
{
    private readonly AppDbContext _db;
    private readonly PharmacyPermissionChecker _permissionChecker;

    public BillingOperationsController(
        AppDbContext db,
        PharmacyPermissionChecker permissionChecker)
    {
        _db = db;
        _permissionChecker = permissionChecker;
    }

    // =====================================================
    // CHECK SALES PERMISSION
    //
    // PharmacyAdmin
    // -> PharmacyPermissionRole
    //
    // Pharmacist
    // -> UserPermission
    // =====================================================

    private async Task<IActionResult?> CheckSalesPermission(
        string action)
    {
        // =================================================
        // PHARMACY ADMIN
        // =================================================

        if (User.IsInRole("PharmacyAdmin"))
        {
            var allowed =
                await _permissionChecker.HasPermissionAsync(
                    UserId,
                    "Sales",
                    action);

            if (!allowed)
            {
                return StatusCode(
                    StatusCodes.Status403Forbidden,
                    new
                    {
                        Success = false,
                        Message =
                            $"You do not have {action} permission for Bills."
                    });
            }

            return null;
        }

        // =================================================
        // PHARMACIST
        // =================================================

        if (User.IsInRole("Pharmacist"))
        {
            var permission =
                await _db.UserPermissions
                    .AsNoTracking()
                    .FirstOrDefaultAsync(x =>
                        x.UserId == UserId &&
                        x.Module == "Bills");

            if (permission == null)
            {
                return StatusCode(
                    StatusCodes.Status403Forbidden,
                    new
                    {
                        Success = false,
                        Message =
                            "You do not have access to Bills."
                    });
            }

            var allowed =
                action.Trim()
                    .ToLowerInvariant() switch
                {
                    "view" =>
                        permission.CanView,

                    "create" =>
                        permission.CanCreate,

                    "edit" =>
                        permission.CanEdit,

                    "delete" =>
                        permission.CanDelete,

                    _ =>
                        false
                };

            if (!allowed)
            {
                return StatusCode(
                    StatusCodes.Status403Forbidden,
                    new
                    {
                        Success = false,
                        Message =
                            $"You do not have {action} permission for Bills."
                    });
            }

            return null;
        }

        return StatusCode(
            StatusCodes.Status403Forbidden,
            new
            {
                Success = false,
                Message =
                    "You do not have permission to access Sales."
            });
    }

    // =====================================================
    // BILL LIST
    // GET /api/pharmacy/bills
    //
    // Sales -> View
    // =====================================================

    [HttpGet("bills")]
    public async Task<IActionResult> Bills(
        [FromQuery] string? status,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to)
    {
        var permissionError =
            await CheckSalesPermission("View");

        if (permissionError != null)
            return permissionError;

        if (from.HasValue &&
            to.HasValue &&
            to.Value.Date < from.Value.Date)
        {
            return BadRequest(new
            {
                Success = false,
                Message =
                    "End date cannot be earlier than start date."
            });
        }

        var query =
            _db.PharmacyBills
                .AsNoTracking()
                .Where(x =>
                    x.PharmacyId == PharmacyId &&
                    x.BranchId == BranchId);

        if (!string.IsNullOrWhiteSpace(status))
        {
            var statusValue =
                status.Trim();

            query =
                query.Where(x =>
                    x.PaymentStatus ==
                    statusValue);
        }

        if (from.HasValue)
        {
            query =
                query.Where(x =>
                    x.BillDate >=
                    from.Value);
        }

        if (to.HasValue)
        {
            var endDate =
                to.Value.Date.AddDays(1);

            query =
                query.Where(x =>
                    x.BillDate <
                    endDate);
        }

        var bills =
            await query
                .OrderByDescending(x =>
                    x.BillDate)
                .Select(x => new
                {
                    x.Id,
                    x.PatientId,
                    x.PrescriptionId,
                    x.BillDate,
                    x.SubTotal,
                    x.GSTAmount,
                    x.Discount,
                    x.GrandTotal,
                    x.PaymentStatus
                })
                .ToListAsync();

        return Ok(new
        {
            Success = true,
            Total = bills.Count,
            Items = bills
        });
    }

    // =====================================================
    // BILL DETAILS
    // GET /api/pharmacy/bills/{id}
    //
    // Sales -> View
    // =====================================================

    [HttpGet("bills/{id:int}")]
    public async Task<IActionResult> Bill(
        int id)
    {
        var permissionError =
            await CheckSalesPermission("View");

        if (permissionError != null)
            return permissionError;

        if (id <= 0)
        {
            return BadRequest(new
            {
                Success = false,
                Message =
                    "A valid bill id is required."
            });
        }

        var bill =
            await _db.PharmacyBills
                .AsNoTracking()
                .Where(x =>
                    x.Id == id &&
                    x.PharmacyId ==
                    PharmacyId &&
                    x.BranchId ==
                    BranchId)
                .Select(x => new
                {
                    x.Id,
                    x.PatientId,
                    x.PrescriptionId,
                    x.BillDate,
                    x.SubTotal,
                    x.GSTAmount,
                    x.Discount,
                    x.GrandTotal,
                    x.PaymentStatus,

                    Items =
                        x.BillItems
                            .Select(i => new
                            {
                                i.Id,
                                i.MedicineId,
                                i.MedicineName,
                                i.Quantity,
                                i.Rate,
                                i.GST,
                                i.Total
                            })
                            .ToList(),

                    Payments =
                        _db.PharmacyPayments
                            .Where(p =>
                                p.PharmacyBillId ==
                                x.Id &&
                                p.PharmacyId ==
                                PharmacyId &&
                                p.BranchId ==
                                BranchId)
                            .Select(p => new
                            {
                                p.Id,
                                p.PaymentMode,
                                p.TransactionId,
                                p.PaidAmount,
                                p.RefundedAmount,
                                p.Status,
                                p.PaymentDate
                            })
                            .ToList()
                })
                .FirstOrDefaultAsync();

        if (bill == null)
        {
            return NotFound(new
            {
                Success = false,
                Message =
                    "Bill not found."
            });
        }

        return Ok(new
        {
            Success = true,
            Bill = bill
        });
    }

    // =====================================================
    // REFUND PAYMENT
    // POST /api/pharmacy/payments/{id}/refund
    //
    // Sales -> Edit
    //
    // Pharmacy Admin only
    // =====================================================

    [HttpPost("payments/{id:int}/refund")]
    [Authorize(Roles = "PharmacyAdmin")]
    public async Task<IActionResult> Refund(
        int id,
        [FromBody] RefundPaymentDto dto)
    {
        var permissionError =
            await CheckSalesPermission("Edit");

        if (permissionError != null)
            return permissionError;

        if (id <= 0)
        {
            return BadRequest(new
            {
                Success = false,
                Message =
                    "A valid payment id is required."
            });
        }

        var payment =
            await _db.PharmacyPayments
                .Include(x =>
                    x.PharmacyBill)
                .FirstOrDefaultAsync(x =>
                    x.Id == id &&
                    x.PharmacyId ==
                    PharmacyId &&
                    x.BranchId ==
                    BranchId);

        if (payment == null)
        {
            return NotFound(new
            {
                Success = false,
                Message =
                    "Payment not found."
            });
        }

        if (payment.Status == "Refunded")
        {
            return BadRequest(new
            {
                Success = false,
                Message =
                    "Payment is already fully refunded."
            });
        }

        if (dto.Amount <= 0 ||
            payment.RefundedAmount +
            dto.Amount >
            payment.PaidAmount)
        {
            return BadRequest(new
            {
                Success = false,
                Message =
                    "Invalid refund amount."
            });
        }

        payment.RefundedAmount +=
            dto.Amount;

        payment.Status =
            payment.RefundedAmount ==
            payment.PaidAmount
                ? "Refunded"
                : "PartiallyRefunded";

        payment.PharmacyBill.PaymentStatus =
            payment.Status;

        await _db.SaveChangesAsync();

        return Ok(new
        {
            Success = true,

            Message =
                "Payment refunded successfully.",

            Payment = new
            {
                payment.Id,
                payment.Status,
                payment.PaidAmount,
                payment.RefundedAmount,
                dto.Reason
            }
        });
    }

    // =====================================================
    // CANCEL BILL
    // POST /api/pharmacy/bills/{id}/cancel
    //
    // Sales -> Delete
    //
    // Pharmacy Admin only
    // =====================================================

    [HttpPost("bills/{id:int}/cancel")]
    [Authorize(Roles = "PharmacyAdmin")]
    public async Task<IActionResult> CancelBill(
        int id)
    {
        var permissionError =
            await CheckSalesPermission("Delete");

        if (permissionError != null)
            return permissionError;

        if (id <= 0)
        {
            return BadRequest(new
            {
                Success = false,
                Message =
                    "A valid bill id is required."
            });
        }

        var bill =
            await _db.PharmacyBills
                .FirstOrDefaultAsync(x =>
                    x.Id == id &&
                    x.PharmacyId ==
                    PharmacyId &&
                    x.BranchId ==
                    BranchId);

        if (bill == null)
        {
            return NotFound(new
            {
                Success = false,
                Message =
                    "Bill not found."
            });
        }

        if (bill.PaymentStatus == "Paid")
        {
            return BadRequest(new
            {
                Success = false,
                Message =
                    "Refund a paid bill before cancellation."
            });
        }

        if (bill.PaymentStatus ==
            "PartiallyRefunded")
        {
            return BadRequest(new
            {
                Success = false,
                Message =
                    "Complete the refund before cancelling the bill."
            });
        }

        if (bill.PaymentStatus ==
            "Cancelled")
        {
            return BadRequest(new
            {
                Success = false,
                Message =
                    "Bill is already cancelled."
            });
        }

        bill.PaymentStatus =
            "Cancelled";

        await _db.SaveChangesAsync();

        return Ok(new
        {
            Success = true,
            Message =
                "Bill cancelled successfully."
        });
    }
}