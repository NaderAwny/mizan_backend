namespace Mizan.Application.DTOs.Reports;

public record PeriodicReportEmailJob(
    Guid ReportId,
    Guid OwnerUserId,
    string RecipientEmail,
    string RecipientName,
    int BatchNumber,
    byte[] PdfBytes
);
