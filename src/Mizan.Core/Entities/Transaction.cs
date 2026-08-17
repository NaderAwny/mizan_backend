using Mizan.Core.Enums;
using Mizan.Core.Exceptions;

namespace Mizan.Core.Entities;

public class Transaction
{
    public Guid Id { get; private set; }
    public Guid OwnerUserId { get; private set; }
    public Guid ContactId { get; private set; }
    public TransactionType Type { get; private set; }
    public decimal Amount { get; private set; }
    public DateTime TransactionDate { get; private set; }
    public bool IsInstallment { get; private set; }
    public InstallmentPlanMode? InstallmentPlanMode { get; private set; }
    public NoteType NoteType { get; private set; } = NoteType.None;
    public string? NoteText { get; private set; }
    public string? NoteAudioPath { get; private set; }
    public bool IsActive { get; private set; } = true;
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    // Navigation properties
    public User? Owner { get; private set; }
    public Contact? Contact { get; private set; }
    public ICollection<Installment> Installments { get; private set; } = new List<Installment>();

    private Transaction() { } // Required for EF Core

    public static Transaction Create(
        Guid ownerUserId,
        Guid contactId,
        TransactionType type,
        decimal amount,
        DateTime transactionDate,
        NoteType noteType = NoteType.None,
        string? noteText = null,
        bool isInstallment = false,
        InstallmentPlanMode? installmentPlanMode = null)
    {
        if (amount <= 0)
            throw new DomainException("Amount must be greater than zero");

        if (amount > 999999999m)
            throw new DomainException("Amount exceeds maximum allowed value");

        if (transactionDate.Date > DateTime.UtcNow.Date.AddDays(1))
            throw new DomainException("Transaction date cannot be in the future");

        string? processedNoteText = null;

        if (noteType == NoteType.Voice)
            throw new DomainException("Voice notes cannot be set during creation");

        if (noteType == NoteType.Text)
        {
            if (string.IsNullOrWhiteSpace(noteText))
                throw new DomainException("Note text is required when note type is Text");

            processedNoteText = noteText.Trim();
            if (processedNoteText.Length > 1000)
                throw new DomainException("Note text must not exceed 1000 characters");
        }

        if (isInstallment && installmentPlanMode == null)
            throw new DomainException("Installment plan mode is required for installment transactions");

        var now = DateTime.UtcNow;

        return new Transaction
        {
            Id = Guid.NewGuid(),
            OwnerUserId = ownerUserId,
            ContactId = contactId,
            Type = type,
            Amount = amount,
            TransactionDate = transactionDate,
            IsInstallment = isInstallment,
            InstallmentPlanMode = isInstallment ? installmentPlanMode : null,
            NoteType = noteType,
            NoteText = processedNoteText,
            NoteAudioPath = null,
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now
        };
    }

    public void AttachVoiceNote(string storagePath)
    {
        if (NoteType == NoteType.Text && !string.IsNullOrWhiteSpace(NoteText))
            throw new DomainException("Cannot attach voice note: text note already set");

        if (string.IsNullOrWhiteSpace(storagePath))
            throw new DomainException("Storage path is required");

        NoteType = NoteType.Voice;
        NoteAudioPath = storagePath.Trim();
        NoteText = null;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Deactivate()
    {
        IsActive = false;
        UpdatedAt = DateTime.UtcNow;

        foreach (var installment in Installments)
        {
            if (installment.Status != InstallmentStatus.Paid)
            {
                installment.Void();
            }
        }
    }
}
