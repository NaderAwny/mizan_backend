using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Mizan.Application.DTOs.Auth;
using Mizan.Application.DTOs.Contacts;
using Mizan.Application.DTOs.Transactions;
using Mizan.Core.Enums;
using Xunit;

namespace Mizan.UnitTests.Integration;

public class TransactionsWebApplicationFactory : Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program>
{
    private readonly string _dbName = Guid.NewGuid().ToString();
    public readonly CustomWebApplicationFactory.FakeEmailService EmailSvc = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureServices(services =>
        {
            services.AddDbContext<Mizan.Infrastructure.Persistence.MizanDbContext>(options =>
            {
                options.UseInMemoryDatabase(_dbName);
            });

            var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(Mizan.Application.Interfaces.IEmailService));
            if (descriptor != null) services.Remove(descriptor);

            services.AddSingleton<Mizan.Application.Interfaces.IEmailService>(EmailSvc);
        });
    }
}

public class TransactionsIntegrationTests : IClassFixture<TransactionsWebApplicationFactory>
{
    private readonly TransactionsWebApplicationFactory _factory;
    private readonly JsonSerializerOptions _jsonOptions = new() { PropertyNameCaseInsensitive = true };

    public TransactionsIntegrationTests(TransactionsWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GetTransactions_WithoutToken_ShouldReturn401()
    {
        using var client = _factory.CreateClient();
        var response = await client.GetAsync("/api/transactions");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task TransactionFlow_WithInstallmentsAndPayment_ShouldWorkEndToEnd()
    {
        var client = _factory.CreateClient();
        var token = await AuthenticateAsync(client, "tx.owner@mizan.app");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // 1. Create a contact first
        var contactResp = await client.PostAsJsonAsync("/api/contacts", new CreateContactRequest { Name = "Customer One" });
        var contactJson = await contactResp.Content.ReadFromJsonAsync<JsonElement>(_jsonOptions);
        var contactId = contactJson.GetProperty("data").GetProperty("id").GetInt32();

        // 2. Create automatic installment transaction
        var createTxReq = new CreateTransactionRequest
        {
            ContactId = contactId,
            Type = TransactionType.Sale,
            Amount = 1000m,
            TransactionDate = DateTime.UtcNow,
            NoteType = NoteType.Text,
            NoteText = "Sale on installments",
            IsInstallment = true,
            InstallmentPlanMode = InstallmentPlanMode.Automatic,
            InstallmentCount = 2,
            FirstInstallmentDate = DateTime.UtcNow.AddDays(7),
            Frequency = InstallmentFrequency.Weekly
        };

        var txResp = await client.PostAsJsonAsync("/api/transactions", createTxReq);
        Assert.Equal(HttpStatusCode.Created, txResp.StatusCode);

        var txJson = await txResp.Content.ReadFromJsonAsync<JsonElement>(_jsonOptions);
        var txData = txJson.GetProperty("data");
        var txId = txData.GetProperty("id").GetInt32();
        Assert.Equal(1000m, txData.GetProperty("amount").GetDecimal());
        Assert.Equal(0m, txData.GetProperty("totalPaid").GetDecimal());
        Assert.Equal(1000m, txData.GetProperty("totalRemaining").GetDecimal());

        var installments = txData.GetProperty("installments").EnumerateArray().ToList();
        Assert.Equal(2, installments.Count);
        var firstInstId = installments[0].GetProperty("id").GetInt32();

        // 3. Mark 1st installment paid
        var payResp = await client.PostAsync($"/api/transactions/{txId}/installments/{firstInstId}/pay", null);
        Assert.Equal(HttpStatusCode.OK, payResp.StatusCode);

        var payJson = await payResp.Content.ReadFromJsonAsync<JsonElement>(_jsonOptions);
        var updatedData = payJson.GetProperty("data");
        Assert.Equal(500m, updatedData.GetProperty("totalPaid").GetDecimal());
        Assert.Equal(500m, updatedData.GetProperty("totalRemaining").GetDecimal());

        // 4. Soft delete transaction
        var deleteResp = await client.DeleteAsync($"/api/transactions/{txId}");
        Assert.Equal(HttpStatusCode.NoContent, deleteResp.StatusCode);
    }

    [Fact]
    public async Task UploadVoiceNote_WithDisallowedContentType_ShouldReturn400()
    {
        var client = _factory.CreateClient();
        var token = await AuthenticateAsync(client, "voice.uploader@mizan.app");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Create contact & transaction
        var contactResp = await client.PostAsJsonAsync("/api/contacts", new CreateContactRequest { Name = "Customer Voice" });
        var contactJson = await contactResp.Content.ReadFromJsonAsync<JsonElement>(_jsonOptions);
        var contactId = contactJson.GetProperty("data").GetProperty("id").GetInt32();

        var txResp = await client.PostAsJsonAsync("/api/transactions", new CreateTransactionRequest
        {
            ContactId = contactId,
            Type = TransactionType.Purchase,
            Amount = 250m,
            TransactionDate = DateTime.UtcNow
        });
        var txJson = await txResp.Content.ReadFromJsonAsync<JsonElement>(_jsonOptions);
        var txId = txJson.GetProperty("data").GetProperty("id").GetInt32();

        // Upload invalid content-type file (text/plain)
        using var content = new MultipartFormDataContent();
        var byteArray = System.Text.Encoding.UTF8.GetBytes("This is text, not audio");
        var byteContent = new ByteArrayContent(byteArray);
        byteContent.Headers.ContentType = new MediaTypeHeaderValue("text/plain");
        content.Add(byteContent, "file", "test.txt");

        var uploadResp = await client.PostAsync($"/api/transactions/{txId}/voice-note", content);
        Assert.Equal(HttpStatusCode.BadRequest, uploadResp.StatusCode);
    }

    [Fact]
    public async Task GetVoiceNote_ForAnotherUsersTransaction_ShouldReturn404()
    {
        // User A creates transaction
        var clientA = _factory.CreateClient();
        var tokenA = await AuthenticateAsync(clientA, "user.a.tx@mizan.app");
        clientA.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokenA);

        var contactResp = await clientA.PostAsJsonAsync("/api/contacts", new CreateContactRequest { Name = "User A Contact" });
        var contactJson = await contactResp.Content.ReadFromJsonAsync<JsonElement>(_jsonOptions);
        var contactId = contactJson.GetProperty("data").GetProperty("id").GetInt32();

        var txResp = await clientA.PostAsJsonAsync("/api/transactions", new CreateTransactionRequest
        {
            ContactId = contactId,
            Type = TransactionType.Sale,
            Amount = 100m,
            TransactionDate = DateTime.UtcNow
        });
        var txJson = await txResp.Content.ReadFromJsonAsync<JsonElement>(_jsonOptions);
        var txId = txJson.GetProperty("data").GetProperty("id").GetInt32();

        // User B tries to access voice note endpoint for User A's transaction
        var clientB = _factory.CreateClient();
        var tokenB = await AuthenticateAsync(clientB, "user.b.tx@mizan.app");
        clientB.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokenB);

        var voiceResp = await clientB.GetAsync($"/api/transactions/{txId}/voice-note");
        Assert.Equal(HttpStatusCode.NotFound, voiceResp.StatusCode);
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
