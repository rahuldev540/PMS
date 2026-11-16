using System.ComponentModel.DataAnnotations;

namespace AuthDemo.DTOs;

public class CreateMedicineDto
{
    // =====================================================
    // MEDICINE MASTER DETAILS ONLY
    //
    // Stock-related information is NOT handled here.
    // Use:
    // POST /api/inventory/add-stock
    //
    // for:
    // Quantity
    // Batch
    // Expiry
    // Supplier
    // Purchase/Selling Price
    // GST
    // Stock Levels
    // Storage Location
    // =====================================================


    // =====================================================
    // MEDICINE CODE
    //
    // Optional.
    // Backend can generate MED-... when empty.
    // =====================================================

    public string MedicineCode { get; set; } = string.Empty;


    // =====================================================
    // MEDICINE NAME
    // =====================================================

    [Required]
    public string Name { get; set; } = string.Empty;


    // Frontend compatibility alias.
    public string MedicineName
    {
        get => Name;
        set => Name = value ?? string.Empty;
    }


    // =====================================================
    // GENERIC NAME
    // =====================================================

    public string GenericName { get; set; } = string.Empty;


    // =====================================================
    // BRAND NAME
    // =====================================================

    public string BrandName { get; set; } = string.Empty;


    // =====================================================
    // CATEGORY
    // =====================================================

    [Required]
    public string Category { get; set; } = string.Empty;


    // =====================================================
    // DOSAGE FORM
    //
    // Example:
    // Tablet
    // Capsule
    // Syrup
    // Injection
    // Cream
    // =====================================================

    [Required]
    public string DosageForm { get; set; } = string.Empty;


    // =====================================================
    // STRENGTH
    //
    // Example:
    // 500 mg
    // 650 mg
    // 5 mg/ml
    // =====================================================

    public string Strength { get; set; } = string.Empty;


    // =====================================================
    // UNIT
    //
    // Example:
    // Tablet
    // Capsule
    // Bottle
    // Strip
    // =====================================================

    public string Unit { get; set; } = string.Empty;


    // =====================================================
    // MANUFACTURER
    // =====================================================

    public string Manufacturer { get; set; } = string.Empty;


    // =====================================================
    // PACK SIZE
    //
    // Example:
    // 10 Tablets
    // 15 Tablets
    // 100 ml
    // =====================================================

    public string PackSize { get; set; } = string.Empty;


    // =====================================================
    // PRESCRIPTION REQUIRED
    // =====================================================

    public bool PrescriptionRequired { get; set; }


    // =====================================================
    // CATALOGUE STATUS
    //
    // Active / Inactive
    // =====================================================

    public string CatalogueStatus { get; set; } = "Active";


    // =====================================================
    // DESCRIPTION / USAGE NOTES
    // =====================================================

    public string Description { get; set; } = string.Empty;


    // Frontend compatibility alias.
    public string DescriptionUsageNotes
    {
        get => Description;
        set => Description = value ?? string.Empty;
    }
}