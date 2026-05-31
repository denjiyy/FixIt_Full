using FixIt.Data.Repository.Contracts;
using FixIt.Models.AI;
using FixIt.Models.Enums;
using FixIt.Models.Issues;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Net.Http.Json;
using System.Text.Json;

namespace FixIt.Services.AI;

/// <summary>
/// Interface for AI-powered issue analysis
/// </summary>
public interface IIssueAnalysisService
{
    Task<IssueAnalysis> AnalyzeIssueAsync(string issueId);
    Task<IssueAnalysis?> GetAnalysisAsync(string issueId);
    Task<List<string>> SuggestTagsAsync(string title, string description);
}

public class OpenAIIssueAnalysisService : IIssueAnalysisService
{
    private readonly IRepository<IssueAnalysis> _analysisRepository;
    private readonly IRepository<Issue> _issueRepository;
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<OpenAIIssueAnalysisService> _logger;

    private const string OpenAIApiUrl = "https://api.openai.com/v1/chat/completions";

    public OpenAIIssueAnalysisService(
        IRepository<IssueAnalysis> analysisRepository,
        IRepository<Issue> issueRepository,
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<OpenAIIssueAnalysisService> logger)
    {
        _analysisRepository = analysisRepository;
        _issueRepository = issueRepository;
        _httpClient = httpClient;
        _configuration = configuration;
        _logger = logger;
    }

    /// <summary>
    /// Analyzes a single issue using OpenAI or fallback heuristics
    /// </summary>
    public async Task<IssueAnalysis> AnalyzeIssueAsync(string issueId)
    {
        var existing = await GetAnalysisAsync(issueId);
        if (existing != null)
        {
            _logger.LogInformation("Using cached analysis for issue {IssueId}", issueId);
            return existing;
        }

        var issue = await _issueRepository.GetByIdAsync(issueId);
        if (issue == null)
            throw new InvalidOperationException($"Issue {issueId} not found");

        var isApiConfigured = OpenAiConfig.IsConfigured(_configuration);

        IssueAnalysis analysis;

        if (isApiConfigured)
        {
            try
            {
                _logger.LogInformation("Using OpenAI API to analyze issue {IssueId}", issueId);
                analysis = await CallOpenAIAsync(issue);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "OpenAI API failed for issue {IssueId}; falling back to heuristic analysis", issueId);
                analysis = GenerateHeuristicAnalysis(issue);
            }
        }
        else
        {
            _logger.LogInformation("OpenAI not configured (Enabled={Enabled}, KeySet={KeySet}); using heuristic analysis for issue {IssueId}",
                OpenAiConfig.IsEnabled(_configuration), !string.IsNullOrEmpty(OpenAiConfig.GetApiKey(_configuration)), issueId);
            analysis = GenerateHeuristicAnalysis(issue);
        }

        analysis.IssueId = issueId;

        // Duplicate detection is always computed locally (the model can't see the DB),
        // so it works identically with or without an OpenAI key.
        analysis.PotentialDuplicates = await FindPotentialDuplicatesAsync(issue);

        try
        {
            await _analysisRepository.InsertAsync(analysis);
            _logger.LogInformation("Analysis saved for issue {IssueId}", issueId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save analysis for issue {IssueId}", issueId);
        }

        return analysis;
    }

    /// <summary>
    /// Gets cached analysis for an issue
    /// </summary>
    public async Task<IssueAnalysis?> GetAnalysisAsync(string issueId)
    {
        var results = await _analysisRepository.FindAsync(a => a.IssueId == issueId);
        return results.FirstOrDefault();
    }

    /// <summary>
    /// Calls OpenAI API to analyze the issue
    /// </summary>
    private async Task<IssueAnalysis> CallOpenAIAsync(Issue issue)
    {
        var prompt = BuildAnalysisPrompt(issue);
        var model = OpenAiConfig.GetModel(_configuration);
        var apiKey = OpenAiConfig.GetApiKey(_configuration);

        if (!OpenAiConfig.IsUsableKey(apiKey))
        {
            throw new InvalidOperationException(
                "OpenAI API key is not properly configured. Set OpenAI:ApiKey (or the OPENAI_API_KEY env var) to a valid key.");
        }

        var request = new
        {
            model = model,
            messages = new[]
            {
                new { role = "system", content = "You are an expert urban problem analyst. Analyze citizen reports about city issues and provide structured insights." },
                new { role = "user", content = prompt }
            },
            temperature = 0.7,
            max_tokens = 500
        };

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, OpenAIApiUrl)
        {
            Content = JsonContent.Create(request)
        };

        httpRequest.Headers.Add("Authorization", $"Bearer {apiKey}");

        var timeout = OpenAiConfig.GetTimeoutSeconds(_configuration);
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(timeout));

        var response = await _httpClient.SendAsync(httpRequest, cts.Token);
        
        // Provide detailed error information for debugging
        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(cts.Token);
            var errorMessage = $"OpenAI API returned {(int)response.StatusCode}: {response.ReasonPhrase}";
            
            // Log first 200 chars of error for debugging
            if (!string.IsNullOrEmpty(errorBody))
            {
                try
                {
                    var errorDoc = JsonDocument.Parse(errorBody);
                    if (errorDoc.RootElement.TryGetProperty("error", out var error))
                    {
                        if (error.TryGetProperty("message", out var message))
                        {
                            errorMessage += $" - {message.GetString()}";
                        }
                    }
                }
                catch
                {
                    errorMessage += $" - {errorBody.Substring(0, Math.Min(200, errorBody.Length))}";
                }
            }
            
            _logger.LogError("OpenAI API error: {ErrorMessage}", errorMessage);
            throw new HttpRequestException($"{errorMessage}. Check your API key validity, account status, and rate limits.");
        }

        var responseBody = await response.Content.ReadAsStringAsync(cts.Token);
        var jsonDoc = JsonDocument.Parse(responseBody);
        
        var content = jsonDoc.RootElement
            .GetProperty("choices")[0]
            .GetProperty("message")
            .GetProperty("content")
            .GetString() ?? "";

        return ParseAnalysisResponse(content);
    }

    private IssueAnalysis ParseAnalysisResponse(string response)
    {
        var analysis = new IssueAnalysis
        {
            Category = IssueCategory.Other,
            ConfidenceScore = 75,
            Reasoning = response,
            EstimatedSeverity = 5,
            Keywords = new(),
            SuggestedTags = new(),
            PotentialDuplicates = new()
        };

        // Prefer real JSON parsing; fall back to the regex extractor only when the
        // model returned something that doesn't parse (e.g. prose with embedded JSON).
        if (TryParseAsJson(response, analysis))
        {
            return analysis;
        }

        ApplyField(ExtractJsonField(response, "category"), value =>
        {
            if (Enum.TryParse<IssueCategory>(value, out var c)) { analysis.Category = c; }
        });
        ApplyField(ExtractJsonField(response, "confidence"), value =>
        {
            if (int.TryParse(value, out var n)) { analysis.ConfidenceScore = Math.Clamp(n, 0, 100); }
        });
        ApplyField(ExtractJsonField(response, "severity"), value =>
        {
            if (int.TryParse(value, out var n)) { analysis.EstimatedSeverity = Math.Clamp(n, 1, 10); }
        });
        ApplyField(ExtractJsonField(response, "keywords"), value =>
            analysis.Keywords = SplitCsv(value));
        ApplyField(ExtractJsonField(response, "tags"), value =>
            analysis.SuggestedTags = SplitCsv(value));

        return analysis;
    }

    private bool TryParseAsJson(string response, IssueAnalysis analysis)
    {
        var trimmed = response?.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            return false;
        }

        // Strip Markdown code fences if the model wrapped the JSON in ```json ... ```.
        if (trimmed.StartsWith("```"))
        {
            var firstNewline = trimmed.IndexOf('\n');
            var lastFence = trimmed.LastIndexOf("```", StringComparison.Ordinal);
            if (firstNewline > 0 && lastFence > firstNewline)
            {
                trimmed = trimmed.Substring(firstNewline + 1, lastFence - firstNewline - 1).Trim();
            }
        }

        try
        {
            using var doc = JsonDocument.Parse(trimmed);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                return false;
            }

            if (TryGetString(root, "category", out var categoryStr)
                && Enum.TryParse<IssueCategory>(categoryStr, out var category))
            {
                analysis.Category = category;
            }
            if (TryGetInt(root, "confidence", out var confidence))
            {
                analysis.ConfidenceScore = Math.Clamp(confidence, 0, 100);
            }
            if (TryGetInt(root, "severity", out var severity))
            {
                analysis.EstimatedSeverity = Math.Clamp(severity, 1, 10);
            }
            if (TryGetString(root, "reasoning", out var reasoning) && !string.IsNullOrWhiteSpace(reasoning))
            {
                analysis.Reasoning = reasoning;
            }
            analysis.Keywords = ReadStringList(root, "keywords");
            analysis.SuggestedTags = ReadStringList(root, "tags");
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static bool TryGetString(JsonElement root, string name, out string value)
    {
        value = string.Empty;
        if (!root.TryGetProperty(name, out var prop)) { return false; }
        if (prop.ValueKind == JsonValueKind.String) { value = prop.GetString() ?? string.Empty; return true; }
        return false;
    }

    private static bool TryGetInt(JsonElement root, string name, out int value)
    {
        value = 0;
        if (!root.TryGetProperty(name, out var prop)) { return false; }
        if (prop.ValueKind == JsonValueKind.Number && prop.TryGetInt32(out value)) { return true; }
        if (prop.ValueKind == JsonValueKind.String && int.TryParse(prop.GetString(), out value)) { return true; }
        return false;
    }

    private static List<string> ReadStringList(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out var prop)) { return new List<string>(); }
        if (prop.ValueKind == JsonValueKind.Array)
        {
            return prop.EnumerateArray()
                .Select(e => e.ValueKind == JsonValueKind.String ? e.GetString() : e.ToString())
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Select(s => s!.Trim())
                .ToList();
        }
        if (prop.ValueKind == JsonValueKind.String)
        {
            return SplitCsv(prop.GetString() ?? string.Empty);
        }
        return new List<string>();
    }

    private static List<string> SplitCsv(string value) =>
        (value ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .ToList();

    private static void ApplyField(string? value, Action<string> apply)
    {
        if (!string.IsNullOrEmpty(value)) { apply(value); }
    }

    private string? ExtractJsonField(string json, string fieldName)
    {
        try
        {
            var pattern = $"\"{fieldName}\"\\s*:\\s*\"([^\"]*?)\"";
            var match = System.Text.RegularExpressions.Regex.Match(json, pattern, System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            return match.Success ? match.Groups[1].Value : null;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Builds the prompt for OpenAI analysis
    /// </summary>
    private string BuildAnalysisPrompt(Issue issue)
    {
        return $@"Analyze this citizen report about a city issue and provide a JSON response with these fields:
- category: One of [Infrastructure, PublicSafety, EnvironmentalHealth, Parks, Transportation, Utilities, Sanitation, PublicHealth, Other]
- confidence: 0-100 confidence in your categorization
- severity: 1-10 severity estimate (1=minor, 10=critical)
- reasoning: Brief explanation of your analysis
- keywords: Comma-separated key terms from the issue
- tags: Comma-separated suggested tags for organization

ISSUE TITLE: {issue.Title}
ISSUE DESCRIPTION: {issue.Description}
LOCATION: {issue.CityId}

Provide response as JSON format with these exact field names.";
    }

    /// <summary>
    /// Finds likely duplicate issues for <paramref name="issue"/> via a local
    /// text-similarity pass over open issues in the same city. Bounded and best-effort.
    /// </summary>
    private async Task<List<DuplicateMatch>> FindPotentialDuplicatesAsync(Issue issue)
    {
        if (string.IsNullOrWhiteSpace(issue.CityId))
        {
            return new List<DuplicateMatch>();
        }

        try
        {
            var candidates = await _issueRepository.QueryAsync(
                i => i.Id != issue.Id
                    && i.CityId == issue.CityId
                    && !i.IsDeleted
                    && i.Status != IssueStatus.Fixed
                    && i.Status != IssueStatus.Rejected
                    && i.Status != IssueStatus.Duplicate
                    && i.Status != IssueStatus.Archived,
                skip: 0,
                limit: 200);

            return DuplicateDetection.FindDuplicates(issue, candidates.Items);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Duplicate detection failed for issue {IssueId}", issue.Id);
            return new List<DuplicateMatch>();
        }
    }

    /// <summary>
    /// Generates heuristic analysis without OpenAI API, using the shared
    /// <see cref="IssueHeuristics"/> classifier so it agrees with the civic
    /// draft-suggestion fallback on identical input.
    /// </summary>
    private IssueAnalysis GenerateHeuristicAnalysis(Issue issue)
    {
        var result = IssueHeuristics.Classify(issue.Title, issue.Description);

        return new IssueAnalysis
        {
            Category = result.Category,
            ConfidenceScore = result.Confidence,
            Reasoning = "Automated keyword-based analysis (no AI key configured).",
            EstimatedSeverity = result.Severity,
            Keywords = result.Keywords.ToList(),
            SuggestedTags = result.Tags.ToList(),
            PotentialDuplicates = new()
        };
    }

    public Task<List<string>> SuggestTagsAsync(string title, string description)
    {
        var result = IssueHeuristics.Classify(title, description);
        return Task.FromResult(result.Tags.ToList());
    }
}
