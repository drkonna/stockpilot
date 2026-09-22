namespace StockPilot.Api.Dtos;

public class ProductResponseDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Sku { get; set; } = string.Empty;
    public int QuantityInStock { get; set; }
    public decimal UnitPrice { get; set; }
    public DateTime CreatedAt { get; set; }

    public int? CategoryId { get; set; }
    public string? CategoryName { get; set; }

    public int SupplierId { get; set; }
    public string SupplierName { get; set; } = string.Empty;

    public int? ProductFamilyId { get; set; }
    public string? ProductFamilyName { get; set; }

    public string? Color { get; set; }
    public string? Size { get; set; }
}