using Mizan.Application.DTOs.Reports;

namespace Mizan.Application.Interfaces;

public interface IPeriodicReportService
{
    Task<PagedPeriodicReportResponse> GetPagedAsync(
        Guid ownerUserId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task<PeriodicReportResponse> GetByIdAsync(
        Guid ownerUserId,
        Guid reportId,
        CancellationToken cancellationToken = default);

    Task<(FileStream Stream, string ContentType, string FileName)> GetPdfStreamAsync(
        Guid ownerUserId,
        Guid reportId,
        CancellationToken cancellationToken = default);
}
