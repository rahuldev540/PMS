using System.ComponentModel.DataAnnotations;

namespace AuthDemo.DTOs;

/// <summary>
/// Used to update medicine master/catalogue information.
///
/// Stock, inventory, batch, price and expiry information
/// must be managed through Inventory APIs.
/// </summary>
public class UpdateMedicineDto
{
    // =====================================================
    // MEDICINE IDENTIFICATION
    // =====================================================

    public string MedicineCode { get; set; } = string.Empty;

    [Required(ErrorMessage = "Medicine name is required.")]
    public string Name { get; set; } = string.Empty;

    // Compatibility alias for frontend payloads
    // that use MedicineName instead of Name.
    public string MedicineName
    {
        get => Name;
        set => Name = value ?? string.Empty;
    }


    // =====================================================
    // MEDICINE DETAILS
    // =====================================================

    public string GenericName { get; set; } = string.Empty;

    public string BrandName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Category is required.")]
    public string Category { get; set; } = string.Empty;

    [Required(ErrorMessage = "Dosage form is required.")]
    public string DosageForm { get; set; } = string.Empty;

    public string Strength { get; set; } = string.Empty;

    public string Unit { get; set; } = string.Empty;

    public string Manufacturer { get; set; } = string.Empty;

    public string PackSize { get; set; } = string.Empty;


    // =====================================================
    // PRESCRIPTION
    // =====================================================

    public bool PrescriptionRequired { get; set; }


    // =====================================================
    // STATUS
    // =====================================================

    public string CatalogueStatus { get; set; } = "Active";


    // =====================================================
    // DESCRIPTION
    // =====================================================

    public string Description { get; set; } = string.Empty;

    // Compatibility alias for frontend payloads
    // using DescriptionUsageNotes.
    public string DescriptionUsageNotes
    {
        get => Description;
        set => Description = value ?? string.Empty;
    }
}