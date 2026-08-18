using Mizan.Core.Entities;

namespace Mizan.Core.Interfaces;

public interface IContactRepository
{
    Task<Contact?> GetByIdAsync(Guid id, Guid ownerUserId, CancellationToken cancellationToken = default);

    Task<Contact?> GetByIdWithTransactionsAsync(Guid id, Guid ownerUserId, CancellationToken cancellationToken = default);

    Task<(IReadOnlyList<Contact> Items, int TotalCount)> GetPagedByOwnerAsync(
        Guid ownerUserId,
        int page,
        int pageSize,
        string? searchTerm,
        CancellationToken cancellationToken = default);

    Task<(IReadOnlyList<Contact> Items, int TotalCount)> GetVipPagedAsync(
        Guid ownerUserId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task AddAsync(Contact contact, CancellationToken cancellationToken = default);

    void Update(Contact contact);
}
