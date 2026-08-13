using System.Net.Mail;
using System.Security.Cryptography;
using System.Text;
using Mizan.Core.Exceptions;

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

        if (string.IsNullOrWhiteSpace(code) || code.Length != 6 || !code.All(char.IsDigit))
            throw new DomainException("OTP code must be 6 digits");

        return new OtpCode
        {
            Email = email,
            Code = code,
            ExpiresAt = DateTime.UtcNow.AddSeconds(expirySeconds),
            AttemptsCount = 0,
            IsUsed = false,
            CreatedAt = DateTime.UtcNow
        };
    }

    public bool Verify(string? inputCode)
    {
        if (inputCode == null)
            throw new BadRequestException("Invalid or expired verification code");

        var trimmedInput = inputCode.Trim();

        if (IsUsed || DateTime.UtcNow > ExpiresAt || AttemptsCount >= MaxAttempts)
            throw new BadRequestException("Invalid or expired verification code");

        AttemptsCount++;

        var expectedBytes = Encoding.UTF8.GetBytes(Code);
        var inputBytes = Encoding.UTF8.GetBytes(trimmedInput);

        if (expectedBytes.Length != inputBytes.Length || !CryptographicOperations.FixedTimeEquals(expectedBytes, inputBytes))
            return false;

        IsUsed = true;
        return true;
    }

    public void MarkAsUsed() => IsUsed = true;
}
