namespace Mizan.Infrastructure.Services.Push;

public class FirebaseOptions
{
    public const string SectionName = "Firebase";

    public bool Enabled { get; set; } = false;
    public string? ProjectId { get; set; }
    public string? ServiceAccountJson { get; set; }
    public string? ServiceAccountPath { get; set; }
    public string AndroidChannelId { get; set; } = "mizan_reminders";
}
