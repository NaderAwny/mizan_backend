using Mizan.Core.Entities;
using Mizan.Core.Exceptions;
using Xunit;

namespace Mizan.UnitTests.Core;

public class UserTests
{
    [Theory]
    [InlineData("test@example.com", "test@example.com")]
    [InlineData("USER@EXAMPLE.COM", "user@example.com")]
    [InlineData("  ahmed@gmail.com  ", "ahmed@gmail.com")]
    [InlineData("name+tag@domain.org", "name+tag@domain.org")]
    public void CreateUser_WithValidEmail_ShouldNormalizeAndSucceed(string inputEmail, string expectedEmail)
    {
        // Act
        var user = User.Create(inputEmail, "أحمد", "علي");

        // Assert
        Assert.Equal(expectedEmail, user.Email);
        Assert.Equal("أحمد", user.FirstName);
        Assert.Equal("علي", user.LastName);
        Assert.True(user.IsActive);
        Assert.Equal("customer", user.UserType);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("notanemail")]
    [InlineData("missing@")]
    [InlineData("@nodomain.com")]
    [InlineData("no spaces@domain.com")]
    public void CreateUser_WithInvalidEmail_ShouldThrowDomainException(string invalidEmail)
    {
        // Act & Assert
        Assert.Throws<DomainException>(() => User.Create(invalidEmail, "أحمد", "علي"));
    }

    [Theory]
    [InlineData("Ahmed123", "أحمد")]
    [InlineData("أحمد123", "علي")]
    [InlineData("Name!", "علي")]
    public void CreateUser_WithInvalidName_ShouldThrowDomainException(string invalidFirst, string validLast)
    {
        // Act & Assert
        Assert.Throws<DomainException>(() => User.Create("valid@email.com", invalidFirst, validLast));
    }

    [Theory]
    [InlineData("customer")]
    [InlineData("shop_owner")]
    [InlineData("CUSTOMER")]
    [InlineData("SHOP_OWNER")]
    public void SetUserType_WithValidType_ShouldSucceed(string userType)
    {
        // Arrange
        var user = User.Create("test@example.com", "أحمد", "علي");

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
        var user = User.Create("test@example.com", "أحمد", "علي");

        // Act & Assert
        Assert.Throws<DomainException>(() => user.SetUserType(invalidType));
    }
}
