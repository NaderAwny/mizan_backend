using Mizan.Application.DTOs.Contacts;

namespace Mizan.Application.Interfaces;

public interface IContactService
{
    Task<ContactResponse> CreateAsync(int ownerUserId, CreateContactRequest request, CancellationToken cancellationToken = default);
    Task<ContactResponse> UpdateAsync(int ownerUserId, Guid contactId, UpdateContactRequest request, CancellationToken cancellationToken = default);
    Task DeactivateAsync(int ownerUserId, Guid contactId, CancellationToken cancellationToken = default);
    Task<ContactResponse> GetByIdAsync(int ownerUserId, Guid contactId, CancellationToken cancellationToken = default);
    Task<PagedContactResponse> GetPagedAsync(int ownerUserId, int page, int pageSize, string? searchTerm, CancellationToken cancellationToken = default);
}
