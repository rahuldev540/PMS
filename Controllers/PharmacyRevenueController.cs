using AuthDemo.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AuthDemo.Controllers;

[ApiController]
[Route("api/pharmacy/revenue")]
[Authorize(Roles = "PharmacyAdmin")]
public class PharmacyRevenueController : PharmacyApiControllerBase
{
    private readonly AppDbContext _db;
    public PharmacyRevenueController(AppDbContext db) => _db = db;

    // Revenue = sum of completed paid bills (not unpaid/pending).
    // Date filters apply to bill date, inclusive at both ends (UTC).
    [HttpGet]
    public async Task<IActionResult> GetRevenue([FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null, [FromQuery] int? branchId = null)
    {
        if (from.HasValue && to.HasValue && from.Value.Date > to.Value.Date)
            return BadRequest(new { success = false, message = "from must be on or before to." });

        var branches = await _db.PharmacyBranchIdentities.AsNoTracking()
            .Where(x => x.PharmacyId == PharmacyId)
            .Join(_db.Branches.AsNoTracking(), x => x.ExistingBranchId, b => b.Id,
                (x, b) => new { x.BranchId, x.ExistingBranchId, b.Name, b.IsActive })
            .ToListAsync();
        if (branchId.HasValue && !branches.Any(x => x.BranchId == branchId.Value))
            return BadRequest(new { success = false, message = "Branch does not belong to your pharmacy." });

        var bills = _db.PharmacyBills.AsNoTracking()
            .Where(x => x.PharmacyId == PharmacyId && x.PaymentStatus == "Paid");
        if (from.HasValue) bills = bills.Where(x => x.BillDate >= from.Value.Date);
        if (to.HasValue)
        {
            var exclusiveEnd = to.Value.Date.AddDays(1);
            bills = bills.Where(x => x.BillDate < exclusiveEnd);
        }
        if (branchId.HasValue)
        {
            var internalId = branches.First(x => x.BranchId == branchId.Value).ExistingBranchId;
            bills = bills.Where(x => x.BranchId == internalId);
        }
        var sums = await bills.GroupBy(x => x.BranchId).Select(g => new
        {
            internalBranchId = g.Key,
            paidBills = g.Count(),
            grossSales = g.Sum(x => x.SubTotal),
            discount = g.Sum(x => x.Discount),
            gst = g.Sum(x => x.GSTAmount),
            revenue = g.Sum(x => x.GrandTotal)
        }).ToListAsync();
        var report = branches.Where(b => !branchId.HasValue || b.BranchId == branchId.Value)
            .OrderBy(b => b.BranchId)
            .Select(b =>
            {
                var total = sums.FirstOrDefault(x => x.internalBranchId == b.ExistingBranchId);
                return new
                {
                    branchId = b.BranchId, branchName = b.Name,
                    paidBills = total?.paidBills ?? 0,
                    grossSales = total?.grossSales ?? 0m,
                    discount = total?.discount ?? 0m,
                    gst = total?.gst ?? 0m,
                    revenue = total?.revenue ?? 0m
                };
            }).ToList();
        return Ok(new
        {
            success = true, pharmacyId = PharmacyId, from, to,
            revenueBasis = "Paid pharmacy bills by bill date (gross billed revenue, including GST; refunds not netted)",
            totalRevenue = report.Sum(x => x.revenue),
            totalPaidBills = report.Sum(x => x.paidBills),
            branches = report
        });
    }
}
