using Microsoft.AspNetCore.Http;
using Mizan.Application.DTOs.Transactions;
using Mizan.Core.Enums;

namespace Mizan.Application.Interfaces;

public interface ITransactionService
{
    Task<TransactionResponse> CreateAsync(Guid ownerUserId, CreateTransactionRequest request, CancellationToken cancellationToken = default);

    Task<TransactionResponse> GetByIdAsync(Guid ownerUserId, Guid transactionId, CancellationToken cancellationToken = default);

    Task<PagedTransactionResponse> GetPagedAsync(
        Guid ownerUserId,
        int page,
        int pageSize,
        Guid? contactId = null,
        TransactionType? type = null,
        DateTime? dateFrom = null,
        DateTime? dateTo = null,
        CancellationToken cancellationToken = default);

    Task DeactivateAsync(Guid ownerUserId, Guid transactionId, CancellationToken cancellationToken = default);

    Task<TransactionResponse> AttachVoiceNoteAsync(Guid ownerUserId, Guid transactionId, IFormFile audioFile, CancellationToken cancellationToken = default);

    Task<(FileStream Stream, string ContentType, string FileName)> GetVoiceNoteStreamAsync(Guid ownerUserId, Guid transactionId, CancellationToken cancellationToken = default);

    Task<TransactionResponse> MarkInstallmentPaidAsync(Guid ownerUserId, Guid transactionId, Guid installmentId, CancellationToken cancellationToken = default);
}
