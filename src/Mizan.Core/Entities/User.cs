using Mizan.Core.Exceptions;
using System.Text.RegularExpressions;

namespace Mizan.Core.Entities;

public class User
{
    public int Id { get; private set; }
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
        var user = new User();
        user.SetEmail(email);
        user.UpdateProfile(firstName, lastName);
        user.SetUserType(userType);
        user.CreatedAt = DateTime.UtcNow;
        user.IsActive = true;
        return user;
    }

    public void SetEmail(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
            throw new DomainException("البريد الإلكتروني مطلوب");

        email = email.Trim().ToLowerInvariant();

        // Validate proper email format
        var emailRegex = new Regex(@"^[a-zA-Z0-9._%+\-]+@[a-zA-Z0-9.\-]+\.[a-zA-Z]{2,}$", RegexOptions.IgnoreCase);
        if (!emailRegex.IsMatch(email))
            throw new DomainException("صيغة البريد الإلكتروني غير صالحة");

        if (email.Length > 100)
            throw new DomainException("البريد الإلكتروني لا يمكن أن يتجاوز 100 حرف");

        Email = email;
    }

    public void UpdateProfile(string firstName, string lastName)
    {
        if (string.IsNullOrWhiteSpace(firstName))
            throw new DomainException("الاسم الأول مطلوب");

        if (string.IsNullOrWhiteSpace(lastName))
            throw new DomainException("الاسم الأخير مطلوب");

        firstName = firstName.Trim();
        lastName = lastName.Trim();

        if (firstName.Length > 50)
            throw new DomainException("الاسم الأول لا يمكن أن يتجاوز 50 حرف");

        if (lastName.Length > 50)
            throw new DomainException("الاسم الأخير لا يمكن أن يتجاوز 50 حرف");

        // Only allow letters (Arabic + English) and spaces
        var nameRegex = new Regex(@"^[\u0600-\u06FFa-zA-Z\s]+$");
        if (!nameRegex.IsMatch(firstName))
            throw new DomainException("الاسم الأول يجب أن يحتوي على أحرف فقط (عربي أو إنجليزي)");

        if (!nameRegex.IsMatch(lastName))
            throw new DomainException("الاسم الأخير يجب أن يحتوي على أحرف فقط (عربي أو إنجليزي)");

        FirstName = firstName;
        LastName = lastName;
    }

    public void SetUserType(string userType)
    {
        if (string.IsNullOrWhiteSpace(userType))
            throw new DomainException("نوع المستخدم مطلوب");

        userType = userType.Trim().ToLowerInvariant();
        if (userType != "customer" && userType != "shop_owner")
            throw new DomainException("نوع المستخدم يجب أن يكون customer أو shop_owner");

        UserType = userType;
    }

    public void SetPasswordHash(string? hash)
    {
        PasswordHash = hash;
    }

    public void Deactivate() => IsActive = false;
    public void Activate() => IsActive = true;
}
