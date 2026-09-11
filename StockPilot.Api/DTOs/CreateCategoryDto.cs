using System.ComponentModel.DataAnnotations;

namespace StockPilot.Api.Dtos;

public class CreateCategoryDto
{
    [Required(ErrorMessage = "Το όνομα κατηγορίας είναι υποχρεωτικό.")]
    [StringLength(100, MinimumLength = 2)]
    public string Name { get; set; } = string.Empty;
}