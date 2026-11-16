using AuthDemo.DTOs;

namespace AuthDemo.Services.Interfaces;

public interface IPharmacyService
{
    // =====================================================
    // DISPENSE PRESCRIPTION
    // =====================================================

    Task<string> DispensePrescriptionAsync(
        DispensePrescriptionDto dto,
        int pharmacyId,
        int branchId,
        int userId);

    // =====================================================
    // GET PENDING PRESCRIPTIONS
    // =====================================================

    Task<List<PendingPrescriptionDto>> GetPendingPrescriptionsAsync(
        int pharmacyId,
        int branchId);

    // =====================================================
    // GET PRESCRIPTION DETAILS
    // =====================================================

    Task<PharmacyPrescriptionDto?> GetPrescriptionAsync(
        int prescriptionId,
        int pharmacyId,
        int branchId);

    // =====================================================
    // GENERATE PHARMACY BILL
    // =====================================================

    Task<int> GenerateBillAsync(
        GeneratePharmacyBillDto dto,
        int pharmacyId,
        int branchId);

    // =====================================================
    // PAY PHARMACY BILL
    // =====================================================

    Task<string> PayBillAsync(
        PayPharmacyBillDto dto,
        int pharmacyId,
        int branchId);

    // =====================================================
    // GET PHARMACY INVOICE
    // =====================================================

    Task<PharmacyInvoiceDto?> GetInvoiceAsync(
        int billId,
        int pharmacyId,
        int branchId);

    // =====================================================
    // GET DAILY SALES REPORT
    // =====================================================

    Task<DailySalesReportDto> GetDailySalesReportAsync(
        DateTime date,
        int pharmacyId,
        int branchId);
}
