namespace AuthDemo.DTOs;

public class DailySalesReportDto
{
    public DateTime Date { get; set; }

    public int TotalBills { get; set; }

    public decimal TotalSales { get; set; }

    public decimal TotalGST { get; set; }

    public decimal TotalDiscount { get; set; }

    public decimal NetSales { get; set; }
}