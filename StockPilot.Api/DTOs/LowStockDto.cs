namespace StockPilot.Api.Dtos;

public class LowStockDto
{
    public int ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string Sku { get; set; } = string.Empty;
    public int StoreId { get; set; }
    public string StoreName { get; set; } = string.Empty;
    public string StoreCode { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public int ReorderLevel { get; set; }
}