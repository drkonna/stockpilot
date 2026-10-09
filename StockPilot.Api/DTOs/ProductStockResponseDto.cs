namespace StockPilot.Api.Dtos;

public class ProductStockResponseDto
{
    public int ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string Sku { get; set; } = string.Empty;
    public int TotalQuantity { get; set; }
    public List<StoreStockDto> Stores { get; set; } = new();
}