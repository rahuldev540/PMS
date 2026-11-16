namespace AuthDemo.DTOs;

public class DispensePrescriptionDto
{
    public int PrescriptionId { get; set; }

    public List<DispenseMedicineDto> Medicines { get; set; }
        = new();
}

public class DispenseMedicineDto
{
    public int PrescriptionItemId { get; set; }

    public int Quantity { get; set; }
}