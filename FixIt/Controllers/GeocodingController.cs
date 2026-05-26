using System.Collections.Concurrent;
using System.Text.Json;
using FixIt.Data.Repository.Contracts;
using FixIt.Models.Locations;
using Microsoft.AspNetCore.Mvc;

namespace FixIt.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class GeocodingController : ControllerBase
    {
        public const string NominatimHttpClientName = "Nominatim";

        private const int MaxCacheEntries = 5_000;
        private static readonly TimeSpan CacheEntryTtl = TimeSpan.FromHours(6);

        // ConcurrentDictionary for lock-free reads; the semaphore guards the
        // size-cap eviction pass on the write path.
        private static readonly ConcurrentDictionary<string, GeocodingCacheEntry> _geocodingCache = new();
        private static readonly SemaphoreSlim _evictionSemaphore = new(1, 1);

        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<GeocodingController> _logger;
        private readonly IRepository<City> _cityRepo;

        public GeocodingController(
            IHttpClientFactory httpClientFactory,
            ILogger<GeocodingController> logger,
            IRepository<City> cityRepo)
        {
            _httpClientFactory = httpClientFactory;
            _logger = logger;
            _cityRepo = cityRepo;
        }

        [HttpGet("reverse")]
        public async Task<ActionResult<ReverseGeocodeResponse>> ReverseGeocode(double latitude, double longitude)
        {
            try
            {
                if (latitude < -90 || latitude > 90 || longitude < -180 || longitude > 180)
                {
                    return BadRequest(new { message = "Invalid coordinates" });
                }

                var cacheKey = $"{latitude:F4},{longitude:F4}";

                if (_geocodingCache.TryGetValue(cacheKey, out var cachedEntry))
                {
                    if (DateTimeOffset.UtcNow - cachedEntry.CachedAtUtc > CacheEntryTtl)
                    {
                        _geocodingCache.TryRemove(cacheKey, out _);
                    }
                    else
                    {
                        _logger.LogInformation("Geocoding cache hit for {CacheKey}", cacheKey);
                        return Ok(new ReverseGeocodeResponse
                        {
                            Address = cachedEntry.Address,
                            CityName = cachedEntry.CityName,
                            CityId = cachedEntry.CityId,
                            IsCached = true,
                            Latitude = latitude,
                            Longitude = longitude
                        });
                    }
                }

                var (address, cityName) = await FetchAddressFromNominatim(latitude, longitude);
                var (matchedCityId, matchedCityName) = await FindMatchingCity(cityName);

                var entry = new GeocodingCacheEntry(
                    address,
                    matchedCityId,
                    matchedCityName,
                    DateTimeOffset.UtcNow);

                _geocodingCache[cacheKey] = entry;

                if (_geocodingCache.Count > MaxCacheEntries)
                {
                    await EnforceCacheLimitAsync();
                }

                return Ok(new ReverseGeocodeResponse
                {
                    Address = address,
                    CityName = matchedCityName,
                    CityId = matchedCityId,
                    IsCached = false,
                    Latitude = latitude,
                    Longitude = longitude
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during reverse geocoding for ({Lat},{Lng})", latitude, longitude);
                return Ok(new ReverseGeocodeResponse
                {
                    Address = $"Latitude: {latitude:F6}, Longitude: {longitude:F6}",
                    IsCached = false,
                    Latitude = latitude,
                    Longitude = longitude,
                    Success = false,
                    Message = "Could not fetch address, showing coordinates"
                });
            }
        }

        private async Task<(string address, string cityName)> FetchAddressFromNominatim(double lat, double lng)
        {
            try
            {
                var url = $"https://nominatim.openstreetmap.org/reverse?format=json&lat={lat}&lon={lng}&zoom=18&addressdetails=1";

                using var request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.Add("User-Agent", "FixIt-HazardReporting-App/1.0");

                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                var client = _httpClientFactory.CreateClient(NominatimHttpClientName);
                using var response = await client.SendAsync(request, cts.Token);

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Nominatim API returned status {Status}", response.StatusCode);
                    throw new HttpRequestException($"Nominatim API error: {response.StatusCode}");
                }

                var content = await response.Content.ReadAsStringAsync(cts.Token);
                _logger.LogInformation("Nominatim raw response for {Lat},{Lng}: {Content}", lat, lng, content);

                using var doc = JsonDocument.Parse(content);
                var root = doc.RootElement;
                if (root.TryGetProperty("address", out var addressElement))
                {
                    var address = BuildAddressString(addressElement);
                    var city = ExtractCityFromAddress(addressElement);
                    _logger.LogInformation("Geocoding extracted address {Address} for city {City}", address, city);
                    return (address, city);
                }

                _logger.LogWarning("No address found in Nominatim response");
                throw new InvalidOperationException("No address in response");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching from Nominatim for ({Lat},{Lng})", lat, lng);
                throw;
            }
        }

        private string BuildAddressString(JsonElement addressObj)
        {
            var parts = new List<string>();

            string GetAddressPart(string key) =>
                addressObj.TryGetProperty(key, out var elem) && elem.ValueKind == JsonValueKind.String
                    ? elem.GetString() ?? string.Empty
                    : string.Empty;

            var road = GetAddressPart("road");
            if (string.IsNullOrEmpty(road)) road = GetAddressPart("street");
            if (!string.IsNullOrEmpty(road))
            {
                var houseNum = GetAddressPart("house_number");
                if (!string.IsNullOrEmpty(houseNum)) road += $" {houseNum}";
                parts.Add(road);
            }

            var suburb = GetAddressPart("suburb");
            if (!string.IsNullOrEmpty(suburb) && !parts.Any(p => p.Contains(suburb)))
            {
                parts.Add(suburb);
            }
            else
            {
                var neighbourhood = GetAddressPart("neighbourhood");
                if (!string.IsNullOrEmpty(neighbourhood) && !parts.Any(p => p.Contains(neighbourhood)))
                {
                    parts.Add(neighbourhood);
                }
            }

            var postcode = GetAddressPart("postcode");
            if (!string.IsNullOrEmpty(postcode)) parts.Add(postcode);

            var city = GetAddressPart("city");
            if (string.IsNullOrEmpty(city)) city = GetAddressPart("town");
            if (string.IsNullOrEmpty(city)) city = GetAddressPart("village");
            if (string.IsNullOrEmpty(city)) city = GetAddressPart("municipality");

            if (!string.IsNullOrEmpty(city) && !parts.Any(p => p.Contains(city)))
            {
                parts.Add(city);
            }

            var state = GetAddressPart("state");
            if (!string.IsNullOrEmpty(state) && !string.Equals(state, city, StringComparison.OrdinalIgnoreCase) && !parts.Any(p => p.Contains(state)))
            {
                parts.Add(state);
            }

            var country = GetAddressPart("country");
            if (!string.IsNullOrEmpty(country) && !parts.Any(p => p.Contains(country)))
            {
                parts.Add(country);
            }

            return parts.Count == 0 ? "Location selected" : string.Join(", ", parts);
        }

        private async Task<(string cityId, string cityName)> FindMatchingCity(string nominatimCity)
        {
            if (string.IsNullOrWhiteSpace(nominatimCity))
            {
                _logger.LogWarning("No city name provided to match");
                return (string.Empty, string.Empty);
            }

            try
            {
                var bulgariaCities = await _cityRepo.FindAsync(c => c.Country == "Bulgaria");
                var cities = bulgariaCities.ToList();

                if (cities.Count == 0)
                {
                    _logger.LogWarning("No cities found in database for Bulgaria");
                    return (string.Empty, string.Empty);
                }

                var normalizedTarget = NormalizeForMatching(nominatimCity);
                _logger.LogInformation("Searching for city {City} (normalized: {Normalized})", nominatimCity, normalizedTarget);

                var exactMatch = cities.FirstOrDefault(c =>
                    string.Equals(NormalizeForMatching(c.Name), normalizedTarget, StringComparison.Ordinal));
                if (exactMatch != null)
                {
                    _logger.LogInformation("Exact city match {City} ({Id})", exactMatch.Name, exactMatch.Id);
                    return (exactMatch.Id, exactMatch.Name);
                }

                var partialMatch = cities.FirstOrDefault(c =>
                    NormalizeForMatching(c.Name).Contains(normalizedTarget) ||
                    normalizedTarget.Contains(NormalizeForMatching(c.Name)));
                if (partialMatch != null)
                {
                    _logger.LogInformation("Partial city match {City} ({Id})", partialMatch.Name, partialMatch.Id);
                    return (partialMatch.Id, partialMatch.Name);
                }

                var nominatimWords = normalizedTarget.Split(new[] { ' ', '-' }, StringSplitOptions.RemoveEmptyEntries);
                if (nominatimWords.Length > 0)
                {
                    var wordMatch = cities.FirstOrDefault(c =>
                    {
                        var cityWords = NormalizeForMatching(c.Name).Split(new[] { ' ', '-' }, StringSplitOptions.RemoveEmptyEntries);
                        return nominatimWords.Any(nw => cityWords.Any(cw => cw == nw));
                    });
                    if (wordMatch != null)
                    {
                        _logger.LogInformation("Word-level city match {City} ({Id})", wordMatch.Name, wordMatch.Id);
                        return (wordMatch.Id, wordMatch.Name);
                    }
                }

                var fuzzyMatch = cities
                    .Select(c => new { City = c, Distance = LevenshteinDistance(normalizedTarget, NormalizeForMatching(c.Name)) })
                    .Where(x => x.Distance <= 2)
                    .OrderBy(x => x.Distance)
                    .FirstOrDefault();
                if (fuzzyMatch != null)
                {
                    _logger.LogInformation("Fuzzy city match {City} ({Id}) distance {Distance}", fuzzyMatch.City.Name, fuzzyMatch.City.Id, fuzzyMatch.Distance);
                    return (fuzzyMatch.City.Id, fuzzyMatch.City.Name);
                }

                _logger.LogWarning("No city match found for {City} (normalized: {Normalized})", nominatimCity, normalizedTarget);
                return (string.Empty, string.Empty);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error matching city {City}", nominatimCity);
                return (string.Empty, string.Empty);
            }
        }

        private static int LevenshteinDistance(string s1, string s2)
        {
            var len1 = s1.Length;
            var len2 = s2.Length;
            var d = new int[len1 + 1, len2 + 1];

            for (var i = 0; i <= len1; i++) d[i, 0] = i;
            for (var j = 0; j <= len2; j++) d[0, j] = j;

            for (var i = 1; i <= len1; i++)
            {
                for (var j = 1; j <= len2; j++)
                {
                    var cost = s1[i - 1] == s2[j - 1] ? 0 : 1;
                    d[i, j] = Math.Min(
                        Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1),
                        d[i - 1, j - 1] + cost);
                }
            }

            return d[len1, len2];
        }

        private static readonly Dictionary<string, string> CyrillicToLatin = new()
        {
            {"а", "a"}, {"б", "b"}, {"в", "v"}, {"г", "g"}, {"д", "d"},
            {"е", "e"}, {"ё", "yo"}, {"ж", "zh"}, {"з", "z"}, {"и", "i"},
            {"й", "y"}, {"к", "k"}, {"л", "l"}, {"м", "m"}, {"н", "n"},
            {"о", "o"}, {"п", "p"}, {"р", "r"}, {"с", "s"}, {"т", "t"},
            {"у", "u"}, {"ф", "f"}, {"х", "h"}, {"ц", "ts"}, {"ч", "ch"},
            {"ш", "sh"}, {"щ", "sht"}, {"ъ", "a"}, {"ы", "y"}, {"ь", ""},
            {"э", "e"}, {"ю", "yu"}, {"я", "ya"},
            {"А", "A"}, {"Б", "B"}, {"В", "V"}, {"Г", "G"}, {"Д", "D"},
            {"Е", "E"}, {"Ё", "Yo"}, {"Ж", "Zh"}, {"З", "Z"}, {"И", "I"},
            {"Й", "Y"}, {"К", "K"}, {"Л", "L"}, {"М", "M"}, {"Н", "N"},
            {"О", "O"}, {"П", "P"}, {"Р", "R"}, {"С", "S"}, {"Т", "T"},
            {"У", "U"}, {"Ф", "F"}, {"Х", "H"}, {"Ц", "Ts"}, {"Ч", "Ch"},
            {"Ш", "Sh"}, {"Щ", "Sht"}, {"Ъ", "A"}, {"Ы", "Y"}, {"Ь", ""},
            {"Э", "E"}, {"Ю", "Yu"}, {"Я", "Ya"}
        };

        private static string NormalizeForMatching(string text)
        {
            var result = text;
            foreach (var pair in CyrillicToLatin)
            {
                result = result.Replace(pair.Key, pair.Value);
            }
            return result.ToLowerInvariant().Trim().Replace("  ", " ");
        }

        private static string ExtractCityFromAddress(JsonElement addressObj)
        {
            if (addressObj.TryGetProperty("city", out var elem) && elem.ValueKind == JsonValueKind.String)
                return elem.GetString() ?? string.Empty;
            if (addressObj.TryGetProperty("town", out elem) && elem.ValueKind == JsonValueKind.String)
                return elem.GetString() ?? string.Empty;
            if (addressObj.TryGetProperty("village", out elem) && elem.ValueKind == JsonValueKind.String)
                return elem.GetString() ?? string.Empty;
            if (addressObj.TryGetProperty("municipality", out elem) && elem.ValueKind == JsonValueKind.String)
                return elem.GetString() ?? string.Empty;
            return string.Empty;
        }

        private static async Task EnforceCacheLimitAsync()
        {
            await _evictionSemaphore.WaitAsync();
            try
            {
                while (_geocodingCache.Count > MaxCacheEntries)
                {
                    var oldest = _geocodingCache
                        .OrderBy(kvp => kvp.Value.CachedAtUtc)
                        .Select(kvp => kvp.Key)
                        .FirstOrDefault();
                    if (string.IsNullOrEmpty(oldest)) { break; }
                    _geocodingCache.TryRemove(oldest, out _);
                }
            }
            finally
            {
                _evictionSemaphore.Release();
            }
        }

        private sealed record GeocodingCacheEntry(
            string Address,
            string CityId,
            string CityName,
            DateTimeOffset CachedAtUtc);
    }

    public class ReverseGeocodeResponse
    {
        public string Address { get; set; } = string.Empty;
        public string? CityName { get; set; }
        public string? CityId { get; set; }
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public bool IsCached { get; set; }
        public bool Success { get; set; } = true;
        public string? Message { get; set; }
    }
}
