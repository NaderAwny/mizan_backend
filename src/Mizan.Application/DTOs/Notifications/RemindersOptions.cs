namespace Mizan.Application.DTOs.Notifications;

public class RemindersOptions
{
    public const string SectionName = "Reminders";

    public bool Enabled { get; set; } = true;
    public List<int> DaysBeforeDue { get; set; } = new() { 3, 1 };
    public int CheckIntervalMinutes { get; set; } = 5;
    public int OverdueLookbackDays { get; set; } = 7;
    public int MaxChannelAttempts { get; set; } = 3;
    public int RetryBackoffMinutes { get; set; } = 5;
}
