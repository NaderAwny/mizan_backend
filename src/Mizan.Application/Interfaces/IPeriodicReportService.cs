using Mizan.Application.DTOs.Reports;

namespace Mizan.Application.Interfaces;

public interface IPeriodicReportService
{
    Task<PagedPeriodicReportResponse> GetPagedAsync(
        int ownerUserId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task<PeriodicReportResponse> GetByIdAsync(
        int ownerUserId,
        int reportId,
        CancellationToken cancellationToken = default);

    Task<(FileStream Stream, string ContentType, string FileName)> GetPdfStreamAsync(
        int ownerUserId,
        int reportId,
        CancellationToken cancellationToken = default);
}
