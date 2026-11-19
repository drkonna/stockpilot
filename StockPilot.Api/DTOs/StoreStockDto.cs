namespace StockPilot.Api.Dtos;

public class StoreStockDto
{
    public int StoreId { get; set; }
    public string StoreName { get; set; } = string.Empty;
    public string StoreCode { get; set; } = string.Empty;
    public bool IsCentral { get; set; }
    public int Quantity { get; set; }
    public int? ReorderLevel { get; set; }
    public bool IsLow => ReorderLevel is not null && Quantity <= ReorderLevel;
}