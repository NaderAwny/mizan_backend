using Mizan.Core.Entities;

namespace Mizan.Core.Interfaces;

public interface IContactRepository
{
    Task<Contact?> GetByIdAsync(int id, int ownerUserId, CancellationToken cancellationToken = default);

    Task<(IReadOnlyList<Contact> Items, int TotalCount)> GetPagedByOwnerAsync(
        int ownerUserId,
        int page,
        int pageSize,
        string? searchTerm,
        CancellationToken cancellationToken = default);

    Task AddAsync(Contact contact, CancellationToken cancellationToken = default);

    void Update(Contact contact);
}
