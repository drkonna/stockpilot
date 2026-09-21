using System.ComponentModel.DataAnnotations;

namespace StockPilot.Api.Dtos;

public class CreateProductFamilyDto
{
    [Required(ErrorMessage = "Το όνομα της family είναι υποχρεωτικό.")]
    [StringLength(100, MinimumLength = 2)]
    public string Name { get; set; } = string.Empty;
}