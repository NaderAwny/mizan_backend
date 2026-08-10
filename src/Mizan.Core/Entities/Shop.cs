using Mizan.Core.Exceptions;

namespace Mizan.Core.Entities;

public class Shop
{
    public int Id { get; private set; }
    public int OwnerId { get; private set; }
    public string ShopName { get; private set; } = string.Empty;
    public string Address { get; private set; } = string.Empty;
    public DateTime CreatedAt { get; private set; } = DateTime.UtcNow;

    // Navigation property
    public User Owner { get; private set; } = null!;

    private Shop() { }

    public static Shop Create(int ownerId, string shopName, string address = "")
    {
        if (ownerId <= 0)
            throw new DomainException("معرف المالك غير صحيح");

        if (string.IsNullOrWhiteSpace(shopName))
            throw new DomainException("اسم المحل مطلوب");

        return new Shop
        {
            OwnerId = ownerId,
            ShopName = shopName.Trim(),
            Address = address?.Trim() ?? string.Empty,
            CreatedAt = DateTime.UtcNow
        };
    }

    public void Update(string shopName, string address)
    {
        if (string.IsNullOrWhiteSpace(shopName))
            throw new DomainException("اسم المحل مطلوب");

        ShopName = shopName.Trim();
        Address = address?.Trim() ?? string.Empty;
    }
}
