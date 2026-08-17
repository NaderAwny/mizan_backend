using Mizan.Application.DTOs.Reports;
using Mizan.Application.Interfaces;
using Mizan.Core.Entities;
using Mizan.Core.Exceptions;
using Mizan.Core.Interfaces;

namespace Mizan.Application.Services;

public class PeriodicReportService : IPeriodicReportService
{
    private const int MinPageSize = 1;
    private const int MaxPageSize = 50;

    private readonly IUnitOfWork _unitOfWork;

    public PeriodicReportService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<PagedPeriodicReportResponse> GetPagedAsync(
        Guid ownerUserId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, MinPageSize, MaxPageSize);

        var (items, totalCount) = await _unitOfWork.PeriodicReports.GetPagedByOwnerAsync(
            ownerUserId, page, pageSize, cancellationToken);

        var totalPages = totalCount == 0 ? 0 : (int)Math.Ceiling((double)totalCount / pageSize);

        return new PagedPeriodicReportResponse
        {
            Items = items.Select(MapToResponse).ToList(),
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount,
            TotalPages = totalPages
        };
    }

    public async Task<PeriodicReportResponse> GetByIdAsync(
        Guid ownerUserId,
        Guid reportId,
        CancellationToken cancellationToken = default)
    {
        var report = await GetOwnedReportOrThrowAsync(ownerUserId, reportId, cancellationToken);
        return MapToResponse(report);
    }

    public async Task<(FileStream Stream, string ContentType, string FileName)> GetPdfStreamAsync(
        Guid ownerUserId,
        Guid reportId,
        CancellationToken cancellationToken = default)
    {
        var report = await GetOwnedReportOrThrowAsync(ownerUserId, reportId, cancellationToken);

        if (string.IsNullOrWhiteSpace(report.PdfStoragePath) || !File.Exists(report.PdfStoragePath))
        {
            throw new NotFoundException("ملف التقرير غير موجود");
        }

        var stream = new FileStream(report.PdfStoragePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        var fileName = $"mizan-report-batch-{report.BatchNumber}.pdf";

        return (stream, "application/pdf", fileName);
    }

    private async Task<PeriodicReport> GetOwnedReportOrThrowAsync(
        Guid ownerUserId,
        Guid reportId,
        CancellationToken cancellationToken)
    {
        var report = await _unitOfWork.PeriodicReports.GetByIdAsync(reportId, ownerUserId, cancellationToken);
        if (report == null)
        {
            throw new NotFoundException("التقرير غير موجود");
        }

        return report;
    }

    private static PeriodicReportResponse MapToResponse(PeriodicReport report) => new()
    {
        Id = report.Id,
        BatchNumber = report.BatchNumber,
        TransactionCount = report.TransactionCount,
        TotalSalesAmount = report.TotalSalesAmount,
        TotalPurchasesAmount = report.TotalPurchasesAmount,
        GeneratedAt = report.GeneratedAt
    };
}
