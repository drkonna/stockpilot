using System.ComponentModel.DataAnnotations;

namespace StockPilot.Api.Dtos;

public class UpdateProductFamilyDto
{
    [Required]
    [StringLength(100, MinimumLength = 2)]
    public string Name { get; set; } = string.Empty;
}