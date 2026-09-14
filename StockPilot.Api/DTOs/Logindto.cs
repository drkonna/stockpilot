using System.ComponentModel.DataAnnotations;
 
namespace StockPilot.Api.Dtos;
 
public class LoginDto
{
    [Required(ErrorMessage = "Το email είναι υποχρεωτικό.")]
    [EmailAddress(ErrorMessage = "Μη έγκυρη διεύθυνση email.")]
    public string Email { get; set; } = string.Empty;
 
    [Required(ErrorMessage = "Ο κωδικός είναι υποχρεωτικός.")]
    public string Password { get; set; } = string.Empty;
}
 