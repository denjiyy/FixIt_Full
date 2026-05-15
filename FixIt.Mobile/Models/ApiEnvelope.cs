namespace FixIt.Mobile.Models;

public class ApiEnvelope<T>
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public T? Data { get; set; }
}

public class PaginatedEnvelope<T>
{
    public List<T> Items { get; set; } = [];
    public long Total { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
}

public class TokenPayload
{
    public string AccessToken { get; set; } = string.Empty;
    public string RefreshToken { get; set; } = string.Empty;
    public TokenUserPayload? User { get; set; }
}

public class TokenUserPayload
{
    public string? Id { get; set; }
    public string? Email { get; set; }
    public string? DisplayName { get; set; }
    public int? ReputationScore { get; set; }
    public int? TrustLevel { get; set; }
}

public class RefreshPayload
{
    public string AccessToken { get; set; } = string.Empty;
    public string RefreshToken { get; set; } = string.Empty;
}

public class VoteResultPayload
{
    public int Upvotes { get; set; }
    public int Downvotes { get; set; }
}
