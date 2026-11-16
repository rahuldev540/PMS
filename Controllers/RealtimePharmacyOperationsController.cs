using System.Security.Claims;
using AuthDemo.Data;
using AuthDemo.Models;
using AuthDemo.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AuthDemo.Controllers;

[ApiController]
[Route("api/pharmacy/operations")]
[Authorize(Roles="PharmacyAdmin,Pharmacist")]
public class RealtimePharmacyOperationsController : ControllerBase
{
    private readonly AppDbContext _db; private readonly IPharmacyRealtimeNotifier _rt;
    public RealtimePharmacyOperationsController(AppDbContext db,IPharmacyRealtimeNotifier rt){_db=db;_rt=rt;}
    private int ClaimInt(string name)=>int.TryParse(User.FindFirstValue(name)??User.FindFirstValue(ClaimTypes.NameIdentifier),out var v)?v:0;
    private int PharmacyId=>int.TryParse(User.FindFirstValue("PharmacyId")??User.FindFirstValue("pharmacyId"),out var p)&&p>0?p:0;
    private int BranchId=>int.TryParse(User.FindFirstValue("BranchId"),out var v)?v:0;

    [HttpGet("barcode/{barcode}")]
    public async Task<IActionResult> Barcode(string barcode)=>PharmacyId==0||BranchId==0?Forbid():Ok(await _db.Medicines.AsNoTracking().Where(x=>x.PharmacyId==PharmacyId&&x.BranchId==BranchId&&x.Barcode==barcode).Select(x=>new{x.Id,x.MedicineCode,x.Name,x.Strength,x.SellingPrice,x.IsControlledMedicine,x.IsRecalled}).FirstOrDefaultAsync());

    [HttpPut("medicines/{medicineId:int}/controls")]
    [Authorize(Roles="PharmacyAdmin")]
    public async Task<IActionResult> UpdateMedicineControls(int medicineId,[FromBody] MedicineControlsRequest dto)
    {
        if(PharmacyId==0||BranchId==0)return Forbid();
        var med=await _db.Medicines.FirstOrDefaultAsync(x=>x.Id==medicineId&&x.PharmacyId==PharmacyId&&x.BranchId==BranchId); if(med==null)return NotFound();
        if(!string.IsNullOrWhiteSpace(dto.Barcode) && await _db.Medicines.AnyAsync(x=>x.Id!=medicineId&&x.PharmacyId==PharmacyId&&x.BranchId==BranchId&&x.Barcode==dto.Barcode.Trim())) return Conflict(new{Message="Barcode already exists in this pharmacy."});
        med.Barcode=string.IsNullOrWhiteSpace(dto.Barcode)?null:dto.Barcode.Trim(); med.IsControlledMedicine=dto.IsControlledMedicine; await _db.SaveChangesAsync(); return Ok(new{Success=true,med.Id,med.Barcode,med.IsControlledMedicine});
    }

    [HttpPost("recalls")]
    [Authorize(Roles="PharmacyAdmin")]
    public async Task<IActionResult> Recall([FromBody] RecallRequest dto)
    {
        if(PharmacyId==0||BranchId==0)return Forbid();
        var med=await _db.Medicines.FirstOrDefaultAsync(x=>x.Id==dto.MedicineId&&x.PharmacyId==PharmacyId&&x.BranchId==BranchId); if(med==null)return NotFound();
        med.IsRecalled=true; med.RecallReason=dto.Reason;
        if(dto.BatchId.HasValue){var b=await _db.MedicineBatches.FirstOrDefaultAsync(x=>x.Id==dto.BatchId&&x.Inventory.MedicineId==dto.MedicineId&&x.Inventory.PharmacyId==PharmacyId&&x.Inventory.BranchId==BranchId);if(b==null)return BadRequest(new{Message="Batch not found."});b.IsRecalled=true;b.IsQuarantined=true;b.RecallReason=dto.Reason;}
        _db.MedicineRecalls.Add(new MedicineRecall{PharmacyId=PharmacyId,BranchId=BranchId,MedicineId=dto.MedicineId,MedicineBatchId=dto.BatchId,Reason=dto.Reason,CreatedByUserId=ClaimInt(ClaimTypes.NameIdentifier)}); await _db.SaveChangesAsync();
        await _rt.PublishAsync(PharmacyId,BranchId,"MedicineRecalled",new{dto.MedicineId,dto.BatchId,dto.Reason}); return Ok(new{Success=true});
    }

    [HttpPost("shifts/open")]
    public async Task<IActionResult> OpenShift([FromBody] OpenShiftRequest dto){if(PharmacyId==0||BranchId==0)return Forbid();var uid=ClaimInt(ClaimTypes.NameIdentifier);if(await _db.PharmacyShifts.AnyAsync(x=>x.PharmacyId==PharmacyId&&x.BranchId==BranchId&&x.UserId==uid&&x.Status=="Open"))return Conflict(new{Message="An open shift already exists."});var s=new PharmacyShift{PharmacyId=PharmacyId,BranchId=BranchId,UserId=uid,OpeningCash=dto.OpeningCash,Notes=dto.Notes};_db.Add(s);await _db.SaveChangesAsync();return CreatedAtAction(nameof(GetShift),new{id=s.Id},s);}
    [HttpGet("shifts/{id:int}")] public async Task<IActionResult> GetShift(int id){if(PharmacyId==0||BranchId==0)return Forbid();var shift=await _db.PharmacyShifts.AsNoTracking().FirstOrDefaultAsync(x=>x.Id==id&&x.PharmacyId==PharmacyId&&x.BranchId==BranchId);return shift==null?NotFound():Ok(shift);}
    [HttpPost("shifts/{id:int}/close")]
    public async Task<IActionResult> CloseShift(int id,[FromBody] CloseShiftRequest dto){if(PharmacyId==0||BranchId==0)return Forbid();var s=await _db.PharmacyShifts.FirstOrDefaultAsync(x=>x.Id==id&&x.PharmacyId==PharmacyId&&x.BranchId==BranchId&&x.Status=="Open");if(s==null)return NotFound();s.ClosingCash=dto.ClosingCash;s.ExpectedCash=dto.ExpectedCash;s.ClosedAt=DateTime.UtcNow;s.Status="Closed";s.Notes=string.Join("; ",new[]{s.Notes,dto.Notes}.Where(x=>!string.IsNullOrWhiteSpace(x)));await _db.SaveChangesAsync();return Ok(s);}

    [HttpPost("supplier-invoices")]
    [Authorize(Roles="PharmacyAdmin")]
    public async Task<IActionResult> SupplierInvoice([FromBody] SupplierInvoiceRequest dto){if(PharmacyId==0||BranchId==0)return Forbid();if(!await _db.Suppliers.AnyAsync(x=>x.Id==dto.SupplierId&&x.PharmacyId==PharmacyId&&x.BranchId==BranchId))return BadRequest(new{Message="Supplier not found in this pharmacy branch."});if(dto.PurchaseOrderId.HasValue&&!await _db.PurchaseOrders.AnyAsync(x=>x.Id==dto.PurchaseOrderId.Value&&x.PharmacyId==PharmacyId&&x.BranchId==BranchId&&x.SupplierId==dto.SupplierId))return BadRequest(new{Message="Purchase order not found for this supplier and pharmacy branch."});if(dto.TotalAmount<0||dto.PaidAmount<0||dto.PaidAmount>dto.TotalAmount)return BadRequest(new{Message="Invalid invoice amounts."});var i=new SupplierInvoice{PharmacyId=PharmacyId,BranchId=BranchId,SupplierId=dto.SupplierId,PurchaseOrderId=dto.PurchaseOrderId,InvoiceNumber=dto.InvoiceNumber.Trim(),TotalAmount=dto.TotalAmount,PaidAmount=dto.PaidAmount,DueDate=dto.DueDate,Status=dto.PaidAmount>=dto.TotalAmount?"Paid":dto.PaidAmount>0?"PartiallyPaid":"Unpaid"};_db.Add(i);await _db.SaveChangesAsync();return Ok(i);}

    [HttpPost("bills/{billId:int}/split-payment")]
    public async Task<IActionResult> SplitPayment(int billId,[FromBody] SplitPaymentRequest dto){if(PharmacyId==0||BranchId==0)return Forbid();if(dto.Parts==null||dto.Parts.Count==0||dto.Parts.Any(p=>p.Amount<=0))return BadRequest(new{Message="Each payment part must have a positive amount."});var bill=await _db.PharmacyBills.FirstOrDefaultAsync(x=>x.Id==billId&&x.PharmacyId==PharmacyId&&x.BranchId==BranchId);if(bill==null)return NotFound();var already=await _db.PharmacyPayments.Where(x=>x.PharmacyBillId==billId&&x.PharmacyId==PharmacyId&&x.BranchId==BranchId&&x.Status!="Refunded").SumAsync(x=>(decimal?)x.PaidAmount)??0;var incoming=dto.Parts.Sum(x=>x.Amount);if(incoming<=0||already+incoming>bill.GrandTotal)return BadRequest(new{Message="Payment exceeds outstanding bill amount."});foreach(var p in dto.Parts.Where(x=>x.Amount>0))_db.PharmacyPayments.Add(new PharmacyPayment{PharmacyId=PharmacyId,BranchId=BranchId,PharmacyBillId=billId,PaymentMode=p.Mode,TransactionId=p.TransactionId,PaidAmount=p.Amount,Status="Paid",ReceivedByUserId=ClaimInt(ClaimTypes.NameIdentifier)});bill.PaymentStatus=already+incoming>=bill.GrandTotal?"Paid":"PartiallyPaid";await _db.SaveChangesAsync();await _rt.PublishAsync(PharmacyId,BranchId,"BillPaymentUpdated",new{BillId=billId,bill.PaymentStatus});return Ok(new{Success=true,bill.PaymentStatus,Outstanding=Math.Max(0,bill.GrandTotal-already-incoming)});}
}
public record RecallRequest(int MedicineId,int? BatchId,string Reason);
public record OpenShiftRequest(decimal OpeningCash,string Notes="");
public record CloseShiftRequest(decimal ClosingCash,decimal ExpectedCash,string Notes="");
public record SupplierInvoiceRequest(int SupplierId,int? PurchaseOrderId,string InvoiceNumber,decimal TotalAmount,decimal PaidAmount,DateTime? DueDate);
public record SplitPaymentPart(string Mode,decimal Amount,string? TransactionId);
public record SplitPaymentRequest(List<SplitPaymentPart> Parts);

public record MedicineControlsRequest(string? Barcode,bool IsControlledMedicine);
