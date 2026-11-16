namespace AuthDemo.Models;

public class Inventory
{
    public int Id { get; set; }

    // Standalone pharmacy ownership.
    public int PharmacyId { get; set; }

    // Inventory always belongs to a specific pharmacy branch.
    public int BranchId { get; set; }


    public int MedicineId { get; set; }

    public Medicine Medicine { get; set; } = null!;

    public int CurrentStock { get; set; }

    public int ReservedStock { get; set; }

    public int AvailableStock { get; set; }

    public int MinimumStock { get; set; }

    public int MaximumStock { get; set; }

    public int ReorderLevel { get; set; }

    public string StorageLocation { get; set; } = string.Empty;

    // SQL Server rowversion prevents lost updates during simultaneous dispensing.
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
}
