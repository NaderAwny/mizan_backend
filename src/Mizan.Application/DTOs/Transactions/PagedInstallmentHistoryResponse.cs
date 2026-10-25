namespace Mizan.Application.DTOs.Transactions;

/// <summary>
/// Response مُقسَّم صفحات لتايم لاين الأقساط — متوافق مع نفس شكل Pagination الموجود.
/// </summary>
public class PagedInstallmentHistoryResponse
{
    public IReadOnlyList<InstallmentHistoryItemDto> Items { get; set; } = Array.Empty<InstallmentHistoryItemDto>();
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalCount { get; set; }
    public int TotalPages { get; set; }
}
