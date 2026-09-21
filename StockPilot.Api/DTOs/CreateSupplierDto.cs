using System.ComponentModel.DataAnnotations;

namespace StockPilot.Api.Dtos;

public class CreateSupplierDto
{
    [Required(ErrorMessage = "Το όνομα προμηθευτή είναι υποχρεωτικό.")]
    [StringLength(100, MinimumLength = 2)]
    public string Name { get; set; } = string.Empty;
}