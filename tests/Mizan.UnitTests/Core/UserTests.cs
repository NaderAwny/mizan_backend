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
    [InlineData("plainaddress")]
    [InlineData("@missingusername.com")]
    public void CreateUser_WithInvalidEmail_ShouldThrowDomainException(string invalidEmail)
    {
        // Act & Assert
        var ex = Assert.Throws<DomainException>(() => User.Create(invalidEmail, "أحمد", "علي"));
        Assert.True(ex.Message.Contains("Email is required") || ex.Message.Contains("Invalid email format"));
    }

    [Fact]
    public void CreateUser_WithEmailExceeding254Characters_ShouldThrowDomainException()
    {
        var longEmail = new string('a', 250) + "@example.com"; // > 254 chars
        var ex = Assert.Throws<DomainException>(() => User.Create(longEmail, "أحمد", "علي"));
        Assert.Contains("must not exceed 254 characters", ex.Message);
    }

    [Theory]
    [InlineData("Ahmed123", "Ali")]
    [InlineData("أحمد123", "علي")]
    [InlineData("Name!", "Ali")]
    [InlineData("Ahmed", "Ali@")]
    [InlineData("12345", "Ali")]
    public void CreateUser_WithDigitsOrSymbolsInName_ShouldThrowDomainException(string firstName, string lastName)
    {
        // Act & Assert
        var ex = Assert.Throws<DomainException>(() => User.Create("valid@email.com", firstName, lastName));
        Assert.Contains("must contain letters and spaces only", ex.Message);
    }

    [Theory]
    [InlineData("", "علي")]
    [InlineData("   ", "علي")]
    [InlineData("أحمد", "")]
    [InlineData("أحمد", "   ")]
    public void CreateUser_WithEmptyOrWhitespaceOnlyName_ShouldThrowDomainException(string firstName, string lastName)
    {
        Assert.Throws<DomainException>(() => User.Create("valid@email.com", firstName, lastName));
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
