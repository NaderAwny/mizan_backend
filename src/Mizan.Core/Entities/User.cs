using System.Net.Mail;
using System.Text.RegularExpressions;
using Mizan.Core.Exceptions;

namespace Mizan.Core.Entities;

public class User
{
    public Guid Id { get; private set; }
    public string Email { get; private set; } = string.Empty;
    public string FirstName { get; private set; } = string.Empty;
    public string LastName { get; private set; } = string.Empty;
    public string UserType { get; private set; } = "customer"; // "customer" | "shop_owner"
    public string? PasswordHash { get; private set; }
    public bool IsActive { get; private set; } = true;
    public DateTime CreatedAt { get; private set; } = DateTime.UtcNow;

    // Navigation properties
    public Shop? Shop { get; private set; }
    public ICollection<RefreshToken> RefreshTokens { get; private set; } = new List<RefreshToken>();

    private User() { } // Required for EF Core

    public static User Create(string email, string firstName, string lastName, string userType = "customer")
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            IsActive = true
        };
        user.SetEmail(email);
        user.UpdateProfile(firstName, lastName);
        user.SetUserType(userType);
        return user;
    }

    public void SetEmail(string email)
    {
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(email.Trim()))
            throw new DomainException("Email is required");

        email = email.Trim().ToLowerInvariant();

        if (email.Length > 254)
            throw new DomainException("Email must not exceed 254 characters");

        try
        {
            var mailAddress = new MailAddress(email);
            if (mailAddress.Address != email)
                throw new DomainException("Invalid email format");
        }
        catch
        {
            throw new DomainException("Invalid email format");
        }

        Email = email;
    }

    public void UpdateProfile(string firstName, string lastName)
    {
        if (string.IsNullOrWhiteSpace(firstName) || string.IsNullOrWhiteSpace(firstName.Trim()))
            throw new DomainException("First name is required");

        if (string.IsNullOrWhiteSpace(lastName) || string.IsNullOrWhiteSpace(lastName.Trim()))
            throw new DomainException("Last name is required");

        firstName = firstName.Trim();
        lastName = lastName.Trim();

        if (firstName.Length > 50)
            throw new DomainException("First name must not exceed 50 characters");

        if (lastName.Length > 50)
            throw new DomainException("Last name must not exceed 50 characters");

        // Allow letters (any language) and spaces only - reject digits and symbols
        var nameRegex = new Regex(@"^[\p{L}\s]+$", RegexOptions.Compiled);
        if (!nameRegex.IsMatch(firstName))
            throw new DomainException("First name must contain letters and spaces only");

        if (!nameRegex.IsMatch(lastName))
            throw new DomainException("Last name must contain letters and spaces only");

        FirstName = firstName;
        LastName = lastName;
    }

    public void SetUserType(string userType)
    {
        if (string.IsNullOrWhiteSpace(userType) || string.IsNullOrWhiteSpace(userType.Trim()))
            throw new DomainException("User type is required");

        userType = userType.Trim().ToLowerInvariant();
        if (userType != "customer" && userType != "shop_owner")
            throw new DomainException("User type must be either customer or shop_owner");

        UserType = userType;
    }

    public void SetPasswordHash(string? hash)
    {
        PasswordHash = hash;
    }

    public void Deactivate() => IsActive = false;
    public void Activate() => IsActive = true;
}
