using System.ComponentModel.DataAnnotations;

namespace StockPilot.Api.Dtos;

public class SetStockDto
{
    [Required(ErrorMessage = "Η ποσότητα είναι υποχρεωτική.")]
    [Range(0, int.MaxValue, ErrorMessage = "Η ποσότητα δεν μπορεί να είναι αρνητική.")]
    public int? Quantity { get; set; }
}