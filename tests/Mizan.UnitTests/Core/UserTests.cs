using Mizan.Core.Entities;
using Mizan.Core.Exceptions;
using Xunit;

namespace Mizan.UnitTests.Core;

public class UserTests
{
    [Theory]
    [InlineData("01012345678", "01012345678")]
    [InlineData("+201012345678", "01012345678")]
    [InlineData("201012345678", "01012345678")]
    [InlineData("01198765432", "01198765432")]
    [InlineData("01234567890", "01234567890")]
    [InlineData("01555555555", "01555555555")]
    [InlineData(" 010 1234 5678 ", "01012345678")]
    public void CreateUser_WithValidEgyptianPhone_ShouldNormalizeAndSucceed(string inputPhone, string expectedPhone)
    {
        // Act
        var user = User.Create(inputPhone, "أحمد", "علي");

        // Assert
        Assert.Equal(expectedPhone, user.WhatsAppNumber);
        Assert.Equal("أحمد", user.FirstName);
        Assert.Equal("علي", user.LastName);
        Assert.True(user.IsActive);
        Assert.Equal("customer", user.UserType);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("01412345678")] // Invalid prefix
    [InlineData("0101234")]     // Too short
    [InlineData("0101234567890")] // Too long
    [InlineData("abcdefghijk")]  // Non-numeric
    public void CreateUser_WithInvalidPhone_ShouldThrowDomainException(string invalidPhone)
    {
        // Act & Assert
        Assert.Throws<DomainException>(() => User.Create(invalidPhone, "أحمد", "علي"));
    }

    [Theory]
    [InlineData("customer")]
    [InlineData("shop_owner")]
    [InlineData("CUSTOMER")]
    [InlineData("SHOP_OWNER")]
    public void SetUserType_WithValidType_ShouldSucceed(string userType)
    {
        // Arrange
        var user = User.Create("01012345678", "أحمد", "علي");

        // Act
        user.SetUserType(userType);

        // Assert
        Assert.Equal(userType.ToLowerInvariant(), user.UserType);
    }

    [Theory]
    [InlineData("admin")]
    [InlineData("guest")]
    [InlineData("")]
    public void SetUserType_WithInvalidType_ShouldThrowDomainException(string invalidType)
    {
        // Arrange
        var user = User.Create("01012345678", "أحمد", "علي");

        // Act & Assert
        Assert.Throws<DomainException>(() => user.SetUserType(invalidType));
    }
}
