using Mizan.Application.DTOs.Contacts;

namespace Mizan.Application.Interfaces;

public interface IContactService
{
    Task<ContactResponse> CreateAsync(Guid ownerUserId, CreateContactRequest request, CancellationToken cancellationToken = default);
    Task<ContactResponse> UpdateAsync(Guid ownerUserId, Guid contactId, UpdateContactRequest request, CancellationToken cancellationToken = default);
    Task DeactivateAsync(Guid ownerUserId, Guid contactId, CancellationToken cancellationToken = default);
    Task<ContactResponse> GetByIdAsync(Guid ownerUserId, Guid contactId, CancellationToken cancellationToken = default);
    Task<PagedContactResponse> GetPagedAsync(Guid ownerUserId, int page, int pageSize, string? searchTerm, CancellationToken cancellationToken = default);
}
