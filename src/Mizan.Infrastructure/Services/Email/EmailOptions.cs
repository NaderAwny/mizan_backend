namespace Mizan.Infrastructure.Services.Email;

public class EmailOptions
{
    public const string SectionName = "Email";

    public string Host { get; set; } = "smtp.gmail.com";
    public int Port { get; set; } = 587;
    public string UserName { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string FromEmail { get; set; } = "noreply@mizan.app";
    public string FromName { get; set; } = "Mizan — تطبيق ميزان";
    public bool EnableSsl { get; set; } = true;
    public bool UseMockInDevelopment { get; set; } = true;
}
