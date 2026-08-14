using Microsoft.AspNetCore.Http;
using Mizan.Application.DTOs.Transactions;
using Mizan.Application.Interfaces;
using Mizan.Core.Entities;
using Mizan.Core.Enums;
using Mizan.Core.Exceptions;
using Mizan.Core.Interfaces;

namespace Mizan.Application.Services;

public class TransactionService : ITransactionService
{
    private const int MinPageSize = 1;
    private const int MaxPageSize = 50;

    private readonly IUnitOfWork _unitOfWork;

    public TransactionService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<TransactionResponse> CreateAsync(
        int ownerUserId,
        CreateTransactionRequest request,
        CancellationToken cancellationToken = default)
    {
        // 1. Verify Contact belongs to caller
        var contact = await _unitOfWork.Contacts.GetByIdAsync(request.ContactId, ownerUserId, cancellationToken);
        if (contact == null)
            throw new NotFoundException("Contact not found");

        // 2. Create Transaction entity via factory
        var transaction = Transaction.Create(
            ownerUserId,
            request.ContactId,
            request.Type,
            request.Amount,
            request.TransactionDate,
            request.NoteType,
            request.NoteText,
            request.IsInstallment,
            request.InstallmentPlanMode);

        // 3. Generate Installments if IsInstallment is true
        List<Installment> installments = new();

        if (request.IsInstallment)
        {
            if (request.InstallmentPlanMode == InstallmentPlanMode.Automatic)
            {
                if (!request.InstallmentCount.HasValue || request.InstallmentCount.Value < 2)
                    throw new DomainException("Installment count must be at least 2");

                if (!request.FirstInstallmentDate.HasValue)
                    throw new DomainException("First installment date is required");

                if (!request.Frequency.HasValue)
                    throw new DomainException("Installment frequency is required");

                installments = Installment.GenerateAutomaticSchedule(
                    transaction.Id,
                    request.Amount,
                    request.InstallmentCount.Value,
                    request.FirstInstallmentDate.Value,
                    request.Frequency.Value);
            }
            else if (request.InstallmentPlanMode == InstallmentPlanMode.Custom)
            {
                if (request.CustomInstallments == null || request.CustomInstallments.Count < 2)
                    throw new DomainException("Installment count must be at least 2");

                // Critical validation: custom installment sum must equal transaction total amount
                decimal customSum = request.CustomInstallments.Sum(x => Math.Round(x.Amount, 2));
                if (Math.Round(customSum, 2) != Math.Round(request.Amount, 2))
                    throw new DomainException("Installment amounts must sum exactly to the total transaction amount");

                var items = request.CustomInstallments.Select(x => (x.Amount, x.DueDate)).ToList();
                installments = Installment.GenerateCustomSchedule(transaction.Id, items);
            }

            foreach (var inst in installments)
            {
                transaction.Installments.Add(inst);
            }
        }

        // 4. Save transaction and installments in one Unit-of-Work
        await _unitOfWork.Transactions.AddAsync(transaction, cancellationToken);
        if (installments.Count > 0)
        {
            await _unitOfWork.Installments.AddRangeAsync(installments, cancellationToken);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return MapToResponse(transaction, contact.Name);
    }

    public async Task<TransactionResponse> GetByIdAsync(
        int ownerUserId,
        int transactionId,
        CancellationToken cancellationToken = default)
    {
        var transaction = await GetOwnedTransactionOrThrowAsync(ownerUserId, transactionId, cancellationToken);
        return MapToResponse(transaction);
    }

    public async Task<PagedTransactionResponse> GetPagedAsync(
        int ownerUserId,
        int page,
        int pageSize,
        int? contactId = null,
        TransactionType? type = null,
        DateTime? dateFrom = null,
        DateTime? dateTo = null,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, MinPageSize, MaxPageSize);

        var (items, totalCount) = await _unitOfWork.Transactions.GetPagedByOwnerAsync(
            ownerUserId, page, pageSize, contactId, type, dateFrom, dateTo, cancellationToken);

        var totalPages = totalCount == 0 ? 0 : (int)Math.Ceiling((double)totalCount / pageSize);

        return new PagedTransactionResponse
        {
            Items = items.Select(t => MapToResponse(t)).ToList(),
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount,
            TotalPages = totalPages
        };
    }

    public async Task DeactivateAsync(
        int ownerUserId,
        int transactionId,
        CancellationToken cancellationToken = default)
    {
        var transaction = await GetOwnedTransactionOrThrowAsync(ownerUserId, transactionId, cancellationToken);
        transaction.Deactivate();

        _unitOfWork.Transactions.Update(transaction);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task<TransactionResponse> AttachVoiceNoteAsync(
        int ownerUserId,
        int transactionId,
        IFormFile audioFile,
        CancellationToken cancellationToken = default)
    {
        var transaction = await GetOwnedTransactionOrThrowAsync(ownerUserId, transactionId, cancellationToken);

        if (audioFile == null || audioFile.Length == 0)
            throw new BadRequestException("Audio file is required");

        // Allowed MIME types
        var allowedMimeTypes = new[] { "audio/mpeg", "audio/mp4", "audio/wav", "audio/m4a", "audio/webm", "audio/x-m4a", "audio/x-wav" };
        var contentType = audioFile.ContentType.ToLowerInvariant();
        if (!allowedMimeTypes.Contains(contentType))
            throw new BadRequestException("Invalid audio file format. Allowed formats: mp3, mp4, wav, m4a, webm");

        // 10MB limit
        if (audioFile.Length > 10 * 1024 * 1024)
            throw new BadRequestException("Audio file size exceeds maximum limit of 10MB");

        // Store under App_Data/voice-notes/{ownerUserId}/{guid}.{ext}
        var ext = Path.GetExtension(audioFile.FileName);
        if (string.IsNullOrWhiteSpace(ext))
        {
            ext = contentType switch
            {
                "audio/mpeg" => ".mp3",
                "audio/wav" or "audio/x-wav" => ".wav",
                "audio/webm" => ".webm",
                _ => ".m4a"
            };
        }

        var folderPath = Path.Combine(Directory.GetCurrentDirectory(), "App_Data", "voice-notes", ownerUserId.ToString());
        Directory.CreateDirectory(folderPath);

        var storedFileName = $"{Guid.NewGuid()}{ext}";
        var fullPath = Path.Combine(folderPath, storedFileName);

        using (var stream = new FileStream(fullPath, FileMode.Create))
        {
            await audioFile.CopyToAsync(stream, cancellationToken);
        }

        transaction.AttachVoiceNote(fullPath);
        _unitOfWork.Transactions.Update(transaction);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return MapToResponse(transaction);
    }

    public async Task<(FileStream Stream, string ContentType, string FileName)> GetVoiceNoteStreamAsync(
        int ownerUserId,
        int transactionId,
        CancellationToken cancellationToken = default)
    {
        var transaction = await GetOwnedTransactionOrThrowAsync(ownerUserId, transactionId, cancellationToken);

        if (transaction.NoteType != NoteType.Voice || string.IsNullOrWhiteSpace(transaction.NoteAudioPath))
            throw new NotFoundException("Voice note not found");

        if (!File.Exists(transaction.NoteAudioPath))
            throw new NotFoundException("Voice note file not found");

        var ext = Path.GetExtension(transaction.NoteAudioPath).ToLowerInvariant();
        var contentType = ext switch
        {
            ".mp3" => "audio/mpeg",
            ".mp4" or ".m4a" => "audio/mp4",
            ".wav" => "audio/wav",
            ".webm" => "audio/webm",
            _ => "application/octet-stream"
        };

        var stream = new FileStream(transaction.NoteAudioPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        var fileName = Path.GetFileName(transaction.NoteAudioPath);

        return (stream, contentType, fileName);
    }

    public async Task<TransactionResponse> MarkInstallmentPaidAsync(
        int ownerUserId,
        int transactionId,
        int installmentId,
        CancellationToken cancellationToken = default)
    {
        var transaction = await GetOwnedTransactionOrThrowAsync(ownerUserId, transactionId, cancellationToken);

        var installment = transaction.Installments.FirstOrDefault(i => i.Id == installmentId);
        if (installment == null)
        {
            installment = await _unitOfWork.Installments.GetByIdAsync(installmentId, cancellationToken);
            if (installment == null || installment.TransactionId != transaction.Id)
                throw new NotFoundException("Installment not found");
        }

        installment.MarkAsPaid();
        _unitOfWork.Installments.Update(installment);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return MapToResponse(transaction);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private async Task<Transaction> GetOwnedTransactionOrThrowAsync(
        int ownerUserId,
        int transactionId,
        CancellationToken cancellationToken)
    {
        var transaction = await _unitOfWork.Transactions.GetByIdAsync(transactionId, ownerUserId, cancellationToken);
        if (transaction == null)
            throw new NotFoundException("Transaction not found");

        return transaction;
    }

    private static TransactionResponse MapToResponse(Transaction transaction, string? contactName = null)
    {
        var installments = transaction.Installments
            .OrderBy(i => i.InstallmentNumber)
            .Select(i => new InstallmentResponse
            {
                Id = i.Id,
                InstallmentNumber = i.InstallmentNumber,
                Amount = i.Amount,
                DueDate = i.DueDate,
                Status = (i.Status == InstallmentStatus.Pending && i.DueDate.Date < DateTime.UtcNow.Date)
                    ? InstallmentStatus.Overdue
                    : i.Status,
                PaidAt = i.PaidAt
            })
            .ToList();

        decimal totalPaid = 0;
        decimal totalRemaining = 0;

        if (transaction.IsInstallment)
        {
            totalPaid = installments
                .Where(i => i.Status == InstallmentStatus.Paid)
                .Sum(i => i.Amount);

            totalRemaining = installments
                .Where(i => i.Status == InstallmentStatus.Pending || i.Status == InstallmentStatus.Overdue)
                .Sum(i => i.Amount);
        }
        else
        {
            totalPaid = 0;
            totalRemaining = transaction.Amount;
        }

        return new TransactionResponse
        {
            Id = transaction.Id,
            ContactId = transaction.ContactId,
            ContactName = contactName ?? transaction.Contact?.Name ?? string.Empty,
            Type = transaction.Type,
            Amount = transaction.Amount,
            TransactionDate = transaction.TransactionDate,
            IsInstallment = transaction.IsInstallment,
            InstallmentPlanMode = transaction.InstallmentPlanMode,
            NoteType = transaction.NoteType,
            NoteText = transaction.NoteText,
            HasVoiceNote = transaction.NoteType == NoteType.Voice && !string.IsNullOrWhiteSpace(transaction.NoteAudioPath),
            Installments = installments,
            TotalPaid = totalPaid,
            TotalRemaining = totalRemaining,
            IsActive = transaction.IsActive,
            CreatedAt = transaction.CreatedAt,
            UpdatedAt = transaction.UpdatedAt
        };
    }
}
