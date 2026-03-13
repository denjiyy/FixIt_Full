using FixIt.Data.Repository.Contracts;
using FixIt.Models.Gamification;
using FixIt.Models.Users;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;

namespace FixIt.Services.Gamification;

/// <summary>
/// Service for managing user reputation and achievements
/// </summary>
public interface IReputationService
{
    Task<UserReputation> GetOrCreateUserReputationAsync(string userId);
    Task<UserReputation?> GetUserReputationAsync(string userId);
    Task AddPointsAsync(string userId, int points, string reason, string? issueId = null, string? commentId = null);
    Task CheckAndAwardAchievementsAsync(string userId);
    Task<List<LeaderboardEntry>> GetLeaderboardAsync(LeaderboardPeriod period, int take = 10);
    Task<LeaderboardEntry?> GetUserLeaderboardRankAsync(string userId, LeaderboardPeriod period);
    Task<int> GetUserTrustLevelAsync(string userId);
    Task UpdateTrustLevelAsync(string userId);
    Task RegenerateWeeklyLeaderboardAsync();
    Task RegenerateMonthlyLeaderboardAsync();
    Task RegenerateAllTimeLeaderboardAsync();
}

public class ReputationService : IReputationService
{
    private readonly IRepository<UserReputation> _reputationRepository;
    private readonly IRepository<ReputationTransaction> _transactionRepository;
    private readonly IRepository<LeaderboardEntry> _leaderboardRepository;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<ReputationService> _logger;

    public ReputationService(
        IRepository<UserReputation> reputationRepository,
        IRepository<ReputationTransaction> transactionRepository,
        IRepository<LeaderboardEntry> leaderboardRepository,
        UserManager<ApplicationUser> userManager,
        ILogger<ReputationService> logger)
    {
        _reputationRepository = reputationRepository;
        _transactionRepository = transactionRepository;
        _leaderboardRepository = leaderboardRepository;
        _userManager = userManager;
        _logger = logger;
    }

    /// <summary>
    /// Gets user's reputation or creates a new one if doesn't exist
    /// </summary>
    public async Task<UserReputation> GetOrCreateUserReputationAsync(string userId)
    {
        var reputation = await _reputationRepository.GetByIdAsync(userId);
        
        if (reputation == null)
        {
            reputation = new UserReputation
            {
                Id = userId,
                UserId = userId,
                TotalPoints = 0,
                TrustLevel = 0
            };
            
            await _reputationRepository.InsertAsync(reputation);
            _logger.LogInformation($"Created reputation record for user {userId}");
        }

        return reputation;
    }

    /// <summary>
    /// Gets user's reputation
    /// </summary>
    public async Task<UserReputation?> GetUserReputationAsync(string userId)
    {
        return await _reputationRepository.GetByIdAsync(userId);
    }

    /// <summary>
    /// Adds reputation points to a user
    /// </summary>
    public async Task AddPointsAsync(string userId, int points, string reason, string? issueId = null, string? commentId = null)
    {
        // Get or create user reputation
        var reputation = await GetOrCreateUserReputationAsync(userId);

        // Record the transaction
        var transaction = new ReputationTransaction
        {
            UserId = userId,
            Points = points,
            Reason = reason,
            RelatedIssueId = issueId,
            RelatedCommentId = commentId
        };
        await _transactionRepository.InsertAsync(transaction);

        // Update reputation points
        reputation.TotalPoints += points;
        reputation.LastUpdatedAt = DateTime.UtcNow;

        // Update stats based on reason
        switch (reason)
        {
            case "issue_reported":
                reputation.IssuesReported++;
                break;
            case "issue_confirmed":
                // Points awarded to reporter when issue is confirmed
                break;
            case "comment_posted":
                reputation.CommentsPosted++;
                break;
            case "received_upvote":
                reputation.UpvotesReceived++;
                break;
            case "issue_resolved":
                reputation.IssuesResolved++;
                break;
            case "hazard_confirmed":
                // Points awarded to hazard reporter when confirmed
                break;
        }

        // Save updated reputation FIRST (with new points)
        await _reputationRepository.ReplaceAsync(reputation.Id, reputation);

        // THEN update trust level (so it sees the new TotalPoints)
        await UpdateTrustLevelAsync(userId);

        // Check for new achievements
        await CheckAndAwardAchievementsAsync(userId);

        // FINAL trust level update (achievements may have added more points)
        await UpdateTrustLevelAsync(userId);

        _logger.LogInformation($"Added {points} points to user {userId} for: {reason}");
    }

    /// <summary>
    /// Checks and awards achievements if user qualifies
    /// </summary>
    public async Task CheckAndAwardAchievementsAsync(string userId)
    {
        var reputation = await GetUserReputationAsync(userId);
        if (reputation == null) return;

        var existingAchievementTypes = reputation.Achievements.Select(a => a.Type).ToList();

        // Check each achievement condition
        var achievementsToAdd = new List<Achievement>();

        // First Reporter (1 issue reported)
        if (reputation.IssuesReported >= 1 && !existingAchievementTypes.Contains(AchievementType.FirstReporter))
        {
            achievementsToAdd.Add(CreateAchievement(AchievementType.FirstReporter, "First Reporter", "Reported your first issue", 10));
        }

        // Helpful Commenter (5 comments)
        if (reputation.CommentsPosted >= 5 && !existingAchievementTypes.Contains(AchievementType.HelpfulCommenteer))
        {
            achievementsToAdd.Add(CreateAchievement(AchievementType.HelpfulCommenteer, "Helpful Commenter", "Posted 5 helpful comments", 15));
        }

        // Community Helper (10 comments)
        if (reputation.CommentsPosted >= 10 && !existingAchievementTypes.Contains(AchievementType.CommunityHelper))
        {
            achievementsToAdd.Add(CreateAchievement(AchievementType.CommunityHelper, "Community Helper", "Posted 10+ helpful comments", 25));
        }

        // Issue Solver (1 issue resolved)
        if (reputation.IssuesResolved >= 1 && !existingAchievementTypes.Contains(AchievementType.IssueSolver))
        {
            achievementsToAdd.Add(CreateAchievement(AchievementType.IssueSolver, "Issue Solver", "Your report led to an issue being resolved", 30));
        }

        // Civic Contributor (50 points)
        if (reputation.TotalPoints >= 50 && !existingAchievementTypes.Contains(AchievementType.CivicContributor))
        {
            achievementsToAdd.Add(CreateAchievement(AchievementType.CivicContributor, "Civic Contributor", "Earned 50+ reputation points", 20));
        }

        // Community Champion (150 points)
        if (reputation.TotalPoints >= 150 && !existingAchievementTypes.Contains(AchievementType.CommunityChampion))
        {
            achievementsToAdd.Add(CreateAchievement(AchievementType.CommunityChampion, "Community Champion", "Earned 150+ reputation points", 30));
        }

        // Civic Leader (300+ points)
        if (reputation.TotalPoints >= 300 && !existingAchievementTypes.Contains(AchievementType.CivicLeader))
        {
            achievementsToAdd.Add(CreateAchievement(AchievementType.CivicLeader, "Civic Leader", "Earned 300+ reputation points", 50));
        }

        // Verified Citizen
        var user = await _userManager.FindByIdAsync(userId);
        if (user?.EmailConfirmed == true && !existingAchievementTypes.Contains(AchievementType.VerifiedCitizen))
        {
            achievementsToAdd.Add(CreateAchievement(AchievementType.VerifiedCitizen, "Verified Citizen", "Verified your email address", 5));
        }

        // Add new achievements
        if (achievementsToAdd.Any())
        {
            reputation.Achievements.AddRange(achievementsToAdd);
            
            // Add points from achievements
            foreach (var achievement in achievementsToAdd)
            {
                if (achievement.PointsReward > 0)
                {
                    reputation.TotalPoints += achievement.PointsReward;
                }
            }

            await _reputationRepository.ReplaceAsync(reputation.Id, reputation);
            _logger.LogInformation($"Awarded {achievementsToAdd.Count} new achievements to user {userId}");
        }
    }

    /// <summary>
    /// Updates user's trust level based on reputation points
    /// </summary>
    public async Task UpdateTrustLevelAsync(string userId)
    {
        var reputation = await GetUserReputationAsync(userId);
        if (reputation == null) return;

        int newTrustLevel = reputation.TotalPoints switch
        {
            < 11 => 0,      // New (0-10 pts)
            < 51 => 1,      // Active (11-50 pts)
            < 151 => 2,     // Trusted (51-150 pts)
            _ => 3          // Leader (150+ pts)
        };

        if (newTrustLevel != reputation.TrustLevel)
        {
            reputation.TrustLevel = newTrustLevel;
            await _reputationRepository.ReplaceAsync(reputation.Id, reputation);
            
            // Also update ApplicationUser's denormalized trust level
            var user = await _userManager.FindByIdAsync(userId);
            if (user != null)
            {
                user.TrustLevel = newTrustLevel;
                await _userManager.UpdateAsync(user);
            }

            _logger.LogInformation($"Updated user {userId} trust level to {newTrustLevel}");
        }
    }

    /// <summary>
    /// Gets the leaderboard for a specific period
    /// </summary>
    public async Task<List<LeaderboardEntry>> GetLeaderboardAsync(LeaderboardPeriod period, int take = 10)
    {
        var leaderboard = await _leaderboardRepository.FindAsync(e => e.Period == period);
        
        return leaderboard
            .OrderBy(e => e.Rank)
            .Take(take)
            .ToList();
    }

    /// <summary>
    /// Gets a user's leaderboard rank for a specific period
    /// </summary>
    public async Task<LeaderboardEntry?> GetUserLeaderboardRankAsync(string userId, LeaderboardPeriod period)
    {
        var leaderboard = await _leaderboardRepository.FindAsync(e => e.UserId == userId && e.Period == period);
        
        return leaderboard.FirstOrDefault();
    }

    /// <summary>
    /// Gets user's current trust level
    /// </summary>
    public async Task<int> GetUserTrustLevelAsync(string userId)
    {
        var reputation = await GetUserReputationAsync(userId);
        return reputation?.TrustLevel ?? 0;
    }

    /// <summary>
    /// Regenerates the weekly leaderboard
    /// </summary>
    public async Task RegenerateWeeklyLeaderboardAsync()
    {
        await RegenerateLeaderboardAsync(LeaderboardPeriod.Weekly, DateTime.UtcNow.AddDays(-7));
    }

    /// <summary>
    /// Regenerates the monthly leaderboard
    /// </summary>
    public async Task RegenerateMonthlyLeaderboardAsync()
    {
        await RegenerateLeaderboardAsync(LeaderboardPeriod.Monthly, DateTime.UtcNow.AddMonths(-1));
    }

    /// <summary>
    /// Regenerates the all-time leaderboard
    /// </summary>
    public async Task RegenerateAllTimeLeaderboardAsync()
    {
        await RegenerateLeaderboardAsync(LeaderboardPeriod.AllTime, DateTime.MinValue);
    }

    /// <summary>
    /// <summary>
    /// Internal method to regenerate leaderboard for a period
    /// </summary>
    private async Task RegenerateLeaderboardAsync(LeaderboardPeriod period, DateTime sinceDate)
    {
        var entries = new List<dynamic>();

        if (period == LeaderboardPeriod.AllTime)
        {
            // For AllTime, use UserReputation.TotalPoints directly (more reliable than summing transactions)
            var allReputations = await _reputationRepository.FindAsync(r => r.TotalPoints > 0);
            entries = allReputations
                .OrderByDescending(r => r.TotalPoints)
                .Select(r => new { UserId = r.UserId, Points = r.TotalPoints })
                .Cast<dynamic>()
                .ToList();
        }
        else
        {
            // For Weekly/Monthly, use points earned in the specific period from transactions
            var transactions = await _transactionRepository.FindAsync(t => t.CreatedAt >= sinceDate);
            entries = transactions
                .GroupBy(t => t.UserId)
                .Select(g => new { UserId = g.Key, Points = g.Sum(t => t.Points) })
                .OrderByDescending(x => x.Points)
                .Cast<dynamic>()
                .ToList();
        }

        // Clear old leaderboard entries for this period
        var oldEntries = await _leaderboardRepository.FindAsync(e => e.Period == period);
        var entriesToRemove = oldEntries.ToList();
        foreach (var entry in entriesToRemove)
        {
            await _leaderboardRepository.DeleteAsync(entry.Id);
        }

        // Create new leaderboard entries
        var newEntries = new List<LeaderboardEntry>();
        for (int i = 0; i < entries.Count; i++)
        {
            var entry = entries[i];
            var user = await _userManager.FindByIdAsync(entry.UserId);
            var userReputation = await GetUserReputationAsync(entry.UserId);
            
            if (user != null)
            {
                newEntries.Add(new LeaderboardEntry
                {
                    UserId = entry.UserId,
                    UserDisplayName = user.DisplayName,
                    UserAvatarId = user.AvatarMediaId,
                    Points = entry.Points,
                    Rank = i + 1,
                    TrustLevel = userReputation?.TrustLevel ?? 0,
                    Period = period
                });
            }
        }

        // Save new leaderboard entries
        foreach (var entry in newEntries)
        {
            await _leaderboardRepository.InsertAsync(entry);
        }

        _logger.LogInformation($"Regenerated {period} leaderboard with {newEntries.Count} entries");
    }

    /// <summary>
    /// Helper method to create an achievement
    /// </summary>
    private static Achievement CreateAchievement(AchievementType type, string name, string description, int points = 0)
    {
        return new Achievement
        {
            Type = type,
            Name = name,
            Description = description,
            PointsReward = points,
            EarnedAt = DateTime.UtcNow
        };
    }
}
