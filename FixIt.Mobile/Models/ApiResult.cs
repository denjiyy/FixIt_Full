namespace FixIt.Mobile.Models;

public record ApiResult(bool Success, string? Error = null, int? HttpStatus = null)
{
    public static ApiResult Ok() => new(true);

    public static ApiResult Fail(string? error, int? httpStatus = null) => new(false, error, httpStatus);
}
