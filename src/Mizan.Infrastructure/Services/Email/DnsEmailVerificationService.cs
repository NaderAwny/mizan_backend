using System.Net.Mail;
using DnsClient;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Mizan.Application.DTOs.Auth;
using Mizan.Application.Interfaces;

namespace Mizan.Infrastructure.Services.Email;

public class DnsEmailVerificationService : IEmailVerificationService
{
    private readonly EmailVerificationOptions _options;
    private readonly ILookupClient _lookupClient;
    private readonly IMemoryCache _cache;
    private readonly ILogger<DnsEmailVerificationService> _logger;
    private readonly HashSet<string> _disposableDomains;

    // Cache key prefix; scoped per email address
    private const string CacheKeyPrefix = "EmailVerification_";

    private static readonly string[] DefaultDisposableDomains =
    [
        "tempmail.com",
        "10minutemail.com",
        "guerrillamail.com",
        "guerrillamailblock.com",
        "mailinator.com",
        "throwawaymail.com",
        "yopmail.com",
        "trashmail.com",
        "trashmail.net",
        "sharklasers.com",
        "dispostable.com",
        "getairmail.com",
        "fakeinbox.com",
        "generator.email",
        "temp-mail.org",
        "maildrop.cc",
        "mohmal.com",
        "inboxkitten.com",
        "nada.ltd",
        "getnada.com",
        "burnermail.io",
        "crazymailing.com",
        "tmpmail.net",
        "tmpmail.org",
        "disposablemail.com",
        "mytemp.email"
    ];

    public DnsEmailVerificationService(
        IOptions<EmailVerificationOptions> options,
        IMemoryCache cache,
        ILogger<DnsEmailVerificationService> logger,
        ILookupClient? lookupClient = null)
    {
        _options = options?.Value ?? new EmailVerificationOptions();
        _cache = cache;
        _logger = logger;

        var clientOptions = new LookupClientOptions
        {
            Timeout = TimeSpan.FromSeconds(Math.Max(1, _options.TimeoutSeconds)),
            Retries = 1,
            UseCache = true
        };
        _lookupClient = lookupClient ?? new LookupClient(clientOptions);

        _disposableDomains = new HashSet<string>(DefaultDisposableDomains, StringComparer.OrdinalIgnoreCase);
        if (_options.BlockedDomains != null)
        {
            foreach (var domain in _options.BlockedDomains)
            {
                if (!string.IsNullOrWhiteSpace(domain))
                    _disposableDomains.Add(domain.Trim().ToLowerInvariant());
            }
        }
    }

    public async Task<EmailVerificationResult> VerifyAsync(string email, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(email))
            return EmailVerificationResult.Invalid("InvalidFormat");

        var normalizedEmail = email.Trim().ToLowerInvariant();

        // Cache hit — return immediately to avoid redundant DNS/SMTP probes
        var cacheKey = CacheKeyPrefix + normalizedEmail;
        if (_cache.TryGetValue(cacheKey, out EmailVerificationResult? cached) && cached != null)
        {
            _logger.LogDebug("📦 Email verification cache hit for {Email}: {Reason}", normalizedEmail, cached.Reason);
            return cached;
        }

        var result = await VerifyInternalAsync(normalizedEmail, cancellationToken);

        // Only cache definitive results (not transient failures like timeouts)
        if (ShouldCache(result))
        {
            var expiry = TimeSpan.FromMinutes(_options.CacheMinutes);
            _cache.Set(cacheKey, result, expiry);
            _logger.LogDebug("📥 Cached email verification result for {Email}: {Reason} (TTL={Expiry}min)", normalizedEmail, result.Reason, _options.CacheMinutes);
        }

        return result;
    }

    private async Task<EmailVerificationResult> VerifyInternalAsync(string email, CancellationToken cancellationToken)
    {
        string domain;
        try
        {
            var address = new MailAddress(email);
            domain = address.Host.ToLowerInvariant();
        }
        catch
        {
            return EmailVerificationResult.Invalid("InvalidFormat");
        }

        if (string.IsNullOrWhiteSpace(domain) || !domain.Contains('.'))
            return EmailVerificationResult.Invalid("InvalidFormat");

        // Layer A-1: Disposable domain blacklist
        if (_disposableDomains.Contains(domain))
        {
            _logger.LogWarning("⚠️ Email verification rejected disposable domain: {Domain} for email {Email}", domain, email);
            return EmailVerificationResult.Invalid("DisposableDomain");
        }

        // Layer A-2: MX record lookup
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(TimeSpan.FromSeconds(Math.Max(1, _options.TimeoutSeconds)));

        List<DnsClient.Protocol.MxRecord> mxRecords;
        try
        {
            var dnsResult = await _lookupClient.QueryAsync(domain, QueryType.MX, cancellationToken: cts.Token);

            mxRecords = dnsResult.Answers.MxRecords()
                .Where(r => r.Exchange != null && !string.IsNullOrWhiteSpace(r.Exchange.Value) && r.Exchange.Value.Trim() != ".")
                .ToList();

            if (mxRecords.Count == 0)
            {
                _logger.LogWarning("⚠️ No valid MX records found for domain: {Domain}", domain);
                return EmailVerificationResult.Invalid("DomainNotFound");
            }

            _logger.LogInformation("✅ Domain {Domain} has {Count} MX record(s). Proceeding.", domain, mxRecords.Count);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning("⚠️ DNS MX lookup timed out for domain: {Domain}. FailClosed={FailClosed}", domain, _options.FailClosed);
            return _options.FailClosed
                ? EmailVerificationResult.Invalid("DnsTimeout")
                : EmailVerificationResult.Valid("TimeoutFallback");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ DNS MX lookup failed unexpectedly for domain: {Domain}. FailClosed={FailClosed}", domain, _options.FailClosed);
            return _options.FailClosed
                ? EmailVerificationResult.Invalid("DnsLookupFailed")
                : EmailVerificationResult.Valid("ErrorFallback");
        }

        // Layer B: SMTP RCPT-TO probe (optional, gated by EnableSmtpProbe)
        if (!_options.EnableSmtpProbe)
            return EmailVerificationResult.Valid("MxVerified");

        return await SmtpProbeAsync(email, domain, mxRecords, cancellationToken);
    }

    private async Task<EmailVerificationResult> SmtpProbeAsync(
        string email,
        string domain,
        List<DnsClient.Protocol.MxRecord> mxRecords,
        CancellationToken cancellationToken)
    {
        // Try MX hosts in order of preference (lowest preference number = highest priority)
        var sortedMx = mxRecords.OrderBy(r => r.Preference).ToList();

        foreach (var mx in sortedMx)
        {
            var mxHost = mx.Exchange.Value.TrimEnd('.');
            if (string.IsNullOrWhiteSpace(mxHost)) continue;

            try
            {
                using var smtpCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                smtpCts.CancelAfter(TimeSpan.FromSeconds(Math.Max(1, _options.SmtpTimeoutSeconds)));

                var result = await MailKitSmtpProbeAsync(mxHost, email, _options.SmtpTimeoutSeconds, smtpCts.Token);

                if (result == "MailboxNotFound")
                {
                    _logger.LogWarning("⚠️ SMTP probe (MailKit) rejected mailbox: {Email} via {Host}", email, mxHost);
                    return EmailVerificationResult.Invalid("MailboxNotFound");
                }

                if (result == "SmtpVerified")
                {
                    _logger.LogInformation("✅ SMTP probe (MailKit) confirmed mailbox exists: {Email} via {Host}", email, mxHost);
                    return EmailVerificationResult.Valid("SmtpVerified");
                }

                // SmtpUnknown → ambiguous (4xx / greylisting), fail-open
                _logger.LogWarning("⚠️ SMTP probe (MailKit) ambiguous for {Email} via {Host}. Fail-open.", email, mxHost);
                return EmailVerificationResult.Valid("SmtpUnknown");
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                _logger.LogWarning("⚠️ SMTP probe (MailKit) timed out for MX host: {Host} (email: {Email}). Fail-open.", mxHost, email);
                return EmailVerificationResult.Valid("SmtpTimeout");
            }
            catch (Exception ex)
            {
                // Port 25 blocked, firewall, TLS mismatch, or any connection error → fail-open (try next MX)
                _logger.LogWarning(ex, "⚠️ SMTP probe (MailKit) could not connect to MX host: {Host} (email: {Email}). Trying next MX.", mxHost, email);
            }
        }

        // All MX hosts tried, none gave a definitive answer → fail-open
        _logger.LogWarning("⚠️ All MX hosts unreachable for domain: {Domain}. Fail-open.", domain);
        return EmailVerificationResult.Valid("SmtpAllHostsUnreachable");
    }

    /// <summary>
    /// Connects to the MX host on port 25 via MailKit, issues EHLO, MAIL FROM, and RCPT TO,
    /// returning a classification string: "SmtpVerified", "MailboxNotFound", or "SmtpUnknown".
    /// Utilizes MailKit's RFC-compliant multiline response parsing, encoding, and connection state management.
    /// </summary>
    private static async Task<string> MailKitSmtpProbeAsync(string mxHost, string email, int timeoutSeconds, CancellationToken cancellationToken)
    {
        using var client = new MailKitSmtpProbeClient();
        client.Timeout = Math.Max(1, timeoutSeconds) * 1000;

        await client.ConnectAsync(mxHost, 25, SecureSocketOptions.None, cancellationToken);

        try
        {
            return await client.ProbeMailboxAsync("probe@mizan-check.invalid", email, cancellationToken);
        }
        finally
        {
            if (client.IsConnected)
            {
                try
                {
                    await client.DisconnectAsync(true, cancellationToken);
                }
                catch
                {
                    // Best effort disconnect
                }
            }
        }
    }

    /// <summary>
    /// Subclasses MailKit.Net.Smtp.SmtpClient to access the protected SendCommandAsync method
    /// for executing raw MAIL FROM and RCPT TO probing without attempting to transmit an email body.
    /// </summary>
    private sealed class MailKitSmtpProbeClient : MailKit.Net.Smtp.SmtpClient
    {
        public async Task<string> ProbeMailboxAsync(string mailFrom, string rcptTo, CancellationToken cancellationToken)
        {
            try
            {
                var mailFromResponse = await SendCommandAsync($"MAIL FROM:<{mailFrom}>\r\n", cancellationToken);
                var mailFromCode = (int)mailFromResponse.StatusCode;
                if (mailFromCode >= 400)
                {
                    return "SmtpUnknown";
                }

                var rcptResponse = await SendCommandAsync($"RCPT TO:<{rcptTo}>\r\n", cancellationToken);
                var rcptCode = (int)rcptResponse.StatusCode;

                if (rcptCode >= 500 && rcptCode < 600)
                    return "MailboxNotFound";

                if (rcptCode >= 200 && rcptCode < 300)
                    return "SmtpVerified";

                return "SmtpUnknown";
            }
            catch (SmtpCommandException ex)
            {
                var code = (int)ex.StatusCode;
                if (code >= 500 && code < 600)
                    return "MailboxNotFound";

                return "SmtpUnknown";
            }
        }
    }

    /// <summary>
    /// Only cache definitive verdicts. Transient states (timeouts, connection failures)
    /// should not be persisted so the next request gets a fresh attempt.
    /// </summary>
    private static bool ShouldCache(EmailVerificationResult result)
    {
        return result.Reason is
            not "DnsTimeout" and
            not "TimeoutFallback" and
            not "SmtpTimeout" and
            not "SmtpConnectFailed" and
            not "SmtpAllHostsUnreachable";
    }
}
