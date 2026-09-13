namespace Mizan.Infrastructure.Services.Email;

public class EmailVerificationOptions
{
    public const string SectionName = "EmailVerification";

    /// <summary>Timeout in seconds for the DNS MX lookup.</summary>
    public int TimeoutSeconds { get; set; } = 3;

    /// <summary>
    /// When true, DNS failures are treated as blocked (fail-closed).
    /// When false, DNS failures allow the request through (fail-open).
    /// </summary>
    public bool FailClosed { get; set; } = true;

    /// <summary>Extra domains to block in addition to the built-in disposable domain list.</summary>
    public List<string> BlockedDomains { get; set; } = new();

    /// <summary>
    /// When true, performs an SMTP RCPT-TO handshake after MX lookup to verify
    /// that the mailbox actually exists on the remote server.
    /// SMTP connection errors always fail-open regardless of FailClosed.
    /// </summary>
    public bool EnableSmtpProbe { get; set; } = false;

    /// <summary>Timeout in seconds for the SMTP probe TCP connection and command round-trip.</summary>
    public int SmtpTimeoutSeconds { get; set; } = 5;

    /// <summary>How long (in minutes) to cache a definitive verification result per email address.</summary>
    public int CacheMinutes { get; set; } = 60;
}
