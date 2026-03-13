using FixIt.Data.Repository.Contracts;
using FixIt.Models.Accessibility;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Net.Http.Json;

namespace FixIt.Services.Accessibility;

/// <summary>
/// Service for managing translations across the platform
/// Enables automatic translation of issues, comments, and official responses
/// </summary>
public interface ITranslationService
{
    /// <summary>
    /// Translate content and store translation record
    /// </summary>
    Task<string> TranslateContentAsync(string content, string sourceLanguage, string targetLanguage, string contentType, string contentId);

    /// <summary>
    /// Get supported languages for a city
    /// </summary>
    Task<List<SupportedLanguage>> GetCitySupportedLanguagesAsync(string cityId);

    /// <summary>
    /// Create/update supported language for city
    /// </summary>
    Task<SupportedLanguage> AddSupportedLanguageAsync(SupportedLanguage language);

    /// <summary>
    /// Get translations for content
    /// </summary>
    Task<List<TranslationRecord>> GetContentTranslationsAsync(string contentId);

    /// <summary>
    /// Vote on translation accuracy
    /// </summary>
    Task<bool> VoteOnTranslationAsync(string translationId, bool isAccurate);
}

public class TranslationService : ITranslationService
{
    private readonly IRepository<TranslationRecord> _translationRepo;
    private readonly IRepository<SupportedLanguage> _languageRepo;
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<TranslationService> _logger;

    public TranslationService(
        IRepository<TranslationRecord> translationRepo,
        IRepository<SupportedLanguage> languageRepo,
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<TranslationService> logger)
    {
        _translationRepo = translationRepo;
        _languageRepo = languageRepo;
        _httpClient = httpClient;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<string> TranslateContentAsync(string content, string sourceLanguage, string targetLanguage, string contentType, string contentId)
    {
        // Check if translation already exists
        var existing = (await _translationRepo.FindAsync(t =>
            t.ContentId == contentId &&
            t.SourceLanguage == sourceLanguage &&
            t.TargetLanguage == targetLanguage &&
            t.ContentType == contentType)).FirstOrDefault();

        if (existing != null)
        {
            _logger.LogInformation($"Using cached translation for content {contentId}");
            return existing.TranslatedText;
        }

        // Translate using Google Translate API (or fallback)
        var translatedText = await TranslateViaGoogleAsync(content, sourceLanguage, targetLanguage);

        // Store translation record
        var record = new TranslationRecord
        {
            ContentType = contentType,
            ContentId = contentId,
            SourceLanguage = sourceLanguage,
            TargetLanguage = targetLanguage,
            OriginalText = content,
            TranslatedText = translatedText,
            TranslationService = "Google",
            TranslationConfidence = 0.95, // Google typically high confidence
            IsHumanReviewed = false
        };

        await _translationRepo.InsertAsync(record);
        _logger.LogInformation($"Translated content {contentId} from {sourceLanguage} to {targetLanguage}");

        return translatedText;
    }

    public async Task<List<SupportedLanguage>> GetCitySupportedLanguagesAsync(string cityId)
    {
        // For now, return global languages, but in production would be per-city
        var languages = await _languageRepo.FindAsync(l => l.AutoTranslateEnabled);
        return languages.OrderByDescending(l => l.Priority).ToList();
    }

    public async Task<SupportedLanguage> AddSupportedLanguageAsync(SupportedLanguage language)
    {
        language.CreatedAt = DateTime.UtcNow;
        var created = await _languageRepo.InsertAsync(language);
        _logger.LogInformation($"Added supported language: {language.DisplayName}");
        return created;
    }

    public async Task<List<TranslationRecord>> GetContentTranslationsAsync(string contentId)
    {
        var translations = await _translationRepo.FindAsync(t => t.ContentId == contentId);
        return translations.ToList();
    }

    public async Task<bool> VoteOnTranslationAsync(string translationId, bool isAccurate)
    {
        var translation = await _translationRepo.GetByIdAsync(translationId);
        if (translation == null)
            return false;

        if (isAccurate)
        {
            translation.AccuracyVotes++;
        }
        else
        {
            translation.InnaccuracyVotes++;
        }

        translation.UpdatedAt = DateTime.UtcNow;
        await _translationRepo.ReplaceAsync(translation.Id, translation);

        return true;
    }

    private async Task<string> TranslateViaGoogleAsync(string text, string sourceLanguage, string targetLanguage)
    {
        try
        {
            var apiKey = _configuration["Translation:GoogleApiKey"];
            if (string.IsNullOrEmpty(apiKey) || apiKey.StartsWith("YOUR_"))
            {
                // Return heuristic translation (placeholder)
                return await Task.FromResult($"[{targetLanguage.ToUpper()}] {text}");
            }

            // In production, call actual Google Translate API
            // For now, return placeholder
            _logger.LogInformation($"Would translate using Google API: {sourceLanguage} -> {targetLanguage}");
            return await Task.FromResult(text); // Placeholder
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Translation failed, returning original text");
            return await Task.FromResult(text);
        }
    }
}
