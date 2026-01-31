using FixIt.Data.Repository.Contracts;
using FixIt.Models.AI;
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
    /// Analyzes a single issue using OpenAI
    /// </summary>
    public async Task<IssueAnalysis> AnalyzeIssueAsync(string issueId)
    {
        // Check if analysis already exists
        var existing = await GetAnalysisAsync(issueId);
        if (existing != null)
        {
            _logger.LogInformation($"Using cached analysis for issue {issueId}");
            return existing;
        }

        var issue = await _issueRepository.GetByIdAsync(issueId);
        if (issue == null)
            throw new InvalidOperationException($"Issue {issueId} not found");

        var apiKey = _configuration["OpenAI:ApiKey"];
        if (string.IsNullOrEmpty(apiKey) || apiKey.StartsWith("sk-YOUR"))
        {
            _logger.LogWarning("OpenAI API key not configured. Skipping analysis.");
            return new IssueAnalysis
            {
                IssueId = issueId,
                Category = IssueCategory.Other,
                ConfidenceScore = 0,
                Reasoning = "AI analysis not configured",
                EstimatedSeverity = 5
            };
        }

        try
        {
            var analysis = await CallOpenAIAsync(issue);
            analysis.IssueId = issueId;
            
            // Save analysis
            await _analysisRepository.InsertAsync(analysis);
            
            _logger.LogInformation($"Analyzed issue {issueId} as {analysis.Category} with {analysis.ConfidenceScore}% confidence");
            
            return analysis;
        }
        catch (Exception ex)
        {
            _logger.LogError($"Failed to analyze issue {issueId}: {ex.Message}");
            
            // Return fallback analysis
            return new IssueAnalysis
            {
                IssueId = issueId,
                Category = IssueCategory.Other,
                ConfidenceScore = 0,
                Reasoning = $"Analysis failed: {ex.Message}",
                EstimatedSeverity = 5
            };
        }
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
        var model = _configuration["OpenAI:Model"] ?? "gpt-4o-mini";
        var apiKey = _configuration["OpenAI:ApiKey"]!;

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

        var timeout = int.TryParse(_configuration["OpenAI:TimeoutSeconds"], out var seconds) ? seconds : 30;
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(timeout));

        var response = await _httpClient.SendAsync(httpRequest, cts.Token);
        response.EnsureSuccessStatusCode();

        var responseBody = await response.Content.ReadAsStringAsync(cts.Token);
        var jsonDoc = JsonDocument.Parse(responseBody);
        
        var content = jsonDoc.RootElement
            .GetProperty("choices")[0]
            .GetProperty("message")
            .GetProperty("content")
            .GetString() ?? "";

        return ParseAnalysisResponse(content);
    }

    /// <summary>
    /// Parses OpenAI response into structured analysis
    /// </summary>
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

        // Parse category
        var categoryMatch = ExtractJsonField(response, "category");
        if (!string.IsNullOrEmpty(categoryMatch) && Enum.TryParse<IssueCategory>(categoryMatch, out var category))
        {
            analysis.Category = category;
        }

        // Parse confidence
        if (int.TryParse(ExtractJsonField(response, "confidence"), out var confidence))
        {
            analysis.ConfidenceScore = Math.Min(100, Math.Max(0, confidence));
        }

        // Parse severity
        if (int.TryParse(ExtractJsonField(response, "severity"), out var severity))
        {
            analysis.EstimatedSeverity = Math.Min(10, Math.Max(1, severity));
        }

        // Extract keywords
        var keywordsStr = ExtractJsonField(response, "keywords");
        if (!string.IsNullOrEmpty(keywordsStr))
        {
            analysis.Keywords = keywordsStr.Split(',')
                .Select(k => k.Trim())
                .Where(k => !string.IsNullOrEmpty(k))
                .ToList();
        }

        // Extract tags
        var tagsStr = ExtractJsonField(response, "tags");
        if (!string.IsNullOrEmpty(tagsStr))
        {
            analysis.SuggestedTags = tagsStr.Split(',')
                .Select(t => t.Trim())
                .Where(t => !string.IsNullOrEmpty(t))
                .ToList();
        }

        return analysis;
    }

    /// <summary>
    /// Extracts a field value from JSON-formatted response
    /// </summary>
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
}
