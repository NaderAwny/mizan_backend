using Microsoft.AspNetCore.Http;
using Mizan.Application.DTOs.Transactions;
using Mizan.Core.Enums;

namespace Mizan.Application.Interfaces;

public interface ITransactionService
{
    Task<TransactionResponse> CreateAsync(int ownerUserId, CreateTransactionRequest request, CancellationToken cancellationToken = default);

    Task<TransactionResponse> GetByIdAsync(int ownerUserId, int transactionId, CancellationToken cancellationToken = default);

    Task<PagedTransactionResponse> GetPagedAsync(
        int ownerUserId,
        int page,
        int pageSize,
        int? contactId = null,
        TransactionType? type = null,
        DateTime? dateFrom = null,
        DateTime? dateTo = null,
        CancellationToken cancellationToken = default);

    Task DeactivateAsync(int ownerUserId, int transactionId, CancellationToken cancellationToken = default);

    Task<TransactionResponse> AttachVoiceNoteAsync(int ownerUserId, int transactionId, IFormFile audioFile, CancellationToken cancellationToken = default);

    Task<(FileStream Stream, string ContentType, string FileName)> GetVoiceNoteStreamAsync(int ownerUserId, int transactionId, CancellationToken cancellationToken = default);

    Task<TransactionResponse> MarkInstallmentPaidAsync(int ownerUserId, int transactionId, int installmentId, CancellationToken cancellationToken = default);
}
