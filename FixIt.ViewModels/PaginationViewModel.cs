namespace FixIt.ViewModels;

/// <summary>
/// Pagination view model for reusable _Pagination.cshtml partial
/// </summary>
public class PaginationViewModel
{
    public int CurrentPage { get; set; } = 1;
    public int TotalPages { get; set; } = 1;
    public string BaseUrl { get; set; } = "";

    /// <summary>
    /// Build pagination URL for a given page number
    /// </summary>
    public string BuildPageUrl(int pageNumber)
    {
        var separator = BaseUrl.Contains('?') ? "&" : "?";
        return $"{BaseUrl}{separator}page={pageNumber}";
    }
}
