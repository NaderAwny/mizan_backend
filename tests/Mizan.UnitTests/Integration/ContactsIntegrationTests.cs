using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Mizan.Application.DTOs.Auth;
using Mizan.Application.DTOs.Contacts;
using Xunit;

namespace Mizan.UnitTests.Integration;

/// <summary>
/// Standalone factory for ContactsIntegrationTests — owns its own FakeEmailService
/// instance so it is completely isolated from AuthIntegrationTests.
/// </summary>
public class ContactsWebApplicationFactory : Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program>
{
    private readonly string _dbName = Guid.NewGuid().ToString();
    public readonly CustomWebApplicationFactory.FakeEmailService EmailSvc = new();

    protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
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


public class ContactsIntegrationTests : IClassFixture<ContactsWebApplicationFactory>
{
    private readonly ContactsWebApplicationFactory _factory;
    private readonly JsonSerializerOptions _jsonOptions = new() { PropertyNameCaseInsensitive = true };

    public ContactsIntegrationTests(ContactsWebApplicationFactory factory)
    {
        _factory = factory;
    }

    // ── 401 Unauthorized — unauthenticated requests ───────────────────────────

    [Fact]
    public async Task PostContact_WithoutToken_ShouldReturn401()
    {
        using var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/contacts", new { name = "Test" });
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetContacts_WithoutToken_ShouldReturn401()
    {
        using var client = _factory.CreateClient();
        var response = await client.GetAsync("/api/contacts");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetContactById_WithoutToken_ShouldReturn401()
    {
        using var client = _factory.CreateClient();
        var response = await client.GetAsync($"/api/contacts/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task PutContact_WithoutToken_ShouldReturn401()
    {
        using var client = _factory.CreateClient();
        var response = await client.PutAsJsonAsync($"/api/contacts/{Guid.NewGuid()}", new { name = "Test" });
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task DeleteContact_WithoutToken_ShouldReturn401()
    {
        using var client = _factory.CreateClient();
        var response = await client.DeleteAsync($"/api/contacts/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // ── Full flow with authenticated user ─────────────────────────────────────

    [Fact]
    public async Task ContactCrud_WithAuthenticatedUser_ShouldWorkEndToEnd()
    {
        var client = _factory.CreateClient();
        var token = await AuthenticateAsync(client, "contacts.test@mizan.app");

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // 1. Create
        var createRequest = new CreateContactRequest
        {
            Name = "Ahmed Test",
            PhoneNumber = "01012345678",
            Notes = "Test notes"
        };

        var createResponse = await client.PostAsJsonAsync("/api/contacts", createRequest);
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

        var createJson = await createResponse.Content.ReadFromJsonAsync<JsonElement>(_jsonOptions);
        Assert.True(createJson.GetProperty("success").GetBoolean());
        var contactId = createJson.GetProperty("data").GetProperty("id").GetString();
        Assert.NotNull(contactId);

        // 2. Get by id
        var getResponse = await client.GetAsync($"/api/contacts/{contactId}");
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);

        var getJson = await getResponse.Content.ReadFromJsonAsync<JsonElement>(_jsonOptions);
        Assert.Equal("Ahmed Test", getJson.GetProperty("data").GetProperty("name").GetString());

        // 3. List contacts
        var listResponse = await client.GetAsync("/api/contacts?page=1&pageSize=20");
        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);

        var listJson = await listResponse.Content.ReadFromJsonAsync<JsonElement>(_jsonOptions);
        Assert.True(listJson.GetProperty("data").GetProperty("totalCount").GetInt32() >= 1);

        // 4. Update
        var updateRequest = new UpdateContactRequest { Name = "Ahmed Updated", PhoneNumber = null, Notes = null };
        var updateResponse = await client.PutAsJsonAsync($"/api/contacts/{contactId}", updateRequest);
        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);

        var updateJson = await updateResponse.Content.ReadFromJsonAsync<JsonElement>(_jsonOptions);
        Assert.Equal("Ahmed Updated", updateJson.GetProperty("data").GetProperty("name").GetString());

        // 5. Soft delete
        var deleteResponse = await client.DeleteAsync($"/api/contacts/{contactId}");
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);
    }

    [Fact]
    public async Task GetContactById_ForAnotherUsersContact_ShouldReturn404()
    {
        // User A creates a contact
        var clientA = _factory.CreateClient();
        var tokenA = await AuthenticateAsync(clientA, "user.a.contacts@mizan.app");
        clientA.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokenA);

        var createResponse = await clientA.PostAsJsonAsync("/api/contacts",
            new CreateContactRequest { Name = "User A Contact" });
        var createJson = await createResponse.Content.ReadFromJsonAsync<JsonElement>(_jsonOptions);
        var contactId = createJson.GetProperty("data").GetProperty("id").GetString();

        // User B tries to access it
        var clientB = _factory.CreateClient();
        var tokenB = await AuthenticateAsync(clientB, "user.b.contacts@mizan.app");
        clientB.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokenB);

        var getResponse = await clientB.GetAsync($"/api/contacts/{contactId}");
        Assert.Equal(HttpStatusCode.NotFound, getResponse.StatusCode);
    }

    // ── Helper: register + verify OTP → get token ────────────────────────────

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
