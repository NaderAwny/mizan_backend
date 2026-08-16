namespace Mizan.Infrastructure.Services.Email;

public class EmailOptions
{
    public const string SectionName = "Email";

    public string? ApiKey { get; set; }
    public string SenderEmail { get; set; } = string.Empty;
    public string SenderName { get; set; } = "Mizan";
    public bool UseMockInDevelopment { get; set; } = true;
}
