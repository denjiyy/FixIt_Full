using System.Text.Json.Serialization;
using FixIt.Mobile.Models;

namespace FixIt.Mobile.Services;

[JsonSerializable(typeof(List<Issue>))]
[JsonSerializable(typeof(Issue))]
[JsonSerializable(typeof(UserInfo))]
[JsonSerializable(typeof(ApiResult))]
[JsonSerializable(typeof(AuthResult))]
[JsonSerializable(typeof(ReportIssueRequest))]
[JsonSerializable(typeof(List<Comment>))]
[JsonSerializable(typeof(Comment))]
[JsonSerializable(typeof(List<SafetyHazard>))]
[JsonSerializable(typeof(SafetyHazard))]
[JsonSerializable(typeof(LeaderboardResult))]
[JsonSerializable(typeof(List<LeaderboardEntry>))]
[JsonSerializable(typeof(CityHealthReport))]
[JsonSerializable(typeof(IssueAnalysis))]
[JsonSerializable(typeof(IssueFilterResult))]
[JsonSerializable(typeof(PublicUserProfile))]
[JsonSerializable(typeof(ApiEnvelope<object>))]
[JsonSerializable(typeof(ApiEnvelope<TokenPayload>))]
[JsonSerializable(typeof(ApiEnvelope<RefreshPayload>))]
public partial class FixItJsonContext : JsonSerializerContext
{
}
