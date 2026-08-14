using Mizan.Core.Entities;
using Mizan.Core.Enums;
using Mizan.Core.Exceptions;
using Xunit;

namespace Mizan.UnitTests.Core;

public class TransactionTests
{
    private readonly int _contactId = 1;

    // ── Amount Validation ─────────────────────────────────────────────────────

    [Theory]
    [InlineData(0)]
    [InlineData(-100)]
    [InlineData(-0.01)]
    public void Create_WithZeroOrNegativeAmount_ShouldThrowDomainException(decimal amount)
    {
        var ex = Assert.Throws<DomainException>(() =>
            Transaction.Create(1, _contactId, TransactionType.Sale, amount, DateTime.UtcNow));

        Assert.Contains("Amount must be greater than zero", ex.Message);
    }

    [Fact]
    public void Create_WithAmountExceedingMaximum_ShouldThrowDomainException()
    {
        var ex = Assert.Throws<DomainException>(() =>
            Transaction.Create(1, _contactId, TransactionType.Sale, 1_000_000_000m, DateTime.UtcNow));

        Assert.Contains("Amount exceeds maximum allowed value", ex.Message);
    }

    [Fact]
    public void Create_WithValidAmount_ShouldSucceed()
    {
        var transaction = Transaction.Create(1, _contactId, TransactionType.Sale, 1500.50m, DateTime.UtcNow);

        Assert.Equal(1500.50m, transaction.Amount);
    }

    // ── TransactionDate Validation ───────────────────────────────────────────

    [Fact]
    public void Create_WithDateMoreThan1DayInFuture_ShouldThrowDomainException()
    {
        var futureDate = DateTime.UtcNow.AddDays(2);

        var ex = Assert.Throws<DomainException>(() =>
            Transaction.Create(1, _contactId, TransactionType.Sale, 500m, futureDate));

        Assert.Contains("Transaction date cannot be in the future", ex.Message);
    }

    [Fact]
    public void Create_WithDateTodayOrPast_ShouldSucceed()
    {
        var pastDate = DateTime.UtcNow.AddDays(-10);
        var transaction = Transaction.Create(1, _contactId, TransactionType.Sale, 500m, pastDate);

        Assert.Equal(pastDate, transaction.TransactionDate);
    }

    // ── NoteType & NoteText Consistency ──────────────────────────────────────

    [Fact]
    public void Create_WithVoiceNoteType_ShouldThrowDomainException()
    {
        var ex = Assert.Throws<DomainException>(() =>
            Transaction.Create(1, _contactId, TransactionType.Sale, 100m, DateTime.UtcNow, noteType: NoteType.Voice));

        Assert.Contains("Voice notes cannot be set during creation", ex.Message);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithTextNoteTypeAndEmptyText_ShouldThrowDomainException(string? noteText)
    {
        var ex = Assert.Throws<DomainException>(() =>
            Transaction.Create(1, _contactId, TransactionType.Sale, 100m, DateTime.UtcNow, noteType: NoteType.Text, noteText: noteText));

        Assert.Contains("Note text is required when note type is Text", ex.Message);
    }

    [Fact]
    public void Create_WithTextNoteTypeExceeding1000Chars_ShouldThrowDomainException()
    {
        var longText = new string('A', 1001);

        var ex = Assert.Throws<DomainException>(() =>
            Transaction.Create(1, _contactId, TransactionType.Sale, 100m, DateTime.UtcNow, noteType: NoteType.Text, noteText: longText));

        Assert.Contains("must not exceed 1000 characters", ex.Message);
    }

    [Fact]
    public void Create_WithNoneNoteType_ShouldSetNoteTextToNull()
    {
        var transaction = Transaction.Create(1, _contactId, TransactionType.Sale, 100m, DateTime.UtcNow, noteType: NoteType.None, noteText: "ignored");

        Assert.Equal(NoteType.None, transaction.NoteType);
        Assert.Null(transaction.NoteText);
        Assert.Null(transaction.NoteAudioPath);
    }

    // ── Voice Note Attachment ────────────────────────────────────────────────

    [Fact]
    public void AttachVoiceNote_WhenTextNoteIsSet_ShouldThrowDomainException()
    {
        var transaction = Transaction.Create(1, _contactId, TransactionType.Sale, 100m, DateTime.UtcNow, noteType: NoteType.Text, noteText: "Text note");

        var ex = Assert.Throws<DomainException>(() => transaction.AttachVoiceNote("/path/to/voice.mp3"));

        Assert.Contains("Cannot attach voice note: text note already set", ex.Message);
    }

    [Fact]
    public void AttachVoiceNote_WhenValid_ShouldSetVoiceNoteProperties()
    {
        var transaction = Transaction.Create(1, _contactId, TransactionType.Sale, 100m, DateTime.UtcNow);

        transaction.AttachVoiceNote("App_Data/voice-notes/1/file.mp3");

        Assert.Equal(NoteType.Voice, transaction.NoteType);
        Assert.Equal("App_Data/voice-notes/1/file.mp3", transaction.NoteAudioPath);
        Assert.Null(transaction.NoteText);
    }

    // ── Deactivation & Installment Voiding ───────────────────────────────────

    [Fact]
    public void Deactivate_ShouldSetIsActiveFalseAndVoidPendingInstallments()
    {
        var transaction = Transaction.Create(
            1, _contactId, TransactionType.Sale, 1000m, DateTime.UtcNow,
            isInstallment: true, installmentPlanMode: InstallmentPlanMode.Automatic);

        var installments = Installment.GenerateAutomaticSchedule(
            transaction.Id, 1000m, 3, DateTime.UtcNow.AddDays(7), InstallmentFrequency.Weekly);

        foreach (var inst in installments)
        {
            transaction.Installments.Add(inst);
        }

        // Mark 1st installment as Paid
        transaction.Installments.First().MarkAsPaid();

        transaction.Deactivate();

        Assert.False(transaction.IsActive);

        var instList = transaction.Installments.OrderBy(i => i.InstallmentNumber).ToList();
        Assert.Equal(InstallmentStatus.Paid, instList[0].Status);
        Assert.Equal(InstallmentStatus.Voided, instList[1].Status);
        Assert.Equal(InstallmentStatus.Voided, instList[2].Status);
    }
}
