namespace Mizan.Infrastructure.Services.Email;

public class EmailOptions
{
    public const string SectionName = "Email";

    public string SmtpHost { get; set; } = "smtp.gmail.com";
    public int SmtpPort { get; set; } = 587;
    public string SenderEmail { get; set; } = string.Empty;
    public string SenderPassword { get; set; } = string.Empty;
    public string? ApiKey { get; set; }
    public string FromName { get; set; } = "Mizan — تطبيق ميزان";
    public bool EnableSsl { get; set; } = true;
    public bool UseMockInDevelopment { get; set; } = true;
}
