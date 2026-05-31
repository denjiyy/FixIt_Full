using System.Text;
using FixIt.Models.AI;
using FixIt.Models.Issues;

namespace FixIt.Services.AI;

/// <summary>
/// Local, dependency-free duplicate detection for civic issues. Ranks candidate
/// issues against a target by textual overlap (title + description), nudged by
/// same-category and geographic proximity. Runs with or without OpenAI — the LLM
/// can't see the database, so duplicates are always computed here.
/// </summary>
public static class DuplicateDetection
{
    // Minimum textual overlap (0–1) before a candidate is considered at all — keeps
    // geography/category alone from ever flagging an unrelated issue.
    private const double TextFloor = 0.34;

    private static readonly HashSet<string> StopWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "the", "and", "for", "are", "was", "this", "that", "near", "has", "have", "been",
        "very", "please", "there", "here", "will", "would", "can", "could", "our", "your",
        "their", "they", "them", "from", "but", "not", "out", "down", "with", "into", "over",
        "report", "reported", "issue", "issues", "problem", "area", "street", "road", "again",
        "still", "some", "any", "all", "its", "his", "her", "who", "what", "when", "where",
    };

    /// <summary>Normalizes text into a set of significant tokens.</summary>
    public static HashSet<string> Tokenize(string? text)
    {
        var tokens = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(text))
        {
            return tokens;
        }

        var builder = new StringBuilder(text.Length);
        foreach (var ch in text.ToLowerInvariant())
        {
            builder.Append(char.IsLetterOrDigit(ch) ? ch : ' ');
        }

        foreach (var raw in builder.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            var token = Singularize(raw);
            if (token.Length >= 3 && !StopWords.Contains(token))
            {
                tokens.Add(token);
            }
        }

        return tokens;
    }

    // Light plural folding so "potholes" matches "pothole", "cars" matches "car".
    private static string Singularize(string token)
    {
        if (token.Length >= 4 && token.EndsWith("s", StringComparison.Ordinal) && !token.EndsWith("ss", StringComparison.Ordinal))
        {
            return token[..^1];
        }
        return token;
    }

    /// <summary>
    /// Textual similarity in 0–1, a blend of Jaccard (overall overlap) and the
    /// overlap coefficient (so a short title that is a subset of a longer one still scores).
    /// </summary>
    public static double TextSimilarity(string? aText, string? bText)
        => TextSimilarity(Tokenize(aText), Tokenize(bText));

    public static double TextSimilarity(HashSet<string> a, HashSet<string> b)
    {
        if (a.Count == 0 || b.Count == 0)
        {
            return 0;
        }

        var intersection = a.Count <= b.Count
            ? a.Count(b.Contains)
            : b.Count(a.Contains);
        if (intersection == 0)
        {
            return 0;
        }

        var union = a.Count + b.Count - intersection;
        var jaccard = (double)intersection / union;
        var overlap = (double)intersection / Math.Min(a.Count, b.Count);
        return 0.5 * jaccard + 0.5 * overlap;
    }

    /// <summary>Great-circle distance between two coordinates, in metres.</summary>
    public static double DistanceMeters(double lat1, double lon1, double lat2, double lon2)
    {
        const double earthRadius = 6_371_000d;
        double ToRad(double d) => d * Math.PI / 180d;

        var dLat = ToRad(lat2 - lat1);
        var dLon = ToRad(lon2 - lon1);
        var h = Math.Sin(dLat / 2) * Math.Sin(dLat / 2)
                + Math.Cos(ToRad(lat1)) * Math.Cos(ToRad(lat2)) * Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        return 2 * earthRadius * Math.Asin(Math.Min(1, Math.Sqrt(h)));
    }

    /// <summary>
    /// Combines textual similarity with category + proximity nudges into a 0–100 score.
    /// </summary>
    public static int CombinedScore(double textSimilarity, bool sameCategory, double? distanceMeters)
    {
        var score = textSimilarity;

        if (sameCategory)
        {
            score += 0.08;
        }

        if (distanceMeters.HasValue)
        {
            if (distanceMeters.Value <= 150) { score += 0.15; }
            else if (distanceMeters.Value <= 500) { score += 0.08; }
            else if (distanceMeters.Value > 3000) { score -= 0.10; }
        }

        return (int)Math.Round(Math.Clamp(score, 0, 1) * 100);
    }

    /// <summary>
    /// Ranks <paramref name="candidates"/> against <paramref name="target"/> and returns the
    /// strongest likely duplicates (highest score first).
    /// </summary>
    public static List<DuplicateMatch> FindDuplicates(
        Issue target,
        IEnumerable<Issue> candidates,
        int maxResults = 3,
        int minScore = 55)
    {
        var targetTokens = Tokenize($"{target.Title} {target.Description}");
        if (targetTokens.Count == 0)
        {
            return new List<DuplicateMatch>();
        }

        var targetCoords = Coordinates(target);

        var scored = new List<(int Score, double TextSim, DuplicateMatch Match)>();

        foreach (var candidate in candidates)
        {
            if (candidate is null || candidate.Id == target.Id)
            {
                continue;
            }

            var candidateTokens = Tokenize($"{candidate.Title} {candidate.Description}");
            var textSim = TextSimilarity(targetTokens, candidateTokens);
            if (textSim < TextFloor)
            {
                continue;
            }

            var sameCategory = target.Category.HasValue
                && candidate.Category.HasValue
                && target.Category == candidate.Category;

            double? distance = null;
            var candidateCoords = Coordinates(candidate);
            if (targetCoords.HasValue && candidateCoords.HasValue)
            {
                distance = DistanceMeters(
                    targetCoords.Value.Lat, targetCoords.Value.Lng,
                    candidateCoords.Value.Lat, candidateCoords.Value.Lng);
            }

            var score = CombinedScore(textSim, sameCategory, distance);
            if (score < minScore)
            {
                continue;
            }

            scored.Add((score, textSim, new DuplicateMatch
            {
                IssueId = candidate.Id,
                IssueTitle = candidate.Title,
                SimilarityScore = score,
                Reason = BuildReason(targetTokens, candidateTokens, sameCategory, distance)
            }));
        }

        return scored
            .OrderByDescending(s => s.Score)
            .ThenByDescending(s => s.TextSim)
            .Take(maxResults)
            .Select(s => s.Match)
            .ToList();
    }

    private static string BuildReason(
        HashSet<string> targetTokens,
        HashSet<string> candidateTokens,
        bool sameCategory,
        double? distanceMeters)
    {
        var shared = targetTokens
            .Where(candidateTokens.Contains)
            .OrderByDescending(t => t.Length)
            .Take(3)
            .ToList();

        var bits = new List<string>();
        if (shared.Count > 0)
        {
            bits.Add($"shared terms: {string.Join(", ", shared)}");
        }
        if (sameCategory)
        {
            bits.Add("same category");
        }
        if (distanceMeters is <= 500)
        {
            bits.Add("nearby location");
        }

        if (bits.Count == 0)
        {
            return "Similar wording";
        }

        var reason = string.Join("; ", bits);
        return char.ToUpperInvariant(reason[0]) + reason[1..];
    }

    private static (double Lat, double Lng)? Coordinates(Issue issue)
    {
        var coordinates = issue.Location?.Coordinates;
        if (coordinates is null)
        {
            return null;
        }

        return (coordinates.Latitude, coordinates.Longitude);
    }
}
