namespace AuthDemo.Models;

public class StockTransaction
{
    public int Id { get; set; }

    // Standalone pharmacy ownership.
    public int PharmacyId { get; set; }

    // Stock transaction always belongs to a specific pharmacy branch.
    public int BranchId { get; set; }


    public int MedicineId { get; set; }

    public int? MedicineBatchId { get; set; }

    public string TransactionType { get; set; } = string.Empty;
    // Purchase
    // Dispense
    // Return
    // Adjustment

    public int Quantity { get; set; }

    public int BalanceStock { get; set; }

    public string Remarks { get; set; } = string.Empty;

    public string? ReferenceType { get; set; }

    public int? ReferenceId { get; set; }

    public int? CreatedByUserId { get; set; }

    public DateTime TransactionDate { get; set; } = DateTime.UtcNow;
}
