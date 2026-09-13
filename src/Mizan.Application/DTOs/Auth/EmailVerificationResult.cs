namespace Mizan.Application.DTOs.Auth;

public class EmailVerificationResult
{
    public bool IsDeliverable { get; set; }
    public string Reason { get; set; } = string.Empty;

    public static EmailVerificationResult Valid(string reason = "Verified") =>
        new() { IsDeliverable = true, Reason = reason };

    public static EmailVerificationResult Invalid(string reason) =>
        new() { IsDeliverable = false, Reason = reason };
}
