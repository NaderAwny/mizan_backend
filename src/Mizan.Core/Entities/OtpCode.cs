using Mizan.Core.Exceptions;
using System.Text.RegularExpressions;

namespace Mizan.Core.Entities;

public class OtpCode
{
    public int Id { get; private set; }
    public string Email { get; private set; } = string.Empty;
    public string Code { get; private set; } = string.Empty;
    public DateTime ExpiresAt { get; private set; }
    public int AttemptsCount { get; private set; } = 0;
    public bool IsUsed { get; private set; } = false;
    public DateTime CreatedAt { get; private set; } = DateTime.UtcNow;

    private const int MaxAttempts = 3;

    private OtpCode() { }

    public static OtpCode Create(string email, string code, int expirySeconds = 120)
    {
        if (string.IsNullOrWhiteSpace(email))
            throw new DomainException("البريد الإلكتروني مطلوب");

        // Validate email format
        var emailRegex = new Regex(@"^[a-zA-Z0-9._%+\-]+@[a-zA-Z0-9.\-]+\.[a-zA-Z]{2,}$", RegexOptions.IgnoreCase);
        if (!emailRegex.IsMatch(email.Trim()))
            throw new DomainException("صيغة البريد الإلكتروني غير صالحة");

        // Validate OTP code: exactly 6 numeric digits
        if (string.IsNullOrWhiteSpace(code) || code.Length != 6 || !code.All(char.IsDigit))
            throw new DomainException("كود التحقق يجب أن يتكون من 6 أرقام");

        return new OtpCode
        {
            Email = email.Trim().ToLowerInvariant(),
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

        // Validate input format before comparing
        if (string.IsNullOrWhiteSpace(inputCode) || inputCode.Trim().Length != 6 || !inputCode.Trim().All(char.IsDigit))
            throw new BadRequestException("كود التحقق يجب أن يتكون من 6 أرقام");

        AttemptsCount++;

        if (Code != inputCode.Trim())
            return false;

        IsUsed = true;
        return true;
    }

    public void MarkAsUsed() => IsUsed = true;
}
