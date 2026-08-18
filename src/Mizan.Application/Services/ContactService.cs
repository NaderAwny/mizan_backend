using Mizan.Application.DTOs.Contacts;
using Mizan.Application.DTOs.Transactions;
using Mizan.Application.Interfaces;
using Mizan.Core.Entities;
using Mizan.Core.Enums;
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

        if (request.IsVip.HasValue)
        {
            contact.SetVip(request.IsVip.Value);
        }

        if (request.ContactEmail != null)
        {
            contact.SetContactEmail(request.ContactEmail);
        }

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

    public async Task<ContactResponse> ToggleVipAsync(Guid ownerUserId, Guid contactId, CancellationToken cancellationToken = default)
    {
        var contact = await GetOwnedContactOrThrowAsync(ownerUserId, contactId, cancellationToken);
        contact.SetVip(!contact.IsVip);
        _unitOfWork.Contacts.Update(contact);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return MapToResponse(contact);
    }

    public async Task<ContactTransactionsResponse> GetContactTransactionsAsync(
        Guid ownerUserId, Guid contactId, CancellationToken cancellationToken = default)
    {
        var contact = await _unitOfWork.Contacts.GetByIdWithTransactionsAsync(contactId, ownerUserId, cancellationToken);
        if (contact == null)
            throw new NotFoundException("Contact not found");

        var transactions = contact.Transactions?.Where(t => t.IsActive).ToList() ?? new List<Transaction>();
        return new ContactTransactionsResponse
        {
            ContactId = contact.Id,
            ContactName = contact.Name,
            PhoneNumber = contact.PhoneNumber,
            ContactEmail = contact.ContactEmail,
            IsVip = contact.IsVip,
            Transactions = transactions.Select(MapTransactionToResponse).ToList(),
            TotalTransactions = transactions.Count,
            TotalAmount = transactions.Sum(t => t.Amount)
        };
    }

    public async Task<PagedContactResponse> GetVipContactsAsync(
        Guid ownerUserId, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, MinPageSize, MaxPageSize);

        var (items, totalCount) = await _unitOfWork.Contacts.GetVipPagedAsync(
            ownerUserId, page, pageSize, cancellationToken);

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
        IsVip = contact.IsVip,
        ContactEmail = contact.ContactEmail,
        IsActive = contact.IsActive,
        CreatedAt = contact.CreatedAt,
        UpdatedAt = contact.UpdatedAt
    };

    private static TransactionResponse MapTransactionToResponse(Transaction t)
    {
        decimal totalPaid = 0;
        decimal totalRemaining = 0;

        var installments = t.Installments.Select(i => new InstallmentResponse
        {
            Id = i.Id,
            InstallmentNumber = i.InstallmentNumber,
            Amount = i.Amount,
            DueDate = i.DueDate,
            Status = i.Status,
            PaidAt = i.PaidAt
        }).ToList();

        if (t.IsInstallment)
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
            totalRemaining = t.Amount;
        }

        return new TransactionResponse
        {
            Id = t.Id,
            ShopId = t.ShopId,
            ContactId = t.ContactId,
            ContactName = t.Contact?.Name ?? string.Empty,
            PartyName = !string.IsNullOrWhiteSpace(t.PartyName) ? t.PartyName : (t.Contact?.Name ?? string.Empty),
            Type = t.Type,
            Amount = t.Amount,
            PaymentMethod = t.PaymentMethod,
            TransactionDate = t.TransactionDate,
            IsInstallment = t.IsInstallment,
            InstallmentPlanMode = t.InstallmentPlanMode,
            NoteType = t.NoteType,
            NoteText = t.NoteText,
            HasVoiceNote = t.NoteType == NoteType.Voice && !string.IsNullOrWhiteSpace(t.NoteAudioPath),
            Installments = installments,
            TotalPaid = totalPaid,
            TotalRemaining = totalRemaining,
            IsActive = t.IsActive,
            CreatedAt = t.CreatedAt,
            UpdatedAt = t.UpdatedAt
        };
    }
}
