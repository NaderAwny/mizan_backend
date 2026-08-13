using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Mizan.Application.DTOs.Auth;
using Xunit;

namespace Mizan.UnitTests.Integration;

public class AuthIntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly JsonSerializerOptions _jsonOptions = new() { PropertyNameCaseInsensitive = true };

    public AuthIntegrationTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Full_Auth_Flow_Should_Work_Successfully()
    {
        // 1. Register new user with email
        var registerRequest = new RegisterRequest
        {
            FirstName = "محمد",
            LastName = "أحمد",
            Email = "test.user@mizan.app"
        };

        var regResponse = await _client.PostAsJsonAsync("/api/auth/register", registerRequest);
        Assert.Equal(HttpStatusCode.OK, regResponse.StatusCode);

        var regContent = await regResponse.Content.ReadFromJsonAsync<JsonElement>(_jsonOptions);
        Assert.True(regContent.GetProperty("success").GetBoolean());
        
        var otpData = regContent.GetProperty("data");
        Assert.True(otpData.GetProperty("otpSent").GetBoolean());
        var devCode = otpData.GetProperty("devCode").GetString();
        Assert.NotNull(devCode);

        // 2. Verify OTP
        var verifyRequest = new VerifyOtpRequest
        {
            Email = "test.user@mizan.app",
            Code = devCode
        };

        var verifyResponse = await _client.PostAsJsonAsync("/api/auth/verify-otp", verifyRequest);
        Assert.Equal(HttpStatusCode.OK, verifyResponse.StatusCode);

        var verifyContent = await verifyResponse.Content.ReadFromJsonAsync<JsonElement>(_jsonOptions);
        Assert.True(verifyContent.GetProperty("success").GetBoolean());

        var authData = verifyContent.GetProperty("data");
        var token = authData.GetProperty("token").GetString();
        var refreshToken = authData.GetProperty("refreshToken").GetString();
        Assert.False(string.IsNullOrEmpty(token));
        Assert.False(string.IsNullOrEmpty(refreshToken));

        // 3. Select user type (as shop_owner) with Bearer token
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var selectTypeRequest = new SelectUserTypeRequest
        {
            UserType = "shop_owner",
            ShopName = "محل الأمل للإلكترونيات",
            Address = "شارع الجمهورية - القاهرة"
        };

        var selectTypeResponse = await _client.PostAsJsonAsync("/api/auth/select-user-type", selectTypeRequest);
        Assert.Equal(HttpStatusCode.OK, selectTypeResponse.StatusCode);

        var selectTypeContent = await selectTypeResponse.Content.ReadFromJsonAsync<JsonElement>(_jsonOptions);
        Assert.True(selectTypeContent.GetProperty("success").GetBoolean());
        Assert.Equal("shop_owner", selectTypeContent.GetProperty("data").GetProperty("userType").GetString());
        Assert.Equal("محل الأمل للإلكترونيات", selectTypeContent.GetProperty("data").GetProperty("shopName").GetString());

        // 4. Get Current User profile (GET /api/users/me)
        var meResponse = await _client.GetAsync("/api/users/me");
        Assert.Equal(HttpStatusCode.OK, meResponse.StatusCode);

        var meContent = await meResponse.Content.ReadFromJsonAsync<JsonElement>(_jsonOptions);
        Assert.True(meContent.GetProperty("success").GetBoolean());
        
        var profileData = meContent.GetProperty("data");
        Assert.Equal("محمد", profileData.GetProperty("firstName").GetString());
        Assert.Equal("أحمد", profileData.GetProperty("lastName").GetString());
        Assert.Equal("test.user@mizan.app", profileData.GetProperty("email").GetString());
        Assert.Equal("shop_owner", profileData.GetProperty("userType").GetString());
        Assert.Equal("محل الأمل للإلكترونيات", profileData.GetProperty("shop").GetProperty("shopName").GetString());
    }

    [Fact]
    public async Task VerifyOtp_WithWrongCode_ShouldReturnUnifiedError400()
    {
        // 1. Register
        var registerRequest = new RegisterRequest
        {
            FirstName = "محمود",
            LastName = "علي",
            Email = "wrong.otp@mizan.app"
        };
        await _client.PostAsJsonAsync("/api/auth/register", registerRequest);

        // 2. Verify with wrong OTP
        var verifyRequest = new VerifyOtpRequest
        {
            Email = "wrong.otp@mizan.app",
            Code = "000000"
        };

        var verifyResponse = await _client.PostAsJsonAsync("/api/auth/verify-otp", verifyRequest);
        Assert.Equal(HttpStatusCode.BadRequest, verifyResponse.StatusCode);

        var errorContent = await verifyResponse.Content.ReadFromJsonAsync<JsonElement>(_jsonOptions);
        Assert.Equal(400, errorContent.GetProperty("statusCode").GetInt32());
        Assert.Contains("كود التحقق غير صحيح", errorContent.GetProperty("message").GetString());
    }

    [Fact]
    public async Task GetCurrentUser_WithoutToken_ShouldReturnUnauthorized()
    {
        using var client = new CustomWebApplicationFactory().CreateClient();
        var response = await client.GetAsync("/api/users/me");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task SendOtp_WithEmail_ShouldReturnOk()
    {
        var sendOtpRequest = new SendOtpRequest
        {
            Email = "sendotp@mizan.app"
        };

        var response = await _client.PostAsJsonAsync("/api/auth/send-otp", sendOtpRequest);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var content = await response.Content.ReadFromJsonAsync<JsonElement>(_jsonOptions);
        Assert.True(content.GetProperty("success").GetBoolean());
        Assert.True(content.GetProperty("data").GetProperty("otpSent").GetBoolean());
    }

    [Fact]
    public async Task EmailOtp_FullFlow_ShouldSendOtpAndLoginSuccessfully()
    {
        // 1. Send OTP to email
        var sendOtpRequest = new SendOtpRequest
        {
            Email = "user@mizan.app"
        };

        var sendResponse = await _client.PostAsJsonAsync("/api/auth/send-otp", sendOtpRequest);
        Assert.Equal(HttpStatusCode.OK, sendResponse.StatusCode);

        var sendContent = await sendResponse.Content.ReadFromJsonAsync<JsonElement>(_jsonOptions);
        Assert.True(sendContent.GetProperty("success").GetBoolean());
        var devCode = sendContent.GetProperty("data").GetProperty("devCode").GetString();
        Assert.NotNull(devCode);

        // 2. Verify OTP with email
        var verifyRequest = new VerifyOtpRequest
        {
            Email = "user@mizan.app",
            Code = devCode
        };

        var verifyResponse = await _client.PostAsJsonAsync("/api/auth/verify-otp", verifyRequest);
        Assert.Equal(HttpStatusCode.OK, verifyResponse.StatusCode);

        var verifyContent = await verifyResponse.Content.ReadFromJsonAsync<JsonElement>(_jsonOptions);
        Assert.True(verifyContent.GetProperty("success").GetBoolean());
        var token = verifyContent.GetProperty("data").GetProperty("token").GetString();
        Assert.False(string.IsNullOrEmpty(token));
    }
}
