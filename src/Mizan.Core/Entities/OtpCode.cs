using Mizan.Core.Exceptions;

namespace Mizan.Core.Entities;

public class OtpCode
{
    public int Id { get; private set; }
    public string WhatsAppNumber { get; private set; } = string.Empty;
    public string Code { get; private set; } = string.Empty;
    public DateTime ExpiresAt { get; private set; }
    public int AttemptsCount { get; private set; } = 0;
    public bool IsUsed { get; private set; } = false;
    public DateTime CreatedAt { get; private set; } = DateTime.UtcNow;

    private const int MaxAttempts = 3;

    private OtpCode() { }

    public static OtpCode Create(string whatsappNumber, string code, int expirySeconds = 120)
    {
        if (string.IsNullOrWhiteSpace(whatsappNumber))
            throw new DomainException("رقم الواتساب مطلوب");

        if (string.IsNullOrWhiteSpace(code) || code.Length != 6)
            throw new DomainException("كود التحقق يجب أن يتكون من 6 أرقام");

        return new OtpCode
        {
            WhatsAppNumber = whatsappNumber,
            Code = code,
            ExpiresAt = DateTime.UtcNow.AddSeconds(expirySeconds),
            AttemptsCount = 0,
            IsUsed = false,
            CreatedAt = DateTime.UtcNow
        };
    }

    public bool Verify(string inputCode)
    {
        if (IsUsed)
            throw new BadRequestException("تم استخدام هذا الكود من قبل");

        if (DateTime.UtcNow > ExpiresAt)
            throw new BadRequestException("انتهت صلاحية كود التحقق");

        if (AttemptsCount >= MaxAttempts)
            throw new BadRequestException("تم تجاوز الحد الأقصى للمحاولات (3 محاولات). يرجى طلب كود جديد");

        AttemptsCount++;

        if (Code != inputCode.Trim())
            return false;

        IsUsed = true;
        return true;
    }

    public void MarkAsUsed() => IsUsed = true;
}
