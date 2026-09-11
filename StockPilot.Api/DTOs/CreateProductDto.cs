using System.ComponentModel.DataAnnotations;

namespace StockPilot.Api.Dtos;

public class CreateProductDto
{
    [Required(ErrorMessage = "Το όνομα είναι υποχρεωτικό.")]
    [StringLength(200, MinimumLength = 2)]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "Το SKU είναι υποχρεωτικό.")]
    [StringLength(50)]
    public string Sku { get; set; } = string.Empty;

    [Range(0, int.MaxValue, ErrorMessage = "Η ποσότητα δεν μπορεί να είναι αρνητική.")]
    public int QuantityInStock { get; set; }

    [Range(0.01, double.MaxValue, ErrorMessage = "Η τιμή πρέπει να είναι θετικός αριθμός.")]
    public decimal UnitPrice { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "Πρέπει να επιλέξεις έγκυρη κατηγορία.")]
    public int CategoryId { get; set; }
}