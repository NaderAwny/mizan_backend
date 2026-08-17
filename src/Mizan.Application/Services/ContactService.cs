using Mizan.Application.DTOs.Contacts;
using Mizan.Application.Interfaces;
using Mizan.Core.Entities;
using Mizan.Core.Exceptions;
using Mizan.Core.Interfaces;

namespace Mizan.Application.Services;

public class ContactService : IContactService
{
    private const int MaxPageSize = 50;
    private const int MinPageSize = 1;

    private readonly IUnitOfWork _unitOfWork;

    public ContactService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<ContactResponse> CreateAsync(Guid ownerUserId, CreateContactRequest request, CancellationToken cancellationToken = default)
    {
        var contact = Contact.Create(ownerUserId, request.Name, request.PhoneNumber, request.Notes);
        await _unitOfWork.Contacts.AddAsync(contact, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return MapToResponse(contact);
    }

    public async Task<ContactResponse> UpdateAsync(Guid ownerUserId, Guid contactId, UpdateContactRequest request, CancellationToken cancellationToken = default)
    {
        var contact = await GetOwnedContactOrThrowAsync(ownerUserId, contactId, cancellationToken);
        contact.Update(request.Name, request.PhoneNumber, request.Notes);
        _unitOfWork.Contacts.Update(contact);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return MapToResponse(contact);
    }

    public async Task DeactivateAsync(Guid ownerUserId, Guid contactId, CancellationToken cancellationToken = default)
    {
        var contact = await GetOwnedContactOrThrowAsync(ownerUserId, contactId, cancellationToken);
        contact.Deactivate();
        _unitOfWork.Contacts.Update(contact);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task<ContactResponse> GetByIdAsync(Guid ownerUserId, Guid contactId, CancellationToken cancellationToken = default)
    {
        var contact = await GetOwnedContactOrThrowAsync(ownerUserId, contactId, cancellationToken);
        return MapToResponse(contact);
    }

    public async Task<PagedContactResponse> GetPagedAsync(Guid ownerUserId, int page, int pageSize, string? searchTerm, CancellationToken cancellationToken = default)
    {
        // Clamp server-side — never trust raw client values
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, MinPageSize, MaxPageSize);

        var (items, totalCount) = await _unitOfWork.Contacts.GetPagedByOwnerAsync(
            ownerUserId, page, pageSize, searchTerm, cancellationToken);

        var totalPages = totalCount == 0 ? 0 : (int)Math.Ceiling((double)totalCount / pageSize);

        return new PagedContactResponse
        {
            Items = items.Select(MapToResponse).ToList(),
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount,
            TotalPages = totalPages
        };
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Fetches a contact that belongs to the given owner.
    /// Throws NotFoundException in BOTH cases (not found + wrong owner)
    /// to prevent enumeration attacks — never leak whether an id exists for a different user.
    /// </summary>
    private async Task<Contact> GetOwnedContactOrThrowAsync(Guid ownerUserId, Guid contactId, CancellationToken cancellationToken)
    {
        var contact = await _unitOfWork.Contacts.GetByIdAsync(contactId, ownerUserId, cancellationToken);
        if (contact == null)
            throw new NotFoundException("Contact not found");
        return contact;
    }

    private static ContactResponse MapToResponse(Contact contact) => new()
    {
        Id = contact.Id,
        Name = contact.Name,
        PhoneNumber = contact.PhoneNumber,
        Notes = contact.Notes,
        IsActive = contact.IsActive,
        CreatedAt = contact.CreatedAt,
        UpdatedAt = contact.UpdatedAt
    };
}
