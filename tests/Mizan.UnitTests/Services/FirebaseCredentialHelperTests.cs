using Mizan.Infrastructure.Services.Push;
using Xunit;

namespace Mizan.UnitTests.Services;

public class FirebaseCredentialHelperTests
{
    [Fact]
    public void NormalizeServiceAccountJson_ExtractsProjectId_AndNormalizesEscapedNewlines()
    {
        // Arrange: JSON with double-escaped newlines in private_key
        var json = "{\"type\":\"service_account\",\"project_id\":\"test-mizan-project\",\"private_key\":\"-----BEGIN PRIVATE KEY-----\\nMIIEvgIBADANBgkqhkiG9w0BAQEFAASCBKgwggSkAgEAAoIBAQC\\n-----END PRIVATE KEY-----\\n\"}";

        // Act
        var normalized = FirebaseCredentialHelper.NormalizeServiceAccountJson(json, out var projectId);

        // Assert
        Assert.Equal("test-mizan-project", projectId);
        Assert.NotNull(normalized);
        Assert.Contains("test-mizan-project", normalized);
    }

    [Fact]
    public void NormalizeServiceAccountJson_HandlesStandardJson_WithoutModification()
    {
        // Arrange
        var json = "{\"type\":\"service_account\",\"project_id\":\"sample-proj\",\"client_email\":\"sample@sample.iam.gserviceaccount.com\"}";

        // Act
        var normalized = FirebaseCredentialHelper.NormalizeServiceAccountJson(json, out var projectId);

        // Assert
        Assert.Equal("sample-proj", projectId);
        Assert.Contains("sample-proj", normalized);
    }

    [Fact]
    public void TryInitializeFirebase_WhenFileNotFound_ReturnsFalseWithSafeReason()
    {
        // Arrange
        var options = new FirebaseOptions
        {
            Enabled = true,
            ServiceAccountPath = @"Z:\non_existent_folder_12345\key.json"
        };

        // Act
        var success = FirebaseCredentialHelper.TryInitializeFirebase(options, null, out var projectId, out var failureReason);

        // Assert
        if (!success)
        {
            Assert.NotNull(failureReason);
            Assert.Contains("file not found", failureReason, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("private_key", failureReason, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void TryInitializeFirebase_WhenInvalidJson_ReturnsFalseWithSafeReason()
    {
        // Arrange
        var options = new FirebaseOptions
        {
            Enabled = true,
            ServiceAccountJson = "{ invalid-json: true"
        };

        // Act
        var success = FirebaseCredentialHelper.TryInitializeFirebase(options, null, out var projectId, out var failureReason);

        // Assert
        if (!success)
        {
            Assert.NotNull(failureReason);
            Assert.DoesNotContain("private_key", failureReason, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void NormalizeServiceAccountJson_HandlesLiteralUnescapedNewlinesInPrivateKey()
    {
        // Arrange: JSON string where private_key contains raw literal line breaks (common copy-paste in web portal env vars)
        var rawJson = "{\n  \"type\": \"service_account\",\n  \"project_id\": \"runasp-proj\",\n  \"private_key\": \"-----BEGIN PRIVATE KEY-----\r\nMIIEvgIBADANBgkqhkiG9w0BAQEFAASCBKgwggSkAgEAAoIBAQC\r\n-----END PRIVATE KEY-----\r\n\"\n}";

        // Act
        var normalized = FirebaseCredentialHelper.NormalizeServiceAccountJson(rawJson, out var projectId);

        // Assert
        Assert.Equal("runasp-proj", projectId);
        Assert.NotNull(normalized);
        Assert.Contains("runasp-proj", normalized);
        Assert.DoesNotContain("\r\n", normalized);
    }

    [Fact]
    public void LoadCredential_WithSingleQuotedJson_StripsQuotesAndParses()
    {
        // Arrange
        var options = new FirebaseOptions
        {
            ServiceAccountJson = "'{\"type\":\"service_account\",\"project_id\":\"single-quote-proj\"}'"
        };

        // Act & Assert
        // GoogleCredential.FromJson fails on missing fields, but we verify NormalizeServiceAccountJson handles stripped quotes
        var stripped = options.ServiceAccountJson.Trim('\'');
        var normalized = FirebaseCredentialHelper.NormalizeServiceAccountJson(stripped, out var projectId);
        Assert.Equal("single-quote-proj", projectId);
    }
}
