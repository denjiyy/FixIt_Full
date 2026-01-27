namespace FixIt.ViewModels;

public class ApiResponse<T>
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public T? Data { get; set; }
    public Dictionary<string, string[]>? Errors { get; set; }

    public static ApiResponse<T> CreateSuccess(T data, string? message = null) =>
        new()
        {
            Success = true,
            Data = data,
            Message = message
        };

    public static ApiResponse<T> CreateError(string message, Dictionary<string, string[]>? errors = null) =>
        new()
        {
            Success = false,
            Message = message,
            Errors = errors
        };
}

public class PaginatedResponse<T>
{
    public IEnumerable<T> Items { get; set; } = Array.Empty<T>();
    public long Total { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalPages => (int)Math.Ceiling((double)Total / PageSize);
}
