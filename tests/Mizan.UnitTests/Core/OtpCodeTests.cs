using Mizan.Core.Entities;
using Mizan.Core.Exceptions;
using Xunit;

namespace Mizan.UnitTests.Core;

public class OtpCodeTests
{
    [Fact]
    public void CreateOtpCode_ShouldInitializeProperly()
    {
        // Act
        var otp = OtpCode.Create("test@example.com", "123456", expirySeconds: 120);

        // Assert
        Assert.Equal("test@example.com", otp.Email);
        Assert.Equal("123456", otp.Code);
        Assert.Equal(0, otp.AttemptsCount);
        Assert.False(otp.IsUsed);
        Assert.True(otp.ExpiresAt > DateTime.UtcNow);
    }

    [Fact]
    public void CreateOtpCode_WithInvalidEmail_ShouldThrowDomainException()
    {
        Assert.Throws<DomainException>(() => OtpCode.Create("notanemail", "123456"));
        Assert.Throws<DomainException>(() => OtpCode.Create("", "123456"));
    }

    [Fact]
    public void CreateOtpCode_WithInvalidCode_ShouldThrowDomainException()
    {
        Assert.Throws<DomainException>(() => OtpCode.Create("test@example.com", "12345"));   // 5 digits
        Assert.Throws<DomainException>(() => OtpCode.Create("test@example.com", "1234567")); // 7 digits
        Assert.Throws<DomainException>(() => OtpCode.Create("test@example.com", "abcdef"));  // non-numeric
    }

    [Fact]
    public void Verify_WithCorrectCode_ShouldReturnTrueAndMarkAsUsed()
    {
        // Arrange
        var otp = OtpCode.Create("test@example.com", "123456", expirySeconds: 120);

        // Act
        var result = otp.Verify("123456");

        // Assert
        Assert.True(result);
        Assert.True(otp.IsUsed);
        Assert.Equal(1, otp.AttemptsCount);
    }

    [Fact]
    public void Verify_WithIncorrectCode_ShouldReturnFalseAndIncrementAttempts()
    {
        // Arrange
        var otp = OtpCode.Create("test@example.com", "123456", expirySeconds: 120);

        // Act
        var result = otp.Verify("654321");

        // Assert
        Assert.False(result);
        Assert.False(otp.IsUsed);
        Assert.Equal(1, otp.AttemptsCount);
    }

    [Fact]
    public void Verify_ExceedingMaxAttempts_ShouldThrowBadRequestException()
    {
        // Arrange
        var otp = OtpCode.Create("test@example.com", "123456", expirySeconds: 120);

        // 3 failed attempts
        otp.Verify("000001");
        otp.Verify("000002");
        otp.Verify("000003");

        // 4th attempt should throw
        var ex = Assert.Throws<BadRequestException>(() => otp.Verify("123456"));
        Assert.Contains("3 محاولات", ex.Message);
    }

    [Fact]
    public void Verify_ExpiredOtp_ShouldThrowBadRequestException()
    {
        // Arrange with negative expiry to simulate expiration
        var otp = OtpCode.Create("test@example.com", "123456", expirySeconds: -10);

        // Act & Assert
        var ex = Assert.Throws<BadRequestException>(() => otp.Verify("123456"));
        Assert.Contains("انتهت صلاحية", ex.Message);
    }

    [Fact]
    public void Verify_AlreadyUsedOtp_ShouldThrowBadRequestException()
    {
        // Arrange
        var otp = OtpCode.Create("test@example.com", "123456", expirySeconds: 120);
        otp.Verify("123456"); // First verification succeeds and marks as used

        // Act & Assert (second attempt)
        var ex = Assert.Throws<BadRequestException>(() => otp.Verify("123456"));
        Assert.Contains("تم استخدام هذا الكود", ex.Message);
    }
}
