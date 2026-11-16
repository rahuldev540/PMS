namespace AuthDemo.Models;

/// <summary>Pharmacy-scoped public import number; existing MedicineImport.Id remains the internal FK.</summary>
public class PharmacyMedicineImportIdentity
{
    public int PharmacyId { get; set; }
    public int ImportId { get; set; }
    public int ExistingImportId { get; set; }
    public MedicineImport MedicineImport { get; set; } = null!;
}
