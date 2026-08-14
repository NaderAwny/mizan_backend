using Mizan.Core.Enums;
using Mizan.Core.Exceptions;

namespace Mizan.Core.Entities;

public class Installment
{
    public int Id { get; private set; }
    public int TransactionId { get; private set; }
    public int InstallmentNumber { get; private set; }
    public decimal Amount { get; private set; }
    public DateTime DueDate { get; private set; }
    public InstallmentStatus Status { get; private set; } = InstallmentStatus.Pending;
    public DateTime? PaidAt { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    // Navigation property
    public Transaction? Transaction { get; private set; }

    private Installment() { } // Required for EF Core

    public static Installment CreateSingle(int transactionId, int installmentNumber, decimal amount, DateTime dueDate)
    {
        if (amount <= 0)
            throw new DomainException("Each installment amount must be greater than zero");

        return new Installment
        {
            TransactionId = transactionId,
            InstallmentNumber = installmentNumber,
            Amount = amount,
            DueDate = dueDate,
            Status = InstallmentStatus.Pending,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }

    public static List<Installment> GenerateAutomaticSchedule(
        int transactionId,
        decimal totalAmount,
        int installmentCount,
        DateTime firstDueDate,
        InstallmentFrequency frequency)
    {
        if (installmentCount < 2)
            throw new DomainException("Installment count must be at least 2");

        var installments = new List<Installment>();
        decimal baseAmount = Math.Round(totalAmount / installmentCount, 2, MidpointRounding.ToZero);
        decimal sumBase = baseAmount * installmentCount;
        decimal remainder = Math.Round(totalAmount - sumBase, 2);

        var now = DateTime.UtcNow;

        for (int i = 1; i <= installmentCount; i++)
        {
            decimal itemAmount = (i == installmentCount) ? baseAmount + remainder : baseAmount;
            DateTime dueDate = frequency switch
            {
                InstallmentFrequency.Weekly => firstDueDate.AddDays(7 * (i - 1)),
                InstallmentFrequency.Monthly => firstDueDate.AddMonths(i - 1),
                InstallmentFrequency.Yearly => firstDueDate.AddYears(i - 1),
                _ => throw new DomainException("Invalid installment frequency")
            };

            installments.Add(new Installment
            {
                TransactionId = transactionId,
                InstallmentNumber = i,
                Amount = itemAmount,
                DueDate = dueDate,
                Status = InstallmentStatus.Pending,
                CreatedAt = now,
                UpdatedAt = now
            });
        }

        return installments;
    }

    public static List<Installment> GenerateCustomSchedule(
        int transactionId,
        IReadOnlyList<(decimal Amount, DateTime DueDate)> customInstallments)
    {
        if (customInstallments == null || customInstallments.Count < 2)
            throw new DomainException("Installment count must be at least 2");

        var installments = new List<Installment>();
        var now = DateTime.UtcNow;

        for (int i = 0; i < customInstallments.Count; i++)
        {
            var item = customInstallments[i];
            if (item.Amount <= 0)
                throw new DomainException("Each installment amount must be greater than zero");

            installments.Add(new Installment
            {
                TransactionId = transactionId,
                InstallmentNumber = i + 1,
                Amount = item.Amount,
                DueDate = item.DueDate,
                Status = InstallmentStatus.Pending,
                CreatedAt = now,
                UpdatedAt = now
            });
        }

        return installments;
    }

    public void MarkAsPaid()
    {
        if (Status == InstallmentStatus.Paid)
            throw new DomainException("Installment is already paid");

        if (Status == InstallmentStatus.Voided)
            throw new DomainException("Cannot pay a voided installment");

        Status = InstallmentStatus.Paid;
        PaidAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Void()
    {
        if (Status != InstallmentStatus.Paid)
        {
            Status = InstallmentStatus.Voided;
            UpdatedAt = DateTime.UtcNow;
        }
    }
}
