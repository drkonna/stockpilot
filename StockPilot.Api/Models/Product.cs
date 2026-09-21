namespace StockPilot.Api.Models;

public class Product
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Sku { get; set; } = string.Empty;
    public int QuantityInStock { get; set; }
    public decimal UnitPrice { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public int? CategoryId { get; set; }
    public Category? Category { get; set; }
    public int SupplierId { get; set; }
    public Supplier? Supplier { get; set; }

    public int? ProductFamilyId { get; set; }
    public ProductFamily? ProductFamily { get; set; }

    public string? Color { get; set; }
    public string? Size { get; set; }
}