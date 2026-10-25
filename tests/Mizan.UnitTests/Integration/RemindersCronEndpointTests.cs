using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace Mizan.UnitTests.Integration;

public class RemindersCronEndpointTests : IClassFixture<CustomWebApplicationFactory>
{
    private const string Endpoint = "/api/notifications/run-reminders-scan-cron";
    private const string CronKeyHeader = "X-Cron-Key";

    private readonly CustomWebApplicationFactory _factory;
    private readonly JsonSerializerOptions _jsonOptions = new() { PropertyNameCaseInsensitive = true };

    public RemindersCronEndpointTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private HttpClient CreateClientWithCronKey(string? cronApiKey)
    {
        var factory = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, config) =>
            {
                if (cronApiKey != null)
                    config.AddInMemoryCollection(new Dictionary<string, string?> { ["CronApiKey"] = cronApiKey });
            });
        });
        return factory.CreateClient();
    }

    [Fact]
    public async Task CronScan_WithoutKeyHeader_ShouldReturn401()
    {
        var client = CreateClientWithCronKey("super-secret-cron-key");

        var response = await client.PostAsync(Endpoint, null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task CronScan_WithWrongKey_ShouldReturn401()
    {
        var client = CreateClientWithCronKey("super-secret-cron-key");

        var request = new HttpRequestMessage(HttpMethod.Post, Endpoint);
        request.Headers.Add(CronKeyHeader, "wrong-key");

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task CronScan_WithKeyNotConfigured_ShouldReturn401_EvenWithMatchingHeader()
    {
        var client = CreateClientWithCronKey(null);

        var request = new HttpRequestMessage(HttpMethod.Post, Endpoint);
        request.Headers.Add(CronKeyHeader, "anything");

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task CronScan_WithValidKey_ShouldReturn200AndExpectedShape_WithoutJwt()
    {
        var client = CreateClientWithCronKey("super-secret-cron-key");

        var request = new HttpRequestMessage(HttpMethod.Post, Endpoint);
        request.Headers.Add(CronKeyHeader, "super-secret-cron-key");

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var content = await response.Content.ReadFromJsonAsync<JsonElement>(_jsonOptions);
        Assert.True(content.GetProperty("success").GetBoolean());
        Assert.Equal(JsonValueKind.Object, content.GetProperty("data").ValueKind);
        Assert.True(content.GetProperty("data").GetProperty("remindersProcessed").GetInt32() >= 0);
        Assert.True(content.GetProperty("data").GetProperty("timestamp").GetDateTime() <= DateTime.UtcNow.AddMinutes(1));
    }

    [Fact]
    public async Task CronScan_IsAnonymous_OtherNotificationsEndpointsStillRequireAuth()
    {
        var client = CreateClientWithCronKey("super-secret-cron-key");

        // The cron endpoint accepts an anonymous call (valid key)...
        var cronRequest = new HttpRequestMessage(HttpMethod.Post, Endpoint);
        cronRequest.Headers.Add(CronKeyHeader, "super-secret-cron-key");
        var cronResponse = await client.SendAsync(cronRequest);
        Assert.Equal(HttpStatusCode.OK, cronResponse.StatusCode);

        // ...but the owner-scoped manual scan still demands a JWT.
        var manualResponse = await client.PostAsync("/api/notifications/run-reminders-scan", null);
        Assert.Equal(HttpStatusCode.Unauthorized, manualResponse.StatusCode);
    }
}
