using Mizan.Application.DTOs.Reports;

namespace Mizan.Application.Interfaces;

public interface IReportPdfGenerator
{
    byte[] GenerateReportPdf(PeriodicReportPdfModel model);
}
