using System.ComponentModel.DataAnnotations;

namespace StockPilot.Api.Dtos;

public class SetStockDto
{
    [Required(ErrorMessage = "Η ποσότητα είναι υποχρεωτική.")]
    [Range(0, int.MaxValue, ErrorMessage = "Η ποσότητα δεν μπορεί να είναι αρνητική.")]
    public int? Quantity { get; set; }

    [Range(0, int.MaxValue, ErrorMessage = "Το όριο δεν μπορεί να είναι αρνητικό.")]
    public int? ReorderLevel { get; set; }
}