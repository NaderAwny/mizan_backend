using Mizan.Application.DTOs.Reports;
using Mizan.Application.Interfaces;
using Mizan.Core.Enums;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Mizan.Infrastructure.Services.Reports;

// NOTE: [Deprecated] — هذا النظام لتوليد تقارير PDF سيتم استبداله كلياً بنظام Statistics API (/api/statistics) بعد التأكد من عمله
[Obsolete("Deprecated — Use Statistics API (/api/statistics) instead")]
public class ReportPdfGenerator : IReportPdfGenerator
{
    static ReportPdfGenerator()
    {
        // Set QuestPDF Community License
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public byte[] GenerateReportPdf(PeriodicReportPdfModel model)
    {
        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(32);
                page.PageColor(Colors.White);
                page.DefaultTextStyle(x => x.FontFamily("Arial").FontSize(10).FontColor(Colors.Grey.Darken3));

                page.Header().Element(header => ComposeHeader(header, model));
                page.Content().Element(content => ComposeContent(content, model));
                page.Footer().Element(ComposeFooter);
            });
        });

        return document.GeneratePdf();
    }

    private static void ComposeHeader(IContainer container, PeriodicReportPdfModel model)
    {
        container.Column(column =>
        {
            column.Item().Row(row =>
            {
                row.RelativeItem().Column(col =>
                {
                    col.Item().Text("تطبيق ميزان — Mizan")
                        .FontSize(20)
                        .Bold()
                        .FontColor(Colors.Green.Darken2);

                    col.Item().Text($"تقرير دوري للعمليات — الدفعة #{model.BatchNumber}")
                        .FontSize(14)
                        .SemiBold()
                        .FontColor(Colors.Grey.Darken2);
                });

                row.ConstantItem(180).Column(col =>
                {
                    col.Item().AlignRight().Text($"تاريخ التقرير: {model.GeneratedAt:yyyy-MM-dd HH:mm}")
                        .FontSize(9)
                        .FontColor(Colors.Grey.Medium);

                    if (!string.IsNullOrWhiteSpace(model.RecipientName))
                    {
                        col.Item().AlignRight().Text($"المستخدم: {model.RecipientName}")
                            .FontSize(9)
                            .FontColor(Colors.Grey.Darken1);
                    }
                });
            });

            column.Item().PaddingTop(12).LineHorizontal(1.5f).LineColor(Colors.Green.Darken1);
        });
    }

    private static void ComposeContent(IContainer container, PeriodicReportPdfModel model)
    {
        decimal netAmount = model.TotalSalesAmount - model.TotalPurchasesAmount;

        container.PaddingVertical(16).Column(column =>
        {
            // 1. Summary Cards
            column.Item().Row(row =>
            {
                // Sales card
                row.RelativeItem().Border(1).BorderColor(Colors.Grey.Lighten2).Background(Colors.Grey.Lighten5).Padding(10).Column(c =>
                {
                    c.Item().Text("إجمالي المبيعات (Sale)").FontSize(10).FontColor(Colors.Grey.Darken1);
                    c.Item().PaddingTop(4).Text($"{model.TotalSalesAmount:N2} ج.م").FontSize(14).Bold().FontColor(Colors.Green.Darken2);
                });

                row.ConstantItem(10);

                // Purchases card
                row.RelativeItem().Border(1).BorderColor(Colors.Grey.Lighten2).Background(Colors.Grey.Lighten5).Padding(10).Column(c =>
                {
                    c.Item().Text("إجمالي المشتريات (Purchase)").FontSize(10).FontColor(Colors.Grey.Darken1);
                    c.Item().PaddingTop(4).Text($"{model.TotalPurchasesAmount:N2} ج.م").FontSize(14).Bold().FontColor(Colors.Red.Darken2);
                });

                row.ConstantItem(10);

                // Net card
                row.RelativeItem().Border(1).BorderColor(Colors.Grey.Lighten2).Background(Colors.Grey.Lighten5).Padding(10).Column(c =>
                {
                    c.Item().Text("صافي العمليات (Net)").FontSize(10).FontColor(Colors.Grey.Darken1);
                    c.Item().PaddingTop(4).Text($"{netAmount:N2} ج.م").FontSize(14).Bold().FontColor(netAmount >= 0 ? Colors.Green.Darken2 : Colors.Red.Darken2);
                });

                row.ConstantItem(10);

                // Count card
                row.RelativeItem().Border(1).BorderColor(Colors.Grey.Lighten2).Background(Colors.Grey.Lighten5).Padding(10).Column(c =>
                {
                    c.Item().Text("عدد العمليات").FontSize(10).FontColor(Colors.Grey.Darken1);
                    c.Item().PaddingTop(4).Text($"{model.TransactionCount}").FontSize(14).Bold().FontColor(Colors.Blue.Darken2);
                });
            });

            column.Item().PaddingTop(20).Text($"تفاصيل العمليات المشمولة في الدفعة #{model.BatchNumber}")
                .FontSize(12)
                .Bold()
                .FontColor(Colors.Grey.Darken3);

            // 2. Transactions Table
            column.Item().PaddingTop(8).Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.ConstantColumn(30); // #
                    columns.RelativeColumn(3);  // Contact Name
                    columns.RelativeColumn(2);  // Type
                    columns.RelativeColumn(2);  // Amount
                    columns.RelativeColumn(2);  // Date
                    columns.RelativeColumn(2);  // Plan
                });

                // Header
                table.Header(header =>
                {
                    header.Cell().Background(Colors.Green.Darken2).Padding(6).AlignCenter().Text("#").FontColor(Colors.White).Bold();
                    header.Cell().Background(Colors.Green.Darken2).Padding(6).Text("الطرف").FontColor(Colors.White).Bold();
                    header.Cell().Background(Colors.Green.Darken2).Padding(6).AlignCenter().Text("النوع").FontColor(Colors.White).Bold();
                    header.Cell().Background(Colors.Green.Darken2).Padding(6).AlignRight().Text("المبلغ").FontColor(Colors.White).Bold();
                    header.Cell().Background(Colors.Green.Darken2).Padding(6).AlignCenter().Text("التاريخ").FontColor(Colors.White).Bold();
                    header.Cell().Background(Colors.Green.Darken2).Padding(6).AlignCenter().Text("نظام الدفع").FontColor(Colors.White).Bold();
                });

                // Rows
                int index = 1;
                foreach (var tx in model.Transactions)
                {
                    var bgColor = index % 2 == 0 ? Colors.Grey.Lighten4 : Colors.White;
                    string typeText = tx.Type == TransactionType.Sale ? "بيع" : "شراء";
                    var typeColor = tx.Type == TransactionType.Sale ? Colors.Green.Darken2 : Colors.Red.Darken2;
                    string planText = tx.IsInstallment ? "أقساط" : "كاش / فوري";

                    table.Cell().Background(bgColor).BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(6).AlignCenter().Text(index.ToString());
                    table.Cell().Background(bgColor).BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(6).Text(string.IsNullOrWhiteSpace(tx.ContactName) ? "-" : tx.ContactName);
                    table.Cell().Background(bgColor).BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(6).AlignCenter().Text(typeText).FontColor(typeColor).SemiBold();
                    table.Cell().Background(bgColor).BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(6).AlignRight().Text($"{tx.Amount:N2}").Bold();
                    table.Cell().Background(bgColor).BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(6).AlignCenter().Text($"{tx.TransactionDate:yyyy-MM-dd}");
                    table.Cell().Background(bgColor).BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(6).AlignCenter().Text(planText);

                    index++;
                }
            });
        });
    }

    private static void ComposeFooter(IContainer container)
    {
        container.Column(col =>
        {
            col.Item().LineHorizontal(0.5f).LineColor(Colors.Grey.Lighten2);
            col.Item().PaddingTop(6).Row(row =>
            {
                row.RelativeItem().Text("تم إنشاء هذا التقرير تلقائياً بواسطة تطبيق ميزان (Mizan App)")
                    .FontSize(8)
                    .FontColor(Colors.Grey.Medium);

                row.RelativeItem().AlignRight().Text(text =>
                {
                    text.Span("صفحة ");
                    text.CurrentPageNumber();
                    text.Span(" من ");
                    text.TotalPages();
                });
            });
        });
    }
}
