using System.Net;
using System.Net.Http.Headers;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Mizan.Core.Exceptions;
using Mizan.Infrastructure.Services.Email;
using SendGrid;
using SendGrid.Helpers.Mail;
using Xunit;

namespace Mizan.UnitTests.Services;

public class EmailServiceTests
{
    private class FakeSendGridClient : ISendGridClient
    {
        public HttpStatusCode StatusCodeToReturn { get; set; } = HttpStatusCode.Accepted;
        public string ResponseBody { get; set; } = "{\"message\":\"success\"}";
        public Exception? ExceptionToThrow { get; set; }
        public List<SendGridMessage> SentMessages { get; } = new();

        public Task<Response> SendEmailAsync(SendGridMessage msg, CancellationToken cancellationToken = default)
        {
            if (ExceptionToThrow != null)
            {
                throw ExceptionToThrow;
            }

            SentMessages.Add(msg);
            var response = new Response(
                StatusCodeToReturn,
                new StringContent(ResponseBody),
                new HttpResponseMessage(StatusCodeToReturn).Headers);

            return Task.FromResult(response);
        }

        public string UrlPath { get; set; } = string.Empty;
        public string Version { get; set; } = "v3";
        public string MediaType { get; set; } = "application/json";

        public AuthenticationHeaderValue AddAuthorization(KeyValuePair<string, string> header)
        {
            return new AuthenticationHeaderValue(header.Key, header.Value);
        }

        public Task<Response> MakeRequest(HttpRequestMessage request, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task<Response> RequestAsync(
            SendGridClient.Method method,
            string? urlPath = null,
            string? requestBody = null,
            string? queryParams = null,
            CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }
    }

    private static (EmailService Service, FakeSendGridClient FakeClient) CreateService(
        bool useMockInDevelopment = false,
        string apiKey = "SG.fake-test-key",
        string senderEmail = "no-reply@mizanapp.com",
        string senderName = "Mizan")
    {
        var options = Options.Create(new EmailOptions
        {
            ApiKey = apiKey,
            SenderEmail = senderEmail,
            SenderName = senderName,
            UseMockInDevelopment = useMockInDevelopment
        });

        var fakeClient = new FakeSendGridClient();
        var logger = NullLogger<EmailService>.Instance;
        var service = new EmailService(options, logger, fakeClient);

        return (service, fakeClient);
    }

    [Fact]
    public async Task SendOtpEmailAsync_WhenMockModeEnabled_ReturnsTrueWithoutCallingSendGrid()
    {
        var (service, fakeClient) = CreateService(useMockInDevelopment: true);

        var result = await service.SendOtpEmailAsync("test@example.com", "123456");

        Assert.True(result);
        Assert.Empty(fakeClient.SentMessages);
    }

    [Fact]
    public async Task SendInstallmentReminderEmailAsync_WhenMockModeEnabled_ReturnsTrueWithoutCallingSendGrid()
    {
        var (service, fakeClient) = CreateService(useMockInDevelopment: true);

        var result = await service.SendInstallmentReminderEmailAsync(
            "test@example.com",
            "أحمد",
            "محمد",
            500m,
            DateTime.UtcNow.AddDays(1),
            1);

        Assert.True(result);
        Assert.Empty(fakeClient.SentMessages);
    }

    [Fact]
    public async Task SendOtpEmailAsync_WhenSendGridReturns2xx_ReturnsTrue()
    {
        var (service, fakeClient) = CreateService(useMockInDevelopment: false);
        fakeClient.StatusCodeToReturn = HttpStatusCode.Accepted;

        var result = await service.SendOtpEmailAsync("user@example.com", "654321");

        Assert.True(result);
        Assert.Single(fakeClient.SentMessages);
    }

    [Fact]
    public async Task SendOtpEmailAsync_WhenSendGridReturnsNon2xx_ReturnsFalseWithoutThrowing()
    {
        var (service, fakeClient) = CreateService(useMockInDevelopment: false);
        fakeClient.StatusCodeToReturn = HttpStatusCode.Unauthorized;
        fakeClient.ResponseBody = "{\"errors\":[{\"message\":\"The provided authorization grant is invalid, expired, or revoked\"}]}";

        var result = await service.SendOtpEmailAsync("user@example.com", "654321");

        Assert.False(result);
        Assert.Single(fakeClient.SentMessages);
    }

    [Fact]
    public async Task SendOtpEmailAsync_WhenNetworkExceptionOccurs_ReturnsFalseWithoutThrowing()
    {
        var (service, fakeClient) = CreateService(useMockInDevelopment: false);
        fakeClient.ExceptionToThrow = new HttpRequestException("DNS resolution failed");

        var result = await service.SendOtpEmailAsync("user@example.com", "654321");

        Assert.False(result);
    }

    [Fact]
    public async Task SendInstallmentReminderEmailAsync_WhenSendGridReturns2xx_ReturnsTrue()
    {
        var (service, fakeClient) = CreateService(useMockInDevelopment: false);
        fakeClient.StatusCodeToReturn = HttpStatusCode.OK;

        var result = await service.SendInstallmentReminderEmailAsync(
            "user@example.com",
            "علي",
            "خالد",
            1200m,
            DateTime.UtcNow.AddDays(2),
            2);

        Assert.True(result);
        Assert.Single(fakeClient.SentMessages);
    }

    [Fact]
    public async Task SendInstallmentReminderEmailAsync_WhenSendGridReturnsNon2xx_ReturnsFalseWithoutThrowing()
    {
        var (service, fakeClient) = CreateService(useMockInDevelopment: false);
        fakeClient.StatusCodeToReturn = HttpStatusCode.InternalServerError;
        fakeClient.ResponseBody = "{\"errors\":[{\"message\":\"SendGrid service error\"}]}";

        var result = await service.SendInstallmentReminderEmailAsync(
            "user@example.com",
            "علي",
            "خالد",
            1200m,
            DateTime.UtcNow.AddDays(2),
            2);

        Assert.False(result);
        Assert.Single(fakeClient.SentMessages);
    }

    [Fact]
    public async Task SendInstallmentReminderEmailAsync_WhenNetworkExceptionOccurs_ReturnsFalseWithoutThrowing()
    {
        var (service, fakeClient) = CreateService(useMockInDevelopment: false);
        fakeClient.ExceptionToThrow = new TaskCanceledException("Connection timeout");

        var result = await service.SendInstallmentReminderEmailAsync(
            "user@example.com",
            "علي",
            "خالد",
            1200m,
            DateTime.UtcNow,
            0);

        Assert.False(result);
    }

    [Fact]
    public async Task SendOtpEmailAsync_WhenEmailOrOtpIsEmpty_ThrowsBadRequestException()
    {
        var (service, _) = CreateService();

        await Assert.ThrowsAsync<BadRequestException>(() => service.SendOtpEmailAsync("", "123456"));
        await Assert.ThrowsAsync<BadRequestException>(() => service.SendOtpEmailAsync("user@example.com", ""));
    }

    [Fact]
    public async Task SendInstallmentReminderEmailAsync_WhenEmailIsEmpty_ReturnsFalse()
    {
        var (service, fakeClient) = CreateService();

        var result = await service.SendInstallmentReminderEmailAsync(
            "",
            "علي",
            "خالد",
            1200m,
            DateTime.UtcNow,
            0);

        Assert.False(result);
        Assert.Empty(fakeClient.SentMessages);
    }
}
