using System.Net;
using DnsClient;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Mizan.Infrastructure.Services.Email;
using Xunit;

namespace Mizan.UnitTests.Services;

public class DnsEmailVerificationServiceTests
{
    private readonly IMemoryCache _memoryCache;
    private readonly NullLogger<DnsEmailVerificationService> _logger;

    public DnsEmailVerificationServiceTests()
    {
        _memoryCache = new MemoryCache(new MemoryCacheOptions());
        _logger = NullLogger<DnsEmailVerificationService>.Instance;
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("invalid-email")]
    [InlineData("missing-domain@")]
    [InlineData("@missing-user.com")]
    [InlineData("user@domain-without-tld")]
    public async Task VerifyAsync_WithInvalidEmailFormat_ReturnsInvalidFormat(string email)
    {
        var options = Options.Create(new EmailVerificationOptions());
        var service = new DnsEmailVerificationService(options, _memoryCache, _logger);

        var result = await service.VerifyAsync(email);

        Assert.False(result.IsDeliverable);
        Assert.Equal("InvalidFormat", result.Reason);
    }

    [Theory]
    [InlineData("test@mailinator.com")]
    [InlineData("user@tempmail.com")]
    [InlineData("fake@10minutemail.com")]
    [InlineData("random@yopmail.com")]
    [InlineData("disposable@trashmail.com")]
    public async Task VerifyAsync_WithBuiltInDisposableDomain_ReturnsDisposableDomain(string email)
    {
        var options = Options.Create(new EmailVerificationOptions());
        var service = new DnsEmailVerificationService(options, _memoryCache, _logger);

        var result = await service.VerifyAsync(email);

        Assert.False(result.IsDeliverable);
        Assert.Equal("DisposableDomain", result.Reason);
    }

    [Fact]
    public async Task VerifyAsync_WithConfiguredBlockedDomain_ReturnsDisposableDomain()
    {
        var options = Options.Create(new EmailVerificationOptions
        {
            BlockedDomains = new List<string> { "custom-blocked.com", "spam-domain.org" }
        });
        var service = new DnsEmailVerificationService(options, _memoryCache, _logger);

        var result = await service.VerifyAsync("user@custom-blocked.com");

        Assert.False(result.IsDeliverable);
        Assert.Equal("DisposableDomain", result.Reason);
    }

    [Fact]
    public async Task VerifyAsync_WhenResultIsCached_ReturnsCachedImmediately()
    {
        var options = Options.Create(new EmailVerificationOptions { CacheMinutes = 10 });
        var service = new DnsEmailVerificationService(options, _memoryCache, _logger);

        // Pre-populate cache with a known verdict
        var email = "pre-cached@example.com";
        var cacheKey = "EmailVerification_" + email;
        var preCached = Mizan.Application.DTOs.Auth.EmailVerificationResult.Valid("CachedDirectly");
        _memoryCache.Set(cacheKey, preCached);

        var result = await service.VerifyAsync(email);

        Assert.True(result.IsDeliverable);
        Assert.Equal("CachedDirectly", result.Reason);
    }

    [Fact]
    public async Task VerifyAsync_WhenDnsTimesOut_WithFailClosedTrue_ReturnsDnsTimeout()
    {
        // Use TEST-NET-1 IP (192.0.2.1) which is reserved and will not respond, forcing a timeout
        var unreachableServer = new IPEndPoint(IPAddress.Parse("192.0.2.1"), 53);
        var lookupOptions = new LookupClientOptions(unreachableServer)
        {
            Timeout = TimeSpan.FromMilliseconds(50),
            Retries = 0
        };
        var timeoutLookupClient = new LookupClient(lookupOptions);

        var options = Options.Create(new EmailVerificationOptions
        {
            TimeoutSeconds = 1,
            FailClosed = true
        });

        var service = new DnsEmailVerificationService(options, _memoryCache, _logger, timeoutLookupClient);

        var result = await service.VerifyAsync("user@some-domain-for-timeout.com");

        Assert.False(result.IsDeliverable);
        Assert.Contains(result.Reason, new[] { "DnsTimeout", "DnsLookupFailed" });
    }

    [Fact]
    public async Task VerifyAsync_WhenDnsTimesOut_WithFailClosedFalse_ReturnsFallbackValid()
    {
        // Use TEST-NET-1 IP (192.0.2.1) to force timeout with FailClosed = false
        var unreachableServer = new IPEndPoint(IPAddress.Parse("192.0.2.1"), 53);
        var lookupOptions = new LookupClientOptions(unreachableServer)
        {
            Timeout = TimeSpan.FromMilliseconds(50),
            Retries = 0
        };
        var timeoutLookupClient = new LookupClient(lookupOptions);

        var options = Options.Create(new EmailVerificationOptions
        {
            TimeoutSeconds = 1,
            FailClosed = false
        });

        var service = new DnsEmailVerificationService(options, _memoryCache, _logger, timeoutLookupClient);

        var result = await service.VerifyAsync("user@some-domain-for-fallback.com");

        Assert.True(result.IsDeliverable);
        Assert.Contains(result.Reason, new[] { "TimeoutFallback", "ErrorFallback" });
    }
}
