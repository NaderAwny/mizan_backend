using System.ComponentModel.DataAnnotations;
using Mizan.Core.Enums;

namespace Mizan.Application.DTOs.Transactions;

public class CreateTransactionRequest : IValidatableObject
{
    [Required]
    public Guid ContactId { get; set; }

    [Required]
    public TransactionType Type { get; set; }

    [Required]
    [Range(typeof(decimal), "0.01", "999999999", ErrorMessage = "Amount must be between 0.01 and 999,999,999")]
    public decimal Amount { get; set; }

    [Required]
    public DateTime TransactionDate { get; set; }

    public NoteType NoteType { get; set; } = NoteType.None;

    [MaxLength(1000, ErrorMessage = "Note text must not exceed 1000 characters")]
    public string? NoteText { get; set; }

    public bool IsInstallment { get; set; } = false;

    public InstallmentPlanMode? InstallmentPlanMode { get; set; }

    // Automatic mode fields
    [Range(2, 500, ErrorMessage = "Installment count must be at least 2")]
    public int? InstallmentCount { get; set; }

    public DateTime? FirstInstallmentDate { get; set; }

    public InstallmentFrequency? Frequency { get; set; }

    // Custom mode fields
    public List<CustomInstallmentItem>? CustomInstallments { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (NoteType == NoteType.Voice)
        {
            yield return new ValidationResult(
                "Voice notes cannot be set during creation. Attach voice notes after creation via the upload endpoint.",
                new[] { nameof(NoteType) });
        }

        if (!IsInstallment)
        {
            if (InstallmentPlanMode != null ||
                InstallmentCount != null ||
                FirstInstallmentDate != null ||
                Frequency != null ||
                (CustomInstallments != null && CustomInstallments.Count > 0))
            {
                yield return new ValidationResult(
                    "Installment fields must be empty when IsInstallment is false.",
                    new[] { nameof(IsInstallment) });
            }
        }
        else
        {
            if (InstallmentPlanMode == null)
            {
                yield return new ValidationResult(
                    "InstallmentPlanMode is required when IsInstallment is true.",
                    new[] { nameof(InstallmentPlanMode) });
            }
            else if (InstallmentPlanMode == Core.Enums.InstallmentPlanMode.Automatic)
            {
                if (InstallmentCount == null || InstallmentCount < 2)
                {
                    yield return new ValidationResult(
                        "InstallmentCount must be at least 2 for automatic installment plans.",
                        new[] { nameof(InstallmentCount) });
                }

                if (FirstInstallmentDate == null)
                {
                    yield return new ValidationResult(
                        "FirstInstallmentDate is required for automatic installment plans.",
                        new[] { nameof(FirstInstallmentDate) });
                }

                if (Frequency == null)
                {
                    yield return new ValidationResult(
                        "Frequency is required for automatic installment plans.",
                        new[] { nameof(Frequency) });
                }

                if (CustomInstallments != null && CustomInstallments.Count > 0)
                {
                    yield return new ValidationResult(
                        "CustomInstallments must be empty when InstallmentPlanMode is Automatic.",
                        new[] { nameof(CustomInstallments) });
                }
            }
            else if (InstallmentPlanMode == Core.Enums.InstallmentPlanMode.Custom)
            {
                if (CustomInstallments == null || CustomInstallments.Count < 2)
                {
                    yield return new ValidationResult(
                        "CustomInstallments must contain at least 2 items for custom installment plans.",
                        new[] { nameof(CustomInstallments) });
                }

                if (InstallmentCount != null || FirstInstallmentDate != null || Frequency != null)
                {
                    yield return new ValidationResult(
                        "InstallmentCount, FirstInstallmentDate, and Frequency must be null when InstallmentPlanMode is Custom.",
                        new[] { nameof(InstallmentPlanMode) });
                }
            }
        }
    }
}
