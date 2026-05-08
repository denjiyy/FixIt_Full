using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using FixIt.Models.AI;
using FixIt.Models.Enums;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace FixIt.Services.AI;

public interface ICivicAiService
{
    Task<IssueDraftSuggestionResult> SuggestIssueDraftAsync(IssueDraftSuggestionInput input, CancellationToken cancellationToken = default);
    Task<AiTextResult> SummarizeIssueThreadAsync(IssueThreadSummaryInput input, CancellationToken cancellationToken = default);
    IAsyncEnumerable<AiStreamEvent> StreamIssueThreadSummaryAsync(IssueThreadSummaryInput input, CancellationToken cancellationToken = default);
    Task<AiTextResult> SummarizeReportAsync(ReportSummaryInput input, CancellationToken cancellationToken = default);
    IAsyncEnumerable<AiStreamEvent> StreamReportSummaryAsync(ReportSummaryInput input, CancellationToken cancellationToken = default);
    Task<AiTextResult> GenerateHazardInsightAsync(HazardInsightInput input, CancellationToken cancellationToken = default);
    IAsyncEnumerable<AiStreamEvent> StreamHazardInsightAsync(HazardInsightInput input, CancellationToken cancellationToken = default);
    Task<IssueFilterTranslationResult> TranslateIssueFilterAsync(IssueFilterTranslationInput input, CancellationToken cancellationToken = default);
}

public sealed class IssueDraftSuggestionInput
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string? CityId { get; set; }
    public byte[]? ImageBytes { get; set; }
    public string? ImageMimeType { get; set; }
}

public sealed class IssueDraftSuggestionResult
{
    public IssueCategory? Category { get; set; }
    public IssuePriority Priority { get; set; } = IssuePriority.Medium;
    public string Department { get; set; } = "General Services";
    public int Confidence { get; set; } = 60;
    public bool AiGenerated { get; set; }
    public bool FallbackUsed { get; set; }
}

public sealed class IssueThreadSummaryInput
{
    public string IssueId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public IReadOnlyCollection<string> Comments { get; set; } = Array.Empty<string>();
}

public sealed class ReportSummaryInput
{
    public string ReportId { get; set; } = string.Empty;
    public string TargetType { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public string? Details { get; set; }
    public string? TargetId { get; set; }
    public string? IssueTitle { get; set; }
    public string? IssueDescription { get; set; }
    public IReadOnlyCollection<string> IssueComments { get; set; } = Array.Empty<string>();
}

public sealed class HazardInsightInput
{
    public string? CityId { get; set; }
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public int TotalReports { get; set; }
    public int TotalConfirmations { get; set; }
    public DateTime? FromUtc { get; set; }
    public DateTime? ToUtc { get; set; }
    public IReadOnlyCollection<HazardTypeFrequencyInput> HazardTypes { get; set; } = Array.Empty<HazardTypeFrequencyInput>();
}

public sealed class HazardTypeFrequencyInput
{
    public string Type { get; set; } = string.Empty;
    public int Count { get; set; }
}

public sealed class IssueFilterTranslationInput
{
    public string Query { get; set; } = string.Empty;
}

public sealed class IssueFilterTranslationResult
{
    public string? SearchQuery { get; set; }
    public int? Status { get; set; }
    public int? Priority { get; set; }
    public string? Category { get; set; }
    public string? From { get; set; }
    public string? To { get; set; }
    public int Confidence { get; set; } = 60;
    public bool AiGenerated { get; set; }
    public bool FallbackUsed { get; set; }
}

public sealed class AiTextResult
{
    public string Content { get; set; } = string.Empty;
    public bool AiGenerated { get; set; }
    public bool FallbackUsed { get; set; }
}

public sealed class AiStreamEvent
{
    public string Type { get; set; } = "chunk";
    public string? Text { get; set; }
    public string? Content { get; set; }
    public bool AiGenerated { get; set; }
    public bool FallbackUsed { get; set; }
    public string? Message { get; set; }
}

public sealed class OpenAiCivicAiService : ICivicAiService
{
    private const string OpenAiApiUrl = "https://api.openai.com/v1/chat/completions";
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<OpenAiCivicAiService> _logger;

    public OpenAiCivicAiService(
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<OpenAiCivicAiService> logger)
    {
        _httpClient = httpClient;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<IssueDraftSuggestionResult> SuggestIssueDraftAsync(IssueDraftSuggestionInput input, CancellationToken cancellationToken = default)
    {
        var fallback = BuildDraftSuggestionFallback(input);
        var apiKey = _configuration["OpenAI:ApiKey"]?.Trim();
        if (!IsApiConfigured(apiKey))
        {
            fallback.FallbackUsed = true;
            return fallback;
        }

        try
        {
            var prompt = new StringBuilder();
            prompt.AppendLine("Classify this civic issue draft and return strict JSON with fields:");
            prompt.AppendLine("category, priority, department, confidence.");
            prompt.AppendLine("category must be one of: Infrastructure, PublicSafety, EnvironmentalHealth, Parks, Transportation, Utilities, Sanitation, PublicHealth, Other.");
            prompt.AppendLine("priority must be one of: Low, Medium, High, Critical.");
            prompt.AppendLine("department should be a short city department name.");
            prompt.AppendLine("confidence is integer 0-100.");
            prompt.AppendLine($"Title: {input.Title}");
            prompt.AppendLine($"Description: {input.Description}");
            if (!string.IsNullOrWhiteSpace(input.CityId))
            {
                prompt.AppendLine($"City: {input.CityId}");
            }

            var messages = new List<object>
            {
                new { role = "system", content = "You are a municipal operations classifier. Respond with JSON only." }
            };

            if (input.ImageBytes != null && input.ImageBytes.Length > 0 && IsImageMimeType(input.ImageMimeType))
            {
                var dataUrl = $"data:{input.ImageMimeType};base64,{Convert.ToBase64String(input.ImageBytes)}";
                messages.Add(new
                {
                    role = "user",
                    content = new object[]
                    {
                        new { type = "text", text = prompt.ToString() },
                        new { type = "image_url", image_url = new { url = dataUrl } }
                    }
                });
            }
            else
            {
                messages.Add(new { role = "user", content = prompt.ToString() });
            }

            var raw = await CompleteTextAsync(messages, maxTokens: 220, temperature: 0.2, jsonMode: true, cancellationToken);
            if (string.IsNullOrWhiteSpace(raw))
            {
                fallback.FallbackUsed = true;
                return fallback;
            }

            var parsed = ParseDraftSuggestion(raw, fallback);
            parsed.AiGenerated = true;
            parsed.FallbackUsed = false;
            return parsed;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Issue draft suggestion failed. Falling back to heuristic mode.");
            fallback.FallbackUsed = true;
            return fallback;
        }
    }

    public async Task<AiTextResult> SummarizeIssueThreadAsync(IssueThreadSummaryInput input, CancellationToken cancellationToken = default)
    {
        var fallback = BuildIssueSummaryFallback(input);
        var apiKey = _configuration["OpenAI:ApiKey"]?.Trim();
        if (!IsApiConfigured(apiKey))
        {
            return new AiTextResult { Content = fallback, AiGenerated = false, FallbackUsed = true };
        }

        try
        {
            var prompt = BuildIssueSummaryPrompt(input);
            var messages = new object[]
            {
                new { role = "system", content = "You summarize civic issue threads for municipal admins in plain language. Keep it to 2-3 sentences." },
                new { role = "user", content = prompt }
            };

            var text = await CompleteTextAsync(messages, maxTokens: 240, temperature: 0.25, jsonMode: false, cancellationToken);
            if (string.IsNullOrWhiteSpace(text))
            {
                return new AiTextResult { Content = fallback, AiGenerated = false, FallbackUsed = true };
            }

            return new AiTextResult
            {
                Content = NormalizeSummary(text),
                AiGenerated = true,
                FallbackUsed = false
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Issue summary generation failed. Falling back.");
            return new AiTextResult { Content = fallback, AiGenerated = false, FallbackUsed = true };
        }
    }

    public async IAsyncEnumerable<AiStreamEvent> StreamIssueThreadSummaryAsync(IssueThreadSummaryInput input, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var fallback = BuildIssueSummaryFallback(input);
        var prompt = BuildIssueSummaryPrompt(input);
        await foreach (var chunk in StreamOrFallbackAsync(
                           systemPrompt: "You summarize civic issue threads for municipal admins in plain language. Keep it to 2-3 sentences.",
                           userPrompt: prompt,
                           fallback: fallback,
                           cancellationToken: cancellationToken))
        {
            yield return chunk;
        }
    }

    public async Task<AiTextResult> SummarizeReportAsync(ReportSummaryInput input, CancellationToken cancellationToken = default)
    {
        var fallback = BuildReportSummaryFallback(input);
        var apiKey = _configuration["OpenAI:ApiKey"]?.Trim();
        if (!IsApiConfigured(apiKey))
        {
            return new AiTextResult { Content = fallback, AiGenerated = false, FallbackUsed = true };
        }

        try
        {
            var prompt = BuildReportSummaryPrompt(input);
            var messages = new object[]
            {
                new { role = "system", content = "You summarize moderation reports for city admins in 2-3 plain-language sentences." },
                new { role = "user", content = prompt }
            };

            var text = await CompleteTextAsync(messages, maxTokens: 240, temperature: 0.2, jsonMode: false, cancellationToken);
            if (string.IsNullOrWhiteSpace(text))
            {
                return new AiTextResult { Content = fallback, AiGenerated = false, FallbackUsed = true };
            }

            return new AiTextResult
            {
                Content = NormalizeSummary(text),
                AiGenerated = true,
                FallbackUsed = false
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Report summary generation failed. Falling back.");
            return new AiTextResult { Content = fallback, AiGenerated = false, FallbackUsed = true };
        }
    }

    public async IAsyncEnumerable<AiStreamEvent> StreamReportSummaryAsync(ReportSummaryInput input, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var fallback = BuildReportSummaryFallback(input);
        var prompt = BuildReportSummaryPrompt(input);
        await foreach (var chunk in StreamOrFallbackAsync(
                           systemPrompt: "You summarize moderation reports for city admins in 2-3 plain-language sentences.",
                           userPrompt: prompt,
                           fallback: fallback,
                           cancellationToken: cancellationToken))
        {
            yield return chunk;
        }
    }

    public async Task<AiTextResult> GenerateHazardInsightAsync(HazardInsightInput input, CancellationToken cancellationToken = default)
    {
        var fallback = BuildHazardInsightFallback(input);
        var apiKey = _configuration["OpenAI:ApiKey"]?.Trim();
        if (!IsApiConfigured(apiKey))
        {
            return new AiTextResult { Content = fallback, AiGenerated = false, FallbackUsed = true };
        }

        try
        {
            var prompt = BuildHazardInsightPrompt(input);
            var messages = new object[]
            {
                new { role = "system", content = "You are a transportation safety analyst. Produce one concise operational insight sentence, then one sentence with context." },
                new { role = "user", content = prompt }
            };

            var text = await CompleteTextAsync(messages, maxTokens: 200, temperature: 0.2, jsonMode: false, cancellationToken);
            if (string.IsNullOrWhiteSpace(text))
            {
                return new AiTextResult { Content = fallback, AiGenerated = false, FallbackUsed = true };
            }

            return new AiTextResult
            {
                Content = NormalizeSummary(text),
                AiGenerated = true,
                FallbackUsed = false
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Hazard insight generation failed. Falling back.");
            return new AiTextResult { Content = fallback, AiGenerated = false, FallbackUsed = true };
        }
    }

    public async IAsyncEnumerable<AiStreamEvent> StreamHazardInsightAsync(HazardInsightInput input, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var fallback = BuildHazardInsightFallback(input);
        var prompt = BuildHazardInsightPrompt(input);
        await foreach (var chunk in StreamOrFallbackAsync(
                           systemPrompt: "You are a transportation safety analyst. Produce one concise operational insight sentence, then one sentence with context.",
                           userPrompt: prompt,
                           fallback: fallback,
                           cancellationToken: cancellationToken))
        {
            yield return chunk;
        }
    }

    public async Task<IssueFilterTranslationResult> TranslateIssueFilterAsync(IssueFilterTranslationInput input, CancellationToken cancellationToken = default)
    {
        var fallback = BuildFilterTranslationFallback(input.Query);
        var apiKey = _configuration["OpenAI:ApiKey"]?.Trim();
        if (!IsApiConfigured(apiKey))
        {
            fallback.FallbackUsed = true;
            return fallback;
        }

        try
        {
            var messages = new object[]
            {
                new { role = "system", content = "Convert natural language civic issue search text into strict JSON for filters." },
                new
                {
                    role = "user",
                    content = $"""
                               Return strict JSON with fields:
                               searchQuery (string or null), status (int 0-3 or null), priority (int 0-3 or null),
                               category (one of Infrastructure, PublicSafety, EnvironmentalHealth, Parks, Transportation, Utilities, Sanitation, PublicHealth, Other, or null),
                               from (YYYY-MM-DD or null), to (YYYY-MM-DD or null), confidence (0-100).
                               Query: {input.Query}
                               """
                }
            };

            var raw = await CompleteTextAsync(messages, maxTokens: 220, temperature: 0.1, jsonMode: true, cancellationToken);
            if (string.IsNullOrWhiteSpace(raw))
            {
                fallback.FallbackUsed = true;
                return fallback;
            }

            var parsed = ParseFilterTranslation(raw, fallback);
            parsed.AiGenerated = true;
            parsed.FallbackUsed = false;
            return parsed;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Issue filter translation failed. Falling back.");
            fallback.FallbackUsed = true;
            return fallback;
        }
    }

    private async IAsyncEnumerable<AiStreamEvent> StreamOrFallbackAsync(
        string systemPrompt,
        string userPrompt,
        string fallback,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var apiKey = _configuration["OpenAI:ApiKey"]?.Trim();
        if (IsApiConfigured(apiKey))
        {
            var streamEvents = await TryStreamEventsAsync(systemPrompt, userPrompt, cancellationToken);
            if (streamEvents != null)
            {
                foreach (var @event in streamEvents)
                {
                    yield return @event;
                }
                yield break;
            }
        }

        yield return new AiStreamEvent
        {
            Type = "chunk",
            Text = fallback,
            AiGenerated = false,
            FallbackUsed = true
        };

        yield return new AiStreamEvent
        {
            Type = "complete",
            Content = fallback,
            AiGenerated = false,
            FallbackUsed = true
        };
    }

    private async Task<List<AiStreamEvent>?> TryStreamEventsAsync(string systemPrompt, string userPrompt, CancellationToken cancellationToken)
    {
        var full = new StringBuilder();
        var events = new List<AiStreamEvent>();

        try
        {
            await foreach (var chunk in StreamTextAsync(systemPrompt, userPrompt, cancellationToken))
            {
                if (string.IsNullOrWhiteSpace(chunk))
                {
                    continue;
                }

                full.Append(chunk);
                events.Add(new AiStreamEvent
                {
                    Type = "chunk",
                    Text = chunk,
                    AiGenerated = true,
                    FallbackUsed = false
                });
            }

            if (full.Length > 0)
            {
                events.Add(new AiStreamEvent
                {
                    Type = "complete",
                    Content = NormalizeSummary(full.ToString()),
                    AiGenerated = true,
                    FallbackUsed = false
                });
                return events;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Streaming AI response failed. Falling back to deterministic copy.");
            return new List<AiStreamEvent>
            {
                new()
                {
                    Type = "error",
                    Message = "AI stream failed. Showing fallback insight."
                }
            };
        }

        return null;
    }

    private async Task<string?> CompleteTextAsync(
        IEnumerable<object> messages,
        int maxTokens,
        double temperature,
        bool jsonMode,
        CancellationToken cancellationToken)
    {
        var apiKey = _configuration["OpenAI:ApiKey"]?.Trim();
        if (!IsApiConfigured(apiKey))
        {
            return null;
        }

        var request = new Dictionary<string, object?>
        {
            ["model"] = _configuration["OpenAI:Model"] ?? "gpt-4o-mini",
            ["messages"] = messages,
            ["temperature"] = temperature,
            ["max_tokens"] = maxTokens
        };

        if (jsonMode)
        {
            request["response_format"] = new { type = "json_object" };
        }

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, OpenAiApiUrl)
        {
            Content = JsonContent.Create(request)
        };

        httpRequest.Headers.Add("Authorization", $"Bearer {apiKey}");

        var timeout = int.TryParse(_configuration["OpenAI:TimeoutSeconds"], out var timeoutSeconds)
            ? timeoutSeconds
            : 30;
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(TimeSpan.FromSeconds(timeout));

        using var response = await _httpClient.SendAsync(httpRequest, cts.Token);
        if (!response.IsSuccessStatusCode)
        {
            var responseBody = await response.Content.ReadAsStringAsync(cts.Token);
            _logger.LogWarning("OpenAI completion failed with status {StatusCode}: {Body}", response.StatusCode, responseBody);
            return null;
        }

        var body = await response.Content.ReadAsStringAsync(cts.Token);
        using var json = JsonDocument.Parse(body);
        return json.RootElement
            .GetProperty("choices")[0]
            .GetProperty("message")
            .GetProperty("content")
            .GetString();
    }

    private async IAsyncEnumerable<string> StreamTextAsync(string systemPrompt, string userPrompt, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var apiKey = _configuration["OpenAI:ApiKey"]?.Trim();
        if (!IsApiConfigured(apiKey))
        {
            yield break;
        }

        var request = new
        {
            model = _configuration["OpenAI:Model"] ?? "gpt-4o-mini",
            messages = new object[]
            {
                new { role = "system", content = systemPrompt },
                new { role = "user", content = userPrompt }
            },
            temperature = 0.2,
            max_tokens = 240,
            stream = true
        };

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, OpenAiApiUrl)
        {
            Content = JsonContent.Create(request)
        };
        httpRequest.Headers.Add("Authorization", $"Bearer {apiKey}");

        var timeout = int.TryParse(_configuration["OpenAI:TimeoutSeconds"], out var timeoutSeconds)
            ? timeoutSeconds
            : 30;
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(TimeSpan.FromSeconds(timeout));

        using var response = await _httpClient.SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead, cts.Token);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cts.Token);
        using var reader = new StreamReader(stream);

        while (!reader.EndOfStream && !cts.IsCancellationRequested)
        {
            var line = await reader.ReadLineAsync();
            if (string.IsNullOrWhiteSpace(line) || !line.StartsWith("data:", StringComparison.Ordinal))
            {
                continue;
            }

            var payload = line["data:".Length..].Trim();
            if (payload == "[DONE]")
            {
                yield break;
            }

            var chunk = TryParseStreamChunk(payload);
            if (chunk != null)
            {
                yield return chunk;
            }
        }
    }

    private string? TryParseStreamChunk(string payload)
    {
        try
        {
            using var json = JsonDocument.Parse(payload);
            var root = json.RootElement;
            var choices = root.GetProperty("choices");
            if (choices.GetArrayLength() == 0)
            {
                return null;
            }

            var delta = choices[0].GetProperty("delta");
            if (!delta.TryGetProperty("content", out var contentNode))
            {
                return null;
            }

            return contentNode.GetString();
        }
        catch
        {
            // Ignore malformed stream chunks and continue reading.
            return null;
        }
    }

    private static IssueDraftSuggestionResult BuildDraftSuggestionFallback(IssueDraftSuggestionInput input)
    {
        var merged = $"{input.Title} {input.Description}".ToLowerInvariant();
        var category = IssueCategory.Other;
        var priority = IssuePriority.Medium;

        if (ContainsAny(merged, "pothole", "road", "sidewalk", "bridge", "asphalt"))
        {
            category = IssueCategory.Infrastructure;
            priority = IssuePriority.High;
        }
        else if (ContainsAny(merged, "crime", "unsafe", "assault", "accident", "danger"))
        {
            category = IssueCategory.PublicSafety;
            priority = IssuePriority.Critical;
        }
        else if (ContainsAny(merged, "trash", "garbage", "waste", "litter"))
        {
            category = IssueCategory.Sanitation;
        }
        else if (ContainsAny(merged, "water", "gas", "electric", "power", "sewage"))
        {
            category = IssueCategory.Utilities;
            priority = IssuePriority.High;
        }
        else if (ContainsAny(merged, "traffic", "parking", "bus", "transit", "intersection"))
        {
            category = IssueCategory.Transportation;
        }
        else if (ContainsAny(merged, "flood", "pollution", "odor", "contamination"))
        {
            category = IssueCategory.EnvironmentalHealth;
            priority = IssuePriority.High;
        }
        else if (ContainsAny(merged, "park", "playground", "tree", "green"))
        {
            category = IssueCategory.Parks;
        }

        if (ContainsAny(merged, "urgent", "immediate", "emergency", "critical"))
        {
            priority = IssuePriority.Critical;
        }

        return new IssueDraftSuggestionResult
        {
            Category = category,
            Priority = priority,
            Department = MapDepartment(category),
            Confidence = 62,
            AiGenerated = false,
            FallbackUsed = true
        };
    }

    private static IssueDraftSuggestionResult ParseDraftSuggestion(string raw, IssueDraftSuggestionResult fallback)
    {
        var jsonText = ExtractJsonObject(raw);
        if (string.IsNullOrWhiteSpace(jsonText))
        {
            return fallback;
        }

        try
        {
            using var json = JsonDocument.Parse(jsonText);
            var root = json.RootElement;

            var category = ParseIssueCategory(GetStringOrNull(root, "category")) ?? fallback.Category;
            var priority = ParsePriority(GetStringOrNull(root, "priority")) ?? fallback.Priority;
            var department = (GetStringOrNull(root, "department") ?? MapDepartment(category ?? IssueCategory.Other)).Trim();
            var confidence = ClampInt(GetIntOrNull(root, "confidence") ?? fallback.Confidence, 0, 100);

            return new IssueDraftSuggestionResult
            {
                Category = category,
                Priority = priority,
                Department = string.IsNullOrWhiteSpace(department) ? fallback.Department : department,
                Confidence = confidence,
                AiGenerated = true,
                FallbackUsed = false
            };
        }
        catch
        {
            return fallback;
        }
    }

    private static IssueFilterTranslationResult BuildFilterTranslationFallback(string query)
    {
        var result = new IssueFilterTranslationResult
        {
            SearchQuery = query?.Trim(),
            Confidence = 58,
            AiGenerated = false,
            FallbackUsed = true
        };

        var lowered = query?.ToLowerInvariant() ?? string.Empty;

        result.Status = ParseStatusFromKeywords(lowered);
        result.Priority = ParsePriorityFromKeywords(lowered);
        result.Category = ParseCategoryFromKeywords(lowered)?.ToString();

        var now = DateTime.UtcNow.Date;
        if (lowered.Contains("today", StringComparison.Ordinal))
        {
            result.From = now.ToString("yyyy-MM-dd");
            result.To = now.ToString("yyyy-MM-dd");
        }
        else if (lowered.Contains("yesterday", StringComparison.Ordinal))
        {
            var yesterday = now.AddDays(-1);
            result.From = yesterday.ToString("yyyy-MM-dd");
            result.To = yesterday.ToString("yyyy-MM-dd");
        }
        else if (lowered.Contains("last week", StringComparison.Ordinal))
        {
            result.From = now.AddDays(-7).ToString("yyyy-MM-dd");
            result.To = now.ToString("yyyy-MM-dd");
        }
        else if (lowered.Contains("last month", StringComparison.Ordinal))
        {
            result.From = now.AddDays(-30).ToString("yyyy-MM-dd");
            result.To = now.ToString("yyyy-MM-dd");
        }
        else
        {
            var daysMatch = Regex.Match(lowered, @"last\s+(\d{1,3})\s+days");
            if (daysMatch.Success && int.TryParse(daysMatch.Groups[1].Value, out var days))
            {
                days = Math.Clamp(days, 1, 365);
                result.From = now.AddDays(-days).ToString("yyyy-MM-dd");
                result.To = now.ToString("yyyy-MM-dd");
            }
        }

        return result;
    }

    private static IssueFilterTranslationResult ParseFilterTranslation(string raw, IssueFilterTranslationResult fallback)
    {
        var jsonText = ExtractJsonObject(raw);
        if (string.IsNullOrWhiteSpace(jsonText))
        {
            return fallback;
        }

        try
        {
            using var json = JsonDocument.Parse(jsonText);
            var root = json.RootElement;

            var status = GetIntOrNull(root, "status");
            var priority = GetIntOrNull(root, "priority");

            if (status == null)
            {
                status = ParseStatusFromKeywords(GetStringOrNull(root, "status") ?? string.Empty);
            }

            if (priority == null)
            {
                priority = ParsePriorityFromKeywords(GetStringOrNull(root, "priority") ?? string.Empty);
            }

            var category = GetStringOrNull(root, "category");
            var from = GetStringOrNull(root, "from");
            var to = GetStringOrNull(root, "to");
            var confidence = ClampInt(GetIntOrNull(root, "confidence") ?? fallback.Confidence, 0, 100);

            return new IssueFilterTranslationResult
            {
                SearchQuery = NormalizeNullableString(GetStringOrNull(root, "searchQuery")) ?? fallback.SearchQuery,
                Status = IsKnownStatus(status) ? status : fallback.Status,
                Priority = IsKnownPriority(priority) ? priority : fallback.Priority,
                Category = ParseIssueCategory(category)?.ToString() ?? fallback.Category,
                From = NormalizeDateString(from) ?? fallback.From,
                To = NormalizeDateString(to) ?? fallback.To,
                Confidence = confidence,
                AiGenerated = true,
                FallbackUsed = false
            };
        }
        catch
        {
            return fallback;
        }
    }

    private static int? ParseStatusFromKeywords(string lowered)
    {
        if (lowered.Contains("in progress", StringComparison.Ordinal) || lowered.Contains("ongoing", StringComparison.Ordinal))
        {
            return 2;
        }

        if (lowered.Contains("new", StringComparison.Ordinal))
        {
            return 0;
        }

        if (lowered.Contains("confirmed", StringComparison.Ordinal))
        {
            return 1;
        }

        if (lowered.Contains("fixed", StringComparison.Ordinal) || lowered.Contains("resolved", StringComparison.Ordinal))
        {
            return 3;
        }

        return null;
    }

    private static int? ParsePriorityFromKeywords(string lowered)
    {
        if (lowered.Contains("critical", StringComparison.Ordinal))
        {
            return 3;
        }

        if (lowered.Contains("high", StringComparison.Ordinal))
        {
            return 2;
        }

        if (lowered.Contains("medium", StringComparison.Ordinal))
        {
            return 1;
        }

        if (lowered.Contains("low", StringComparison.Ordinal))
        {
            return 0;
        }

        return null;
    }

    private static IssueCategory? ParseCategoryFromKeywords(string lowered)
    {
        if (ContainsAny(lowered, "road", "pothole", "infrastructure", "bridge", "sidewalk"))
        {
            return IssueCategory.Infrastructure;
        }

        if (ContainsAny(lowered, "safety", "crime", "danger", "accident"))
        {
            return IssueCategory.PublicSafety;
        }

        if (ContainsAny(lowered, "pollution", "environment", "flood", "contamination"))
        {
            return IssueCategory.EnvironmentalHealth;
        }

        if (ContainsAny(lowered, "park", "playground", "tree"))
        {
            return IssueCategory.Parks;
        }

        if (ContainsAny(lowered, "traffic", "transit", "parking", "bus"))
        {
            return IssueCategory.Transportation;
        }

        if (ContainsAny(lowered, "water", "power", "electric", "gas", "sewer"))
        {
            return IssueCategory.Utilities;
        }

        if (ContainsAny(lowered, "trash", "garbage", "sanitation", "litter"))
        {
            return IssueCategory.Sanitation;
        }

        if (ContainsAny(lowered, "health", "disease", "odor"))
        {
            return IssueCategory.PublicHealth;
        }

        return null;
    }

    private static string BuildIssueSummaryPrompt(IssueThreadSummaryInput input)
    {
        var comments = input.Comments.Take(8).ToList();
        var commentsBlock = comments.Count == 0
            ? "No comments yet."
            : string.Join(Environment.NewLine, comments.Select((comment, index) => $"{index + 1}. {comment}"));

        return $"""
                Summarize this issue thread for an admin operator.
                Keep the output to 2-3 sentences in plain language.
                Mention urgency and trend signals when present.

                Title: {input.Title}
                Description: {input.Description}
                Comments:
                {commentsBlock}
                """;
    }

    private static string BuildReportSummaryPrompt(ReportSummaryInput input)
    {
        var commentsBlock = input.IssueComments.Any()
            ? string.Join(Environment.NewLine, input.IssueComments.Take(8).Select((comment, index) => $"{index + 1}. {comment}"))
            : "No issue comments available.";

        return $"""
                Summarize this moderation report in 2-3 sentences for an admin.
                Focus on what happened, confidence signals, and whether action is urgent.

                Report reason: {input.Reason}
                Report details: {input.Details ?? "No details provided."}
                Target type: {input.TargetType}
                Target ID: {input.TargetId ?? "N/A"}
                Related issue title: {input.IssueTitle ?? "N/A"}
                Related issue description: {input.IssueDescription ?? "N/A"}
                Related issue comments:
                {commentsBlock}
                """;
    }

    private static string BuildHazardInsightPrompt(HazardInsightInput input)
    {
        var hazardBreakdown = input.HazardTypes.Any()
            ? string.Join(", ", input.HazardTypes.Select(h => $"{h.Type}: {h.Count}"))
            : "No hazard type breakdown available.";

        var from = input.FromUtc?.ToString("yyyy-MM-dd") ?? "unknown";
        var to = input.ToUtc?.ToString("yyyy-MM-dd") ?? "now";

        return $"""
                Generate a short operational hazard trend insight for a city safety map.
                Output 1-2 sentences only.
                Mention concentration pattern, dominant hazard types, and timeframe.

                Cluster center: ({input.Latitude:F5}, {input.Longitude:F5})
                Total reports: {input.TotalReports}
                Total confirmations: {input.TotalConfirmations}
                Timeframe: {from} to {to}
                Hazard type breakdown: {hazardBreakdown}
                """;
    }

    private static string BuildIssueSummaryFallback(IssueThreadSummaryInput input)
    {
        var description = TruncateSentence(input.Description, 180);
        var commentCount = input.Comments.Count;
        var communitySignal = commentCount > 0
            ? $"The thread includes {commentCount} community comment{(commentCount == 1 ? string.Empty : "s")} that reinforce local impact."
            : "No public comments were posted yet, so this report currently relies on the original description.";

        return $"{description} {communitySignal} Review priority based on issue scope and recent activity.";
    }

    private static string BuildReportSummaryFallback(ReportSummaryInput input)
    {
        var details = string.IsNullOrWhiteSpace(input.Details)
            ? "No additional details were provided."
            : TruncateSentence(input.Details!, 160);

        if (!string.IsNullOrWhiteSpace(input.IssueTitle))
        {
            return $"Report flagged `{input.Reason}` for issue \"{input.IssueTitle}\". {details} Related thread activity includes {input.IssueComments.Count} comment{(input.IssueComments.Count == 1 ? string.Empty : "s")}, which can help validate follow-up action.";
        }

        return $"Report flagged `{input.Reason}` on a {input.TargetType} target. {details} Review target history before applying moderation action.";
    }

    private static string BuildHazardInsightFallback(HazardInsightInput input)
    {
        var dominant = input.HazardTypes
            .OrderByDescending(h => h.Count)
            .FirstOrDefault();
        var typeLabel = dominant?.Type ?? "mixed hazards";
        var from = input.FromUtc?.ToString("MMM d") ?? "recent weeks";
        var to = input.ToUtc?.ToString("MMM d") ?? "today";

        return $"This cluster has {input.TotalReports} hazard report{(input.TotalReports == 1 ? string.Empty : "s")} between {from} and {to}, with {typeLabel} appearing most frequently. Confirmation volume is {input.TotalConfirmations}, indicating the area may need targeted field checks.";
    }

    private static bool IsKnownStatus(int? status) => status is >= 0 and <= 3;
    private static bool IsKnownPriority(int? priority) => priority is >= 0 and <= 3;

    private static string NormalizeSummary(string value)
    {
        return Regex.Replace(value.Trim(), @"\s+", " ");
    }

    private static string TruncateSentence(string value, int maxLength)
    {
        var clean = Regex.Replace(value.Trim(), @"\s+", " ");
        if (clean.Length <= maxLength)
        {
            return clean;
        }

        return $"{clean[..maxLength].TrimEnd()}...";
    }

    private static string? NormalizeDateString(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (DateTime.TryParse(value, out var parsed))
        {
            return parsed.ToString("yyyy-MM-dd");
        }

        return null;
    }

    private static string? NormalizeNullableString(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static string MapDepartment(IssueCategory category)
    {
        return category switch
        {
            IssueCategory.Infrastructure => "Public Works Department",
            IssueCategory.PublicSafety => "Public Safety Department",
            IssueCategory.EnvironmentalHealth => "Environmental Health Department",
            IssueCategory.Parks => "Parks and Recreation",
            IssueCategory.Transportation => "Transportation Department",
            IssueCategory.Utilities => "Utilities Department",
            IssueCategory.Sanitation => "Sanitation Department",
            IssueCategory.PublicHealth => "Public Health Department",
            _ => "General Services"
        };
    }

    private static bool ContainsAny(string value, params string[] terms)
    {
        foreach (var term in terms)
        {
            if (value.Contains(term, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsApiConfigured(string? apiKey)
    {
        return !string.IsNullOrWhiteSpace(apiKey)
            && apiKey.StartsWith("sk-", StringComparison.Ordinal)
            && !apiKey.Contains("YOUR", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsImageMimeType(string? mimeType)
    {
        return !string.IsNullOrWhiteSpace(mimeType)
               && mimeType.StartsWith("image/", StringComparison.OrdinalIgnoreCase);
    }

    private static string? ExtractJsonObject(string source)
    {
        if (string.IsNullOrWhiteSpace(source))
        {
            return null;
        }

        var trimmed = source.Trim();
        if (trimmed.StartsWith("{", StringComparison.Ordinal) && trimmed.EndsWith("}", StringComparison.Ordinal))
        {
            return trimmed;
        }

        var match = Regex.Match(trimmed, @"\{[\s\S]*\}");
        return match.Success ? match.Value : null;
    }

    private static IssueCategory? ParseIssueCategory(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return Enum.TryParse<IssueCategory>(value.Trim(), true, out var parsed)
            ? parsed
            : ParseCategoryFromKeywords(value.ToLowerInvariant());
    }

    private static IssuePriority? ParsePriority(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (Enum.TryParse<IssuePriority>(value.Trim(), true, out var parsed))
        {
            return parsed;
        }

        var lowered = value.ToLowerInvariant();
        return lowered switch
        {
            "0" => IssuePriority.Low,
            "1" => IssuePriority.Medium,
            "2" => IssuePriority.High,
            "3" => IssuePriority.Critical,
            _ => null
        };
    }

    private static int ClampInt(int value, int min, int max) => Math.Min(max, Math.Max(min, value));

    private static string? GetStringOrNull(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var value))
        {
            return null;
        }

        return value.ValueKind switch
        {
            JsonValueKind.String => value.GetString(),
            JsonValueKind.Number => value.GetRawText(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            _ => null
        };
    }

    private static int? GetIntOrNull(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var value))
        {
            return null;
        }

        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number))
        {
            return number;
        }

        if (value.ValueKind == JsonValueKind.String && int.TryParse(value.GetString(), out number))
        {
            return number;
        }

        return null;
    }
}
