using System.ComponentModel.DataAnnotations;
using Mizan.Core.Enums;

namespace Mizan.Application.DTOs.Transactions;

public class CreateTransactionDto : IValidatableObject
{
    public Guid? ContactId { get; set; }

    [MaxLength(200, ErrorMessage = "اسم الطرف يجب ألا يتجاوز 200 حرف")]
    public string? PartyName { get; set; }

    [Required(ErrorMessage = "نوع العملية مطلوب")]
    public TransactionType OperationType { get; set; }

    [Required(ErrorMessage = "المبلغ مطلوب")]
    [Range(typeof(decimal), "0.01", "999999999", ErrorMessage = "المبلغ يجب أن يكون أكبر من صفر")]
    public decimal Amount { get; set; }

    [Required(ErrorMessage = "طريقة الدفع مطلوبة")]
    public PaymentMethod PaymentMethod { get; set; } = PaymentMethod.Cash;

    [Required(ErrorMessage = "تاريخ العملية مطلوب")]
    public DateTime OperationDate { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (Amount <= 0)
        {
            yield return new ValidationResult(
                "المبلغ يجب أن يكون أكبر من صفر",
                new[] { nameof(Amount) });
        }

        if (ContactId == null && string.IsNullOrWhiteSpace(PartyName))
        {
            yield return new ValidationResult(
                "اسم الطرف مطلوب في حال عدم تحديد العميل/المورد",
                new[] { nameof(PartyName) });
        }
    }
}
