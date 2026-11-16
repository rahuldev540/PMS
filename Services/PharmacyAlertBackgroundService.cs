using AuthDemo.Data;
using AuthDemo.Models;
using Microsoft.EntityFrameworkCore;
namespace AuthDemo.Services;
public sealed class PharmacyAlertBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory; private readonly ILogger<PharmacyAlertBackgroundService> _logger;
    public PharmacyAlertBackgroundService(IServiceScopeFactory scopeFactory, ILogger<PharmacyAlertBackgroundService> logger) { _scopeFactory=scopeFactory; _logger=logger; }
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try { await Scan(stoppingToken); } catch(Exception ex) { _logger.LogError(ex,"Pharmacy alert scan failed"); }
            await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
        }
    }
    private async Task Scan(CancellationToken ct)
    {
        using var scope=_scopeFactory.CreateScope(); var db=scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var settings=await db.PharmacyBranchSettings.AsNoTracking().ToListAsync(ct); var now=DateTime.UtcNow.Date;

        // Release expired pharmacy reservations without changing physical CurrentStock.
        var expiredReservations = await db.Prescriptions.Include(p=>p.Medicines).Where(p=>p.ReservationExpiresAt!=null && p.ReservationExpiresAt<DateTime.UtcNow && p.Medicines.Any(i=>i.ReservedQuantity>0)).ToListAsync(ct);
        foreach(var rx in expiredReservations) foreach(var item in rx.Medicines.Where(i=>i.ReservedQuantity>0))
        {
            var inv=await db.Inventories.FirstOrDefaultAsync(x=>x.PharmacyId==rx.PharmacyId&&x.BranchId==rx.BranchId&&x.MedicineId==item.MedicineId,ct);
            if(inv!=null){inv.ReservedStock=Math.Max(0,inv.ReservedStock-item.ReservedQuantity);inv.AvailableStock=Math.Min(inv.CurrentStock,inv.AvailableStock+item.ReservedQuantity);}
            item.ReservedQuantity=0;
        }

        foreach(var s in settings.Where(x => x.PharmacyId > 0 && x.BranchId > 0))
        {
            if(s.LowStockNotificationsEnabled)
            {
                var low=await db.Inventories.AsNoTracking().Where(x=>x.PharmacyId==s.PharmacyId && x.BranchId==s.BranchId && x.AvailableStock<=x.ReorderLevel).Select(x=>new{x.Id,x.MedicineId,x.AvailableStock}).ToListAsync(ct);
                foreach(var x in low)
                {
                    if(!await db.PharmacyAlerts.AnyAsync(a=>a.PharmacyId==s.PharmacyId&&a.BranchId==s.BranchId&&a.MedicineId==x.MedicineId&&a.AlertType=="LowStock"&&!a.IsResolved,ct)) db.PharmacyAlerts.Add(new PharmacyAlert{PharmacyId=s.PharmacyId,BranchId=s.BranchId,MedicineId=x.MedicineId,AlertType="LowStock",Message=$"Available stock is {x.AvailableStock}. Reorder is recommended."});
                    if(s.AutomaticReorderingEnabled && !await db.PharmacyAlerts.AnyAsync(a=>a.PharmacyId==s.PharmacyId&&a.BranchId==s.BranchId&&a.MedicineId==x.MedicineId&&a.AlertType=="ReorderSuggested"&&!a.IsResolved,ct)) db.PharmacyAlerts.Add(new PharmacyAlert{PharmacyId=s.PharmacyId,BranchId=s.BranchId,MedicineId=x.MedicineId,AlertType="ReorderSuggested",Message="Automatic reorder is enabled. Create/approve a purchase order for this medicine."});
                }
            }
            if(s.ExpiryWarningAlertsEnabled)
            {
                var limit=now.AddDays(s.ExpiryAlertHorizonDays<=0?30:s.ExpiryAlertHorizonDays);
                var batches=await db.MedicineBatches.AsNoTracking().Where(b=>b.Inventory.PharmacyId==s.PharmacyId&&b.Inventory.BranchId==s.BranchId&&b.RemainingQuantity>0&&b.ExpiryDate>=now&&b.ExpiryDate<=limit).Select(b=>new{b.Id,b.Inventory.MedicineId,b.BatchNumber,b.ExpiryDate}).ToListAsync(ct);
                foreach(var b in batches) if(!await db.PharmacyAlerts.AnyAsync(a=>a.PharmacyId==s.PharmacyId&&a.BranchId==s.BranchId&&a.MedicineBatchId==b.Id&&a.AlertType=="NearExpiry"&&!a.IsResolved,ct)) db.PharmacyAlerts.Add(new PharmacyAlert{PharmacyId=s.PharmacyId,BranchId=s.BranchId,MedicineId=b.MedicineId,MedicineBatchId=b.Id,AlertType="NearExpiry",Message=$"Batch {b.BatchNumber} expires on {b.ExpiryDate:yyyy-MM-dd}."});
            }
        }
        await db.SaveChangesAsync(ct);
    }
}
