namespace Mizan.Application.DTOs.Notifications;

public class RemindersOptions
{
    public const string SectionName = "Reminders";

    public bool Enabled { get; set; } = true;
    public List<int> DaysBeforeDue { get; set; } = new() { 3, 1 };
    public int CheckIntervalMinutes { get; set; } = 60;
}
