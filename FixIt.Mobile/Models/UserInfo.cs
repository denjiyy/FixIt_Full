namespace FixIt.Mobile.Models;

public class UserInfo
{
    public string Id { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public int ReputationPoints { get; set; }
    public string TrustLevel { get; set; } = string.Empty;
}
