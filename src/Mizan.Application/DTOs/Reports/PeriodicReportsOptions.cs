namespace Mizan.Application.DTOs.Reports;

public class PeriodicReportsOptions
{
    public const string SectionName = "PeriodicReports";

    public bool Enabled { get; set; } = true;
    public int TransactionThreshold { get; set; } = 7;
    public int CheckIntervalMinutes { get; set; } = 60;
}
