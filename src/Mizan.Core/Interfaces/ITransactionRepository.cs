using Mizan.Core.Entities;
using Mizan.Core.Enums;

namespace Mizan.Core.Interfaces;

public interface ITransactionRepository
{
    Task<Transaction?> GetByIdAsync(Guid id, Guid ownerUserId, CancellationToken cancellationToken = default);

    Task<(IReadOnlyList<Transaction> Items, int TotalCount)> GetPagedByOwnerAsync(
        Guid ownerUserId,
        int page,
        int pageSize,
        Guid? contactId = null,
        TransactionType? type = null,
        DateTime? dateFrom = null,
        DateTime? dateTo = null,
        CancellationToken cancellationToken = default);

    Task<int> GetActiveCountByOwnerAsync(Guid ownerUserId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Transaction>> GetRecentActiveByOwnerAsync(Guid ownerUserId, int count, CancellationToken cancellationToken = default);

    Task AddAsync(Transaction transaction, CancellationToken cancellationToken = default);

    void Update(Transaction transaction);
}
