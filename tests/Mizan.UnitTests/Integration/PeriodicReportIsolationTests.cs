using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Mizan.Application.DTOs.Auth;
using Mizan.Application.DTOs.Contacts;
using Mizan.Application.DTOs.Transactions;
using Mizan.Core.Enums;
using Xunit;
using Xunit.Abstractions;

namespace Mizan.UnitTests.Integration;

public class PeriodicReportIsolationTests : IClassFixture<TransactionsWebApplicationFactory>
{
    private readonly TransactionsWebApplicationFactory _factory;
    private readonly ITestOutputHelper _output;
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
    };

    public PeriodicReportIsolationTests(TransactionsWebApplicationFactory factory, ITestOutputHelper output)
    {
        _factory = factory;
        _output = output;
    }

    [Fact]
    public async Task TwoUsers_CreatingTwoTransactionsEach_ShouldNotTriggerAnyReport()
    {
        using var client = _factory.CreateClient();

        // 1. Register User A and create 2 transactions
        var tokenA = await AuthenticateAsync(client, $"user.a.{Guid.NewGuid()}@mizan.app");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokenA);

        var contactRespA = await client.PostAsJsonAsync("/api/contacts", new CreateContactRequest { Name = "Customer A" });
        var contactJsonA = await contactRespA.Content.ReadFromJsonAsync<JsonElement>(_jsonOptions);
        var contactIdA = Guid.Parse(contactJsonA.GetProperty("data").GetProperty("id").GetString()!);

        for (int i = 0; i < 2; i++)
        {
            await client.PostAsJsonAsync("/api/transactions", new CreateTransactionRequest
            {
                ContactId = contactIdA,
                Type = TransactionType.Sale,
                Amount = 100m,
                TransactionDate = DateTime.UtcNow,
                NoteType = NoteType.None,
                IsInstallment = false
            }, _jsonOptions);
        }

        // 2. Register User B and create 2 transactions
        var tokenB = await AuthenticateAsync(client, $"user.b.{Guid.NewGuid()}@mizan.app");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokenB);

        var contactRespB = await client.PostAsJsonAsync("/api/contacts", new CreateContactRequest { Name = "Customer B" });
        var contactJsonB = await contactRespB.Content.ReadFromJsonAsync<JsonElement>(_jsonOptions);
        var contactIdB = Guid.Parse(contactJsonB.GetProperty("data").GetProperty("id").GetString()!);

        for (int i = 0; i < 2; i++)
        {
            await client.PostAsJsonAsync("/api/transactions", new CreateTransactionRequest
            {
                ContactId = contactIdB,
                Type = TransactionType.Purchase,
                Amount = 200m,
                TransactionDate = DateTime.UtcNow,
                NoteType = NoteType.None,
                IsInstallment = false
            }, _jsonOptions);
        }

        // 3. User B: GET /api/reports
        var reportsRespB = await client.GetAsync("/api/reports");
        var reportsContentB = await reportsRespB.Content.ReadAsStringAsync();
        _output.WriteLine("=== USER B REPORTS RESPONSE ===");
        _output.WriteLine(reportsContentB);

        // 4. User B: GET /api/notifications
        var notifRespB = await client.GetAsync("/api/notifications");
        var notifContentB = await notifRespB.Content.ReadAsStringAsync();
        _output.WriteLine("=== USER B NOTIFICATIONS RESPONSE ===");
        _output.WriteLine(notifContentB);

        Assert.Equal(HttpStatusCode.OK, reportsRespB.StatusCode);
        Assert.Equal(HttpStatusCode.OK, notifRespB.StatusCode);

        var reportsJson = await reportsRespB.Content.ReadFromJsonAsync<JsonElement>(_jsonOptions);
        var notifJson = await notifRespB.Content.ReadFromJsonAsync<JsonElement>(_jsonOptions);

        Assert.Equal(0, reportsJson.GetProperty("data").GetProperty("totalCount").GetInt32());
        Assert.Equal(0, notifJson.GetProperty("data").GetProperty("totalCount").GetInt32());
    }

    private async Task<string> AuthenticateAsync(HttpClient client, string email)
    {
        var registerRequest = new RegisterRequest { FirstName = "Test", LastName = "User", Email = email };
        await client.PostAsJsonAsync("/api/auth/register", registerRequest);

        var otp = _factory.EmailSvc.LastCapturedOtp!;
        var verifyRequest = new VerifyOtpRequest { Email = email, Code = otp };
        var verifyResponse = await client.PostAsJsonAsync("/api/auth/verify-otp", verifyRequest);
        var verifyJson = await verifyResponse.Content.ReadFromJsonAsync<JsonElement>(_jsonOptions);
        return verifyJson.GetProperty("data").GetProperty("token").GetString()!;
    }
}
