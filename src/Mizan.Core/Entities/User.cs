using Mizan.Core.Exceptions;

namespace Mizan.Core.Entities;

public class User
{
    public int Id { get; private set; }
    public string WhatsAppNumber { get; private set; } = string.Empty;
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

    public static User Create(string whatsappNumber, string firstName, string lastName, string userType = "customer")
    {
        var user = new User();
        user.SetWhatsAppNumber(whatsappNumber);
        user.UpdateProfile(firstName, lastName);
        user.SetUserType(userType);
        user.CreatedAt = DateTime.UtcNow;
        user.IsActive = true;
        return user;
    }

    public void SetWhatsAppNumber(string phoneOrIdentifier)
    {
        if (string.IsNullOrWhiteSpace(phoneOrIdentifier))
            throw new DomainException("البريد الإلكتروني أو رقم الهاتف مطلوب");

        phoneOrIdentifier = phoneOrIdentifier.Trim();

        // If Email
        if (phoneOrIdentifier.Contains('@'))
        {
            WhatsAppNumber = phoneOrIdentifier.ToLowerInvariant();
            return;
        }

        // If Phone
        var phone = phoneOrIdentifier.Replace(" ", "").Replace("-", "");

        if (phone.StartsWith("+20"))
            phone = "0" + phone[3..];
        else if (phone.StartsWith("20") && phone.Length == 12)
            phone = "0" + phone[2..];

        if (phone.Length != 11)
            throw new DomainException("رقم الواتساب يجب أن يتكون من 11 رقم");

        var validPrefixes = new[] { "010", "011", "012", "015" };
        if (!validPrefixes.Any(p => phone.StartsWith(p)))
            throw new DomainException("رقم الواتساب يجب أن يبدأ بـ 010 أو 011 أو 012 أو 015");

        WhatsAppNumber = phone;
    }

    public void UpdateProfile(string firstName, string lastName)
    {
        if (string.IsNullOrWhiteSpace(firstName))
            throw new DomainException("الاسم الأول مطلوب");

        if (string.IsNullOrWhiteSpace(lastName))
            throw new DomainException("الاسم الأخير مطلوب");

        if (firstName.Trim().Length > 50)
            throw new DomainException("الاسم الأول لا يمكن أن يتجاوز 50 حرف");

        if (lastName.Trim().Length > 50)
            throw new DomainException("الاسم الأخير لا يمكن أن يتجاوز 50 حرف");

        FirstName = firstName.Trim();
        LastName = lastName.Trim();
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
