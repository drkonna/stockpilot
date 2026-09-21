using System.ComponentModel.DataAnnotations;

namespace StockPilot.Api.Dtos;

public class UpdateProductDto
{
    [Required]
    [StringLength(200, MinimumLength = 2)]
    public string Name { get; set; } = string.Empty;

    [Required]
    [StringLength(50)]
    public string Sku { get; set; } = string.Empty;

    [Range(0, int.MaxValue)]
    public int QuantityInStock { get; set; }

    [Range(0.01, double.MaxValue)]
    public decimal UnitPrice { get; set; }

    
    public int? CategoryId { get; set; }

    [Range(1, int.MaxValue)]
    public int SupplierId { get; set; }

    public int? ProductFamilyId { get; set; }

    [StringLength(50)]
    public string? Color { get; set; }

    [StringLength(50)]
    public string? Size { get; set; }

}