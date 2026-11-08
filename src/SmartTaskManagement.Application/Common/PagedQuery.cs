namespace SmartTaskManagement.Application.Common;

public class PagedQuery
{
    public const int MaxPageSize = 100;

    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 10;
    public string? Search { get; set; }
    public string? SortBy { get; set; }
    public bool Descending { get; set; }

    public int Skip => (Math.Max(PageNumber, 1) - 1) * Take;
    public int Take => Math.Clamp(PageSize, 1, MaxPageSize);
    public string? SearchTerm => string.IsNullOrWhiteSpace(Search) ? null : Search.Trim();
}
