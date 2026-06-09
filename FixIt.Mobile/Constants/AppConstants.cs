namespace FixIt.Mobile.Constants;

public static class AppConstants
{
    public const string BaseUrl = "https://fixitfull-production.up.railway.app/";
    public const string ApiIssues = "api/issues";
    public const string ApiAuth = "api/auth";
    public const string ApiSafety = "api/safety";

    public const string TokenKey = "fixit_jwt";
    public const string RefreshTokenKey = "fixit_refresh";

    public const string ApiClientName = "FixItApi";
    public const string AuthClientName = "FixItAuth";

    public const string DefaultCityId = "6a02316b4560b1b4d08b0ff6";

    public const string RouteHome = "//home";
    public const string RouteIssues = "//issues";
    public const string RouteReportIssueTab = "//report-issue-tab";
    public const string RouteAlerts = "//alerts";
    public const string RouteAccountTab = "account-tab";
    public const string RouteAccountTabAbsolute = $"//{RouteAccountTab}";
    public const string RouteIssueDetail = "issue-detail";
    public const string RouteMyIssues = "my-issues";
    public const string RouteLeaderboard = "leaderboard";
    public const string RouteHealthReport = "health-report";
    public const string RoutePublicProfile = "public-profile";
    public const string RouteSettings = "settings";
    public const string RouteEditIssue = "edit-issue";
    public const string RouteForgotPassword = "forgot-password";
    public const string RouteCities = "cities";
    public const string RouteHeatmap = "heatmap";
    public const string RouteTagDetail = "tag-detail";
    public const string RouteConnectedAccounts = "connected-accounts";
    public const string RouteHazardMode = "hazard-mode";

    public const string FilterAll = "All";
    public const string FilterNew = "New";
    public const string FilterInProgress = "InProgress";
    public const string FilterResolved = "Resolved";

    public const string StatusNew = "New";
    public const string StatusInProgress = "InProgress";
    public const string StatusResolved = "Resolved";
    public const string StatusCritical = "Critical";

    public const int StatusNewValue = 0;
    public const int StatusConfirmedValue = 1;
    public const int StatusInProgressValue = 2;
    public const int StatusFixedValue = 3;

    public static readonly string[] FilterOptions =
    [
        FilterAll,
        FilterNew,
        FilterInProgress,
        FilterResolved
    ];
}
