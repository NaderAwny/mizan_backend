using Mizan.Core.Entities;
using Mizan.Core.Exceptions;
using Xunit;

namespace Mizan.UnitTests.Core;

public class ContactTests
{
    // ── Contact.Create — Valid cases ─────────────────────────────────────────

    [Theory]
    [InlineData("Mohamed Ali", null, null)]
    [InlineData("محمد علي", "01012345678", "ملاحظة")]
    [InlineData("  Ahmed  ", "+201012345678", null)]
    [InlineData("Jean-Pierre", null, "notes here")]
    [InlineData("O'Brien", null, null)]
    public void Create_WithValidData_ShouldSucceed(string name, string? phone, string? notes)
    {
        var contact = Contact.Create(1, name, phone, notes);

        Assert.Equal(name.Trim(), contact.Name);
        Assert.Equal(1, contact.OwnerUserId);
        Assert.True(contact.IsActive);
        Assert.NotEqual(Guid.Empty, contact.Id);
    }

    // ── Name validation ───────────────────────────────────────────────────────

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithEmptyOrWhitespaceName_ShouldThrowDomainException(string name)
    {
        var ex = Assert.Throws<DomainException>(() => Contact.Create(1, name, null, null));
        Assert.Contains("Contact name is required", ex.Message);
    }

    [Fact]
    public void Create_WithNameExceeding100Characters_ShouldThrowDomainException()
    {
        var longName = new string('A', 101);
        var ex = Assert.Throws<DomainException>(() => Contact.Create(1, longName, null, null));
        Assert.Contains("must not exceed 100 characters", ex.Message);
    }

    [Fact]
    public void Create_WithNameExactly100Characters_ShouldSucceed()
    {
        var name = new string('A', 100);
        var contact = Contact.Create(1, name, null, null);
        Assert.Equal(name, contact.Name);
    }

    [Theory]
    [InlineData("Name\u0000WithControl")]  // null character — control char
    [InlineData("Name<script>")]
    [InlineData("Name@#$%")]
    public void Create_WithInvalidCharactersInName_ShouldThrowDomainException(string name)
    {
        Assert.Throws<DomainException>(() => Contact.Create(1, name, null, null));
    }

    // ── Phone validation ──────────────────────────────────────────────────────

    [Theory]
    [InlineData("01012345678")]          // 11 digits, no prefix
    [InlineData("+201012345678")]        // +country code
    [InlineData("12345678")]             // 8 digits minimum
    [InlineData("123456789012345")]      // 15 digits maximum
    public void Create_WithValidPhoneNumber_ShouldSucceed(string phone)
    {
        var contact = Contact.Create(1, "Test", phone, null);
        Assert.Equal(phone, contact.PhoneNumber);
    }

    [Theory]
    [InlineData("123")]                  // too short (< 8 digits)
    [InlineData("1234567890123456")]     // too long (> 15 digits)
    [InlineData("abc12345678")]          // letters
    [InlineData("0101 234 5678")]        // spaces
    [InlineData("++201012345678")]       // double plus
    public void Create_WithInvalidPhoneNumber_ShouldThrowDomainException(string phone)
    {
        var ex = Assert.Throws<DomainException>(() => Contact.Create(1, "Test", phone, null));
        Assert.Contains("Invalid phone number format", ex.Message);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithNullOrEmptyPhone_ShouldSetPhoneToNull(string? phone)
    {
        var contact = Contact.Create(1, "Test", phone, null);
        Assert.Null(contact.PhoneNumber);
    }

    // ── Notes validation ──────────────────────────────────────────────────────

    [Fact]
    public void Create_WithNotesExceeding500Characters_ShouldThrowDomainException()
    {
        var longNotes = new string('X', 501);
        var ex = Assert.Throws<DomainException>(() => Contact.Create(1, "Test", null, longNotes));
        Assert.Contains("must not exceed 500 characters", ex.Message);
    }

    [Fact]
    public void Create_WithNotesExactly500Characters_ShouldSucceed()
    {
        var notes = new string('X', 500);
        var contact = Contact.Create(1, "Test", null, notes);
        Assert.Equal(notes, contact.Notes);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithNullOrEmptyNotes_ShouldSetNotesToNull(string? notes)
    {
        var contact = Contact.Create(1, "Test", null, notes);
        Assert.Null(contact.Notes);
    }

    // ── Update ────────────────────────────────────────────────────────────────

    [Fact]
    public void Update_WithValidData_ShouldUpdateAllFields()
    {
        var contact = Contact.Create(1, "Old Name", null, null);
        var originalCreatedAt = contact.CreatedAt;

        contact.Update("New Name", "+201012345678", "New notes");

        Assert.Equal("New Name", contact.Name);
        Assert.Equal("+201012345678", contact.PhoneNumber);
        Assert.Equal("New notes", contact.Notes);
        Assert.Equal(originalCreatedAt, contact.CreatedAt);  // CreatedAt must not change
        Assert.True(contact.UpdatedAt >= originalCreatedAt);
    }

    [Fact]
    public void Update_WithInvalidName_ShouldThrowDomainException()
    {
        var contact = Contact.Create(1, "Valid Name", null, null);
        Assert.Throws<DomainException>(() => contact.Update("", null, null));
    }

    // ── Activate / Deactivate ─────────────────────────────────────────────────

    [Fact]
    public void Deactivate_ShouldSetIsActiveToFalse()
    {
        var contact = Contact.Create(1, "Test", null, null);
        contact.Deactivate();
        Assert.False(contact.IsActive);
    }

    [Fact]
    public void Activate_ShouldSetIsActiveToTrue()
    {
        var contact = Contact.Create(1, "Test", null, null);
        contact.Deactivate();
        contact.Activate();
        Assert.True(contact.IsActive);
    }

    // ── Default values ────────────────────────────────────────────────────────

    [Fact]
    public void Create_DefaultValues_ShouldBeCorrect()
    {
        var before = DateTime.UtcNow.AddSeconds(-1);
        var contact = Contact.Create(42, "Test Contact", null, null);
        var after = DateTime.UtcNow.AddSeconds(1);

        Assert.True(contact.IsActive);
        Assert.NotEqual(Guid.Empty, contact.Id);
        Assert.Equal(42, contact.OwnerUserId);
        Assert.True(contact.CreatedAt >= before && contact.CreatedAt <= after);
        Assert.True(contact.UpdatedAt >= before && contact.UpdatedAt <= after);
    }
}
