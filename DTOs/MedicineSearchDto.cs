namespace AuthDemo.DTOs;

public class MedicineSearchDto
{
    public int Id { get; set; }

    public string MedicineCode { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string GenericName { get; set; } = string.Empty;

    public string Strength { get; set; } = string.Empty;

    public int AvailableStock { get; set; }
}