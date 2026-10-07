using System.ComponentModel.DataAnnotations;

namespace StockPilot.Api.Dtos;

public class CreateStoreDto
{
    [Required(ErrorMessage = "Το όνομα καταστήματος είναι υποχρεωτικό.")]
    [StringLength(100, MinimumLength = 2)]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "Ο κωδικός καταστήματος είναι υποχρεωτικός.")]
    [StringLength(20, MinimumLength = 2)]
    public string Code { get; set; } = string.Empty;
}