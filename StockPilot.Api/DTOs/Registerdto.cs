using System.ComponentModel.DataAnnotations;
 
namespace StockPilot.Api.Dtos;
 
public class RegisterDto
{
    [Required(ErrorMessage = "Το email είναι υποχρεωτικό.")]
    [EmailAddress(ErrorMessage = "Μη έγκυρη διεύθυνση email.")]
    public string Email { get; set; } = string.Empty;
 
    [Required(ErrorMessage = "Ο κωδικός είναι υποχρεωτικός.")]
    [StringLength(100, MinimumLength = 8, ErrorMessage = "Ο κωδικός πρέπει να έχει τουλάχιστον 8 χαρακτήρες.")]
    public string Password { get; set; } = string.Empty;
}