using System.ComponentModel.DataAnnotations;

namespace Mizan.Application.DTOs.Transactions;

public class CustomInstallmentItem
{
    [Required]
    [Range(typeof(decimal), "0.01", "999999999", ErrorMessage = "Installment amount must be greater than zero and within maximum limit")]
    public decimal Amount { get; set; }

    [Required]
    public DateTime DueDate { get; set; }
}
