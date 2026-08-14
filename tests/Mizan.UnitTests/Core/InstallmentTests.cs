using Mizan.Core.Entities;
using Mizan.Core.Enums;
using Mizan.Core.Exceptions;
using Xunit;

namespace Mizan.UnitTests.Core;

public class InstallmentTests
{
    private readonly int _txId = 1;

    // ── Automatic Schedule Generation ─────────────────────────────────────────

    [Fact]
    public void GenerateAutomaticSchedule_WithCountLessThan2_ShouldThrowDomainException()
    {
        var ex = Assert.Throws<DomainException>(() =>
            Installment.GenerateAutomaticSchedule(_txId, 1000m, 1, DateTime.UtcNow, InstallmentFrequency.Monthly));

        Assert.Contains("Installment count must be at least 2", ex.Message);
    }

    [Fact]
    public void GenerateAutomaticSchedule_EvenSplitWithRemainderOnLast_ShouldSumToExactTotal()
    {
        // 100 split 3 ways: 33.33 + 33.33 + 33.34 = 100.00
        var totalAmount = 100m;
        var installments = Installment.GenerateAutomaticSchedule(_txId, totalAmount, 3, DateTime.UtcNow, InstallmentFrequency.Monthly);

        Assert.Equal(3, installments.Count);
        Assert.Equal(33.33m, installments[0].Amount);
        Assert.Equal(33.33m, installments[1].Amount);
        Assert.Equal(33.34m, installments[2].Amount);
        Assert.Equal(totalAmount, installments.Sum(i => i.Amount));
    }

    [Theory]
    [InlineData(InstallmentFrequency.Weekly)]
    [InlineData(InstallmentFrequency.Monthly)]
    [InlineData(InstallmentFrequency.Yearly)]
    public void GenerateAutomaticSchedule_ShouldCalculateCorrectDueDatesForFrequencies(InstallmentFrequency frequency)
    {
        var firstDueDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var installments = Installment.GenerateAutomaticSchedule(_txId, 1200m, 3, firstDueDate, frequency);

        Assert.Equal(firstDueDate, installments[0].DueDate);

        if (frequency == InstallmentFrequency.Weekly)
        {
            Assert.Equal(firstDueDate.AddDays(7), installments[1].DueDate);
            Assert.Equal(firstDueDate.AddDays(14), installments[2].DueDate);
        }
        else if (frequency == InstallmentFrequency.Monthly)
        {
            Assert.Equal(firstDueDate.AddMonths(1), installments[1].DueDate);
            Assert.Equal(firstDueDate.AddMonths(2), installments[2].DueDate);
        }
        else if (frequency == InstallmentFrequency.Yearly)
        {
            Assert.Equal(firstDueDate.AddYears(1), installments[1].DueDate);
            Assert.Equal(firstDueDate.AddYears(2), installments[2].DueDate);
        }
    }

    // ── Custom Schedule Generation ───────────────────────────────────────────

    [Fact]
    public void GenerateCustomSchedule_WithCountLessThan2_ShouldThrowDomainException()
    {
        var custom = new List<(decimal Amount, DateTime DueDate)>
        {
            (100m, DateTime.UtcNow)
        };

        var ex = Assert.Throws<DomainException>(() => Installment.GenerateCustomSchedule(_txId, custom));

        Assert.Contains("Installment count must be at least 2", ex.Message);
    }

    [Fact]
    public void GenerateCustomSchedule_WithZeroOrNegativeAmount_ShouldThrowDomainException()
    {
        var custom = new List<(decimal Amount, DateTime DueDate)>
        {
            (400m, DateTime.UtcNow),
            (0m, DateTime.UtcNow.AddMonths(1))
        };

        var ex = Assert.Throws<DomainException>(() => Installment.GenerateCustomSchedule(_txId, custom));

        Assert.Contains("Each installment amount must be greater than zero", ex.Message);
    }

    [Fact]
    public void GenerateCustomSchedule_WithValidUnevenAmounts_ShouldAssignSequentialNumbers()
    {
        var custom = new List<(decimal Amount, DateTime DueDate)>
        {
            (400m, DateTime.UtcNow),
            (600m, DateTime.UtcNow.AddMonths(1))
        };

        var installments = Installment.GenerateCustomSchedule(_txId, custom);

        Assert.Equal(2, installments.Count);
        Assert.Equal(1, installments[0].InstallmentNumber);
        Assert.Equal(400m, installments[0].Amount);
        Assert.Equal(2, installments[1].InstallmentNumber);
        Assert.Equal(600m, installments[1].Amount);
    }

    // ── Payment & Voiding Logic ──────────────────────────────────────────────

    [Fact]
    public void MarkAsPaid_WhenPending_ShouldUpdateStatusAndPaidAt()
    {
        var installment = Installment.CreateSingle(_txId, 1, 500m, DateTime.UtcNow.AddDays(5));

        installment.MarkAsPaid();

        Assert.Equal(InstallmentStatus.Paid, installment.Status);
        Assert.NotNull(installment.PaidAt);
    }

    [Fact]
    public void MarkAsPaid_WhenAlreadyPaid_ShouldThrowDomainException()
    {
        var installment = Installment.CreateSingle(_txId, 1, 500m, DateTime.UtcNow.AddDays(5));
        installment.MarkAsPaid();

        var ex = Assert.Throws<DomainException>(() => installment.MarkAsPaid());

        Assert.Contains("Installment is already paid", ex.Message);
    }

    [Fact]
    public void MarkAsPaid_WhenVoided_ShouldThrowDomainException()
    {
        var installment = Installment.CreateSingle(_txId, 1, 500m, DateTime.UtcNow.AddDays(5));
        installment.Void();

        var ex = Assert.Throws<DomainException>(() => installment.MarkAsPaid());

        Assert.Contains("Cannot pay a voided installment", ex.Message);
    }
}
