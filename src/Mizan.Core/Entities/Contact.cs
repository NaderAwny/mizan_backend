using System.Text.RegularExpressions;
using Mizan.Core.Exceptions;

namespace Mizan.Core.Entities;

public class Contact
{
    public Guid Id { get; private set; }
    public Guid OwnerUserId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string? PhoneNumber { get; private set; }
    public string? Notes { get; private set; }
    public bool IsActive { get; private set; } = true;
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    // Navigation property
    public User? Owner { get; private set; }

    private Contact() { } // Required for EF Core

    public static Contact Create(Guid ownerUserId, string name, string? phoneNumber, string? notes)
    {
        var contact = new Contact
        {
            Id = Guid.NewGuid(),
            OwnerUserId = ownerUserId,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        contact.SetName(name);
        contact.SetPhoneNumber(phoneNumber);
        contact.SetNotes(notes);

        return contact;
    }

    public void Update(string name, string? phoneNumber, string? notes)
    {
        SetName(name);
        SetPhoneNumber(phoneNumber);
        SetNotes(notes);
        UpdatedAt = DateTime.UtcNow;
    }

    public void Deactivate()
    {
        IsActive = false;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Activate()
    {
        IsActive = true;
        UpdatedAt = DateTime.UtcNow;
    }

    // ── Private validation helpers ────────────────────────────────────────────

    private void SetName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new DomainException("Contact name is required");

        name = name.Trim();

        if (name.Length > 100)
            throw new DomainException("Contact name must not exceed 100 characters");

        // Allow Unicode letters (Arabic, Latin, etc.), spaces, hyphens, apostrophes, dots
        // Reject control characters and other symbols
        var nameRegex = new Regex(@"^[\p{L}\s\-\'\.\،]+$", RegexOptions.Compiled);
        if (!nameRegex.IsMatch(name))
            throw new DomainException("Contact name contains invalid characters");

        Name = name;
    }

    private void SetPhoneNumber(string? phoneNumber)
    {
        if (string.IsNullOrWhiteSpace(phoneNumber))
        {
            PhoneNumber = null;
            return;
        }

        phoneNumber = phoneNumber.Trim();

        // Optional leading +, then 8-15 digits
        var phoneRegex = new Regex(@"^\+?\d{8,15}$", RegexOptions.Compiled);
        if (!phoneRegex.IsMatch(phoneNumber))
            throw new DomainException("Invalid phone number format");

        PhoneNumber = phoneNumber;
    }

    private void SetNotes(string? notes)
    {
        if (string.IsNullOrWhiteSpace(notes))
        {
            Notes = null;
            return;
        }

        notes = notes.Trim();

        if (notes.Length > 500)
            throw new DomainException("Notes must not exceed 500 characters");

        Notes = notes;
    }
}
