using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Mizan.Application.DTOs.Auth;
using Mizan.Application.Interfaces;
using Xunit;

namespace Mizan.UnitTests.Integration;

public class AuthIntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;
    private readonly JsonSerializerOptions _jsonOptions = new() { PropertyNameCaseInsensitive = true };

    public AuthIntegrationTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public void OtpResponse_MustNeverExposeCodeOrDevCodeField_ReflectionCheck()
    {
        var type = typeof(OtpResponse);
        var properties = type.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
        var fields = type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);

        Assert.DoesNotContain(properties, p => p.Name.Equals("DevCode", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(properties, p => p.Name.Equals("Code", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(properties, p => p.Name.Equals("OtpCode", StringComparison.OrdinalIgnoreCase));

        Assert.DoesNotContain(fields, f => f.Name.Equals("DevCode", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(fields, f => f.Name.Equals("Code", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(fields, f => f.Name.Equals("OtpCode", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Full_Auth_Flow_With_Email_Should_Work_Successfully()
    {
        var testEmail = "test.integration@mizan.app";

        // 1. Register new user with email
        var registerRequest = new RegisterRequest
        {
            FirstName = "محمد",
            LastName = "أحمد",
            Email = testEmail
        };

        var regResponse = await _client.PostAsJsonAsync("/api/auth/register", registerRequest);
        Assert.Equal(HttpStatusCode.OK, regResponse.StatusCode);

        var regContent = await regResponse.Content.ReadFromJsonAsync<JsonElement>(_jsonOptions);
        Assert.True(regContent.GetProperty("success").GetBoolean());
        
        var otpData = regContent.GetProperty("data");
        Assert.True(otpData.GetProperty("otpSent").GetBoolean());
        
        // Assert that response JSON has no devCode or code property
        Assert.False(otpData.TryGetProperty("devCode", out _));
        Assert.False(otpData.TryGetProperty("code", out _));

        // Get captured OTP from fake email service instance
        var sentCode = _factory.EmailService.LastCapturedOtp;
        Assert.NotNull(sentCode);
        Assert.Equal(6, sentCode.Length);

        // 2. Verify OTP
        var verifyRequest = new VerifyOtpRequest
        {
            Email = testEmail,
            Code = sentCode
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
        Assert.Equal(testEmail, profileData.GetProperty("email").GetString());
        Assert.Equal("shop_owner", profileData.GetProperty("userType").GetString());
        Assert.Equal("محل الأمل للإلكترونيات", profileData.GetProperty("shop").GetProperty("shopName").GetString());

        // 5. Logout
        var logoutRequest = new LogoutRequest { RefreshToken = refreshToken };
        var logoutResponse = await _client.PostAsJsonAsync("/api/auth/logout", logoutRequest);
        Assert.Equal(HttpStatusCode.OK, logoutResponse.StatusCode);
    }

    [Fact]
    public async Task VerifyOtp_WithWrongCode_ShouldReturnUnifiedGenericError400()
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
        Assert.Contains("Invalid or expired verification code", errorContent.GetProperty("message").GetString());
    }

    [Fact]
    public async Task GetCurrentUser_WithoutToken_ShouldReturnUnauthorized()
    {
        using var client = _factory.CreateClient();
        var response = await client.GetAsync("/api/users/me");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task SendOtp_WithEmail_ShouldReturnOk()
    {
        var email = "sendotp@mizan.app";

        // Must register first so user exists in database
        var regResponse = await _client.PostAsJsonAsync("/api/auth/register", new RegisterRequest
        {
            FirstName = "مستخدم",
            LastName = "مسجل",
            Email = email
        });
        Assert.Equal(HttpStatusCode.OK, regResponse.StatusCode);

        // Must verify OTP so account is ACTIVE/CONFIRMED!
        var regOtp = _factory.EmailService.LastCapturedOtp!;
        var verifyResp = await _client.PostAsJsonAsync("/api/auth/verify-otp", new VerifyOtpRequest
        {
            Email = email,
            Code = regOtp
        });
        Assert.Equal(HttpStatusCode.OK, verifyResp.StatusCode);

        var sendOtpRequest = new SendOtpRequest
        {
            Email = email
        };

        var response = await _client.PostAsJsonAsync("/api/auth/send-otp", sendOtpRequest);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var content = await response.Content.ReadFromJsonAsync<JsonElement>(_jsonOptions);
        Assert.True(content.GetProperty("success").GetBoolean());
        Assert.True(content.GetProperty("data").GetProperty("otpSent").GetBoolean());
    }

    [Fact]
    public async Task SendOtp_WithUnregisteredEmail_ShouldReturnNotFound404()
    {
        var sendOtpRequest = new SendOtpRequest
        {
            Email = "completely.unregistered.user@mizan.app"
        };

        var response = await _client.PostAsJsonAsync("/api/auth/send-otp", sendOtpRequest);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        var content = await response.Content.ReadFromJsonAsync<JsonElement>(_jsonOptions);
        Assert.Equal(404, content.GetProperty("statusCode").GetInt32());
        Assert.Contains("لا يوجد حساب مسجل بهذا البريد الإلكتروني", content.GetProperty("message").GetString());
    }

    [Fact]
    public async Task SendOtp_WithUnverifiedRegisteredUser_ShouldReturnNotFound404()
    {
        var email = "unverified.user@mizan.app";

        // Registered but never called verify-otp
        await _client.PostAsJsonAsync("/api/auth/register", new RegisterRequest
        {
            FirstName = "غير",
            LastName = "مفعل",
            Email = email
        });

        var sendOtpRequest = new SendOtpRequest
        {
            Email = email
        };

        var response = await _client.PostAsJsonAsync("/api/auth/send-otp", sendOtpRequest);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        var content = await response.Content.ReadFromJsonAsync<JsonElement>(_jsonOptions);
        Assert.Equal(404, content.GetProperty("statusCode").GetInt32());
        Assert.Contains("لا يوجد حساب مسجل بهذا البريد الإلكتروني", content.GetProperty("message").GetString());
    }

    [Fact]
    public async Task Register_WithDisposableDomain_ShouldReturn400()
    {
        // Simulate the service returning "DisposableDomain"
        _factory.EmailVerificationService.NextResult =
            EmailVerificationResult.Invalid("DisposableDomain");

        var registerRequest = new RegisterRequest
        {
            FirstName = "علي",
            LastName = "تجريبي",
            Email = "user@mailinator.com"
        };

        var response = await _client.PostAsJsonAsync("/api/auth/register", registerRequest);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var content = await response.Content.ReadFromJsonAsync<JsonElement>(_jsonOptions);
        Assert.Equal(400, content.GetProperty("statusCode").GetInt32());
        Assert.Contains("البريد الإلكتروني غير قادر على استقبال الرسائل", content.GetProperty("message").GetString());

        // Reset for subsequent tests
        _factory.EmailVerificationService.NextResult = EmailVerificationResult.Valid("TestVerified");
    }

    [Fact]
    public async Task Register_WithNonExistentDomain_ShouldReturnBadRequest400()
    {
        _factory.EmailVerificationService.NextResult =
            EmailVerificationResult.Invalid("DomainNotFound");

        var registerRequest = new RegisterRequest
        {
            FirstName = "اختبار",
            LastName = "وهمي",
            Email = "fake@non-existent-domain-999.com"
        };

        var response = await _client.PostAsJsonAsync("/api/auth/register", registerRequest);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var content = await response.Content.ReadFromJsonAsync<JsonElement>(_jsonOptions);
        Assert.Equal(400, content.GetProperty("statusCode").GetInt32());
        Assert.Contains("البريد الإلكتروني غير قادر على استقبال الرسائل", content.GetProperty("message").GetString());

        _factory.EmailVerificationService.NextResult = EmailVerificationResult.Valid("TestVerified");
    }

    [Fact]
    public async Task Register_WithNonExistentMailbox_ShouldReturn400()
    {
        // Simulate SMTP probe returning "MailboxNotFound" (definitive 5xx)
        _factory.EmailVerificationService.NextResult =
            EmailVerificationResult.Invalid("MailboxNotFound");

        var registerRequest = new RegisterRequest
        {
            FirstName = "أحمد",
            LastName = "وهمي",
            Email = "does.not.exist.xyz123@gmail.com"
        };

        var response = await _client.PostAsJsonAsync("/api/auth/register", registerRequest);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var content = await response.Content.ReadFromJsonAsync<JsonElement>(_jsonOptions);
        Assert.Equal(400, content.GetProperty("statusCode").GetInt32());
        Assert.Contains("البريد الإلكتروني غير قادر على استقبال الرسائل", content.GetProperty("message").GetString());

        // Reset for subsequent tests
        _factory.EmailVerificationService.NextResult = EmailVerificationResult.Valid("TestVerified");
    }

    [Fact]
    public async Task Register_WhenSmtpProbeTimesOut_ShouldAllowRegistration_FailOpen()
    {
        // Simulate an SMTP timeout (transient) — the service returns Valid with reason="SmtpTimeout"
        // The fail-open policy means IsDeliverable=true, so registration proceeds normally.
        _factory.EmailVerificationService.NextResult =
            EmailVerificationResult.Valid("SmtpTimeout");

        var registerRequest = new RegisterRequest
        {
            FirstName = "سارة",
            LastName = "اختبار",
            Email = "failopen.test@mizan.app"
        };

        var response = await _client.PostAsJsonAsync("/api/auth/register", registerRequest);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var content = await response.Content.ReadFromJsonAsync<JsonElement>(_jsonOptions);
        Assert.True(content.GetProperty("success").GetBoolean());
        Assert.True(content.GetProperty("data").GetProperty("otpSent").GetBoolean());

        // Reset for subsequent tests
        _factory.EmailVerificationService.NextResult = EmailVerificationResult.Valid("TestVerified");
    }

    [Fact]
    public async Task Register_ResendOtp_ShouldSucceedWithoutUpdatingProfile()
    {
        var email = "resend.test@mizan.app";

        // 1. Initial register and complete verification
        var reg1 = await _client.PostAsJsonAsync("/api/auth/register", new RegisterRequest
        {
            FirstName = "الاسم الأصلي",
            LastName = "اللقب الأصلي",
            Email = email
        });
        Assert.Equal(HttpStatusCode.OK, reg1.StatusCode);

        var initialOtp = _factory.EmailService.LastCapturedOtp!;
        var initVerify = await _client.PostAsJsonAsync("/api/auth/verify-otp", new VerifyOtpRequest
        {
            Email = email,
            Code = initialOtp
        });
        Assert.Equal(HttpStatusCode.OK, initVerify.StatusCode);

        // 2. Call register again with different names (resend OTP scenario on active account)
        var reg2 = await _client.PostAsJsonAsync("/api/auth/register", new RegisterRequest
        {
            FirstName = "اسم مختلف",
            LastName = "لقب مختلف",
            Email = email
        });
        Assert.Equal(HttpStatusCode.OK, reg2.StatusCode);

        // Verify OTP to inspect returned user profile
        var otp = _factory.EmailService.LastCapturedOtp;
        Assert.NotNull(otp);

        var verifyResponse = await _client.PostAsJsonAsync("/api/auth/verify-otp", new VerifyOtpRequest
        {
            Email = email,
            Code = otp
        });
        Assert.Equal(HttpStatusCode.OK, verifyResponse.StatusCode);

        var verifyContent = await verifyResponse.Content.ReadFromJsonAsync<JsonElement>(_jsonOptions);
        var data = verifyContent.GetProperty("data");

        // Profile must retain the original names
        Assert.Equal("الاسم الأصلي", data.GetProperty("firstName").GetString());
        Assert.Equal("اللقب الأصلي", data.GetProperty("lastName").GetString());
    }

    [Fact]
    public async Task GenerateOtp_WhenEmailServiceFails_ShouldThrowBadRequest()
    {
        var email = "email.fail.test@mizan.app";

        // Simulate email sending failure in FakeEmailService
        _factory.EmailService.ShouldSucceed = false;

        try
        {
            var registerRequest = new RegisterRequest
            {
                FirstName = "فشل",
                LastName = "إرسال",
                Email = email
            };

            var response = await _client.PostAsJsonAsync("/api/auth/register", registerRequest);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

            var content = await response.Content.ReadFromJsonAsync<JsonElement>(_jsonOptions);
            Assert.Equal(400, content.GetProperty("statusCode").GetInt32());
            Assert.Contains("فشل إرسال كود التحقق", content.GetProperty("message").GetString());
        }
        finally
        {
            _factory.EmailService.ShouldSucceed = true;
        }
    }
}

