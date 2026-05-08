using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using FixIt.Data.Repository.Contracts;
using FixIt.Models.Locations;

namespace FixIt.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class GeocodingController : ControllerBase
    {
        private const int MaxCacheEntries = 5_000;
        private static readonly TimeSpan CacheEntryTtl = TimeSpan.FromHours(6);
        private static readonly HttpClient _httpClient = new HttpClient();
        private readonly ILogger<GeocodingController> _logger;
        private readonly IRepository<City> _cityRepo;

        // Cache for geocoding results storing both address and city ID
        private static readonly Dictionary<string, GeocodingCacheEntry> _geocodingCache = new();
        private static readonly SemaphoreSlim _cacheSemaphore = new(1, 1);

        public GeocodingController(ILogger<GeocodingController> logger, IRepository<City> cityRepo)
        {
            _logger = logger;
            _cityRepo = cityRepo;
        }

        /// <summary>
        /// Get address from coordinates using reverse geocoding (Nominatim)
        /// </summary>
        /// <param name="latitude">Latitude coordinate</param>
        /// <param name="longitude">Longitude coordinate</param>
        /// <returns>Address string or fallback to coordinates</returns>
        [HttpGet("reverse")]
        public async Task<ActionResult<ReverseGeocodeResponse>> ReverseGeocode(double latitude, double longitude)
        {
            try
            {
                // Validate coordinates
                if (latitude < -90 || latitude > 90 || longitude < -180 || longitude > 180)
                {
                    return BadRequest(new { message = "Invalid coordinates" });
                }

                // Create cache key (rounded to reduce cache misses for nearby clicks)
                string cacheKey = $"{latitude:F4},{longitude:F4}";

                // Check cache first
                await _cacheSemaphore.WaitAsync();
                try
                {
                    if (_geocodingCache.TryGetValue(cacheKey, out var cachedEntry))
                    {
                        if (DateTimeOffset.UtcNow - cachedEntry.CachedAtUtc > CacheEntryTtl)
                        {
                            _geocodingCache.Remove(cacheKey);
                        }
                        else
                        {
                            _logger.LogInformation($"Cache hit for {cacheKey}");
                            var cachedAddress = cachedEntry.Address;
                            var cachedCityId = cachedEntry.CityId;
                            var cachedCityName = cachedEntry.CityName;
                            return Ok(new ReverseGeocodeResponse
                            {
                                Address = cachedAddress,
                                CityName = cachedCityName,
                                CityId = cachedCityId,
                                IsCached = true,
                                Latitude = latitude,
                                Longitude = longitude
                            });
                        }
                    }
                }
                finally
                {
                    _cacheSemaphore.Release();
                }

                // Fetch from Nominatim API
                var (address, cityName) = await FetchAddressFromNominatim(latitude, longitude);
                
                // Try to match to a city in the database
                var (matchedCityId, matchedCityName) = await FindMatchingCity(cityName);

                // Cache the result with both address and city info
                await _cacheSemaphore.WaitAsync();
                try
                {
                    if (!_geocodingCache.ContainsKey(cacheKey))
                    {
                        EnforceCacheLimit();
                    }

                    _geocodingCache[cacheKey] = new GeocodingCacheEntry(
                        address,
                        matchedCityId,
                        matchedCityName,
                        DateTimeOffset.UtcNow);
                }
                finally
                {
                    _cacheSemaphore.Release();
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
                _logger.LogError(ex, "Error during reverse geocoding");
                // Return fallback with coordinates
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

                var request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.Add("User-Agent", "FixIt-HazardReporting-App/1.0");

                using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5)))
                {
                    var response = await _httpClient.SendAsync(request, cts.Token);

                    if (!response.IsSuccessStatusCode)
                    {
                        _logger.LogWarning($"Nominatim API returned status {response.StatusCode}");
                        throw new HttpRequestException($"Nominatim API error: {response.StatusCode}");
                    }

                    var content = await response.Content.ReadAsStringAsync();
                    _logger.LogInformation($"Nominatim raw response for {lat},{lng}: {content}");
                    
                    using (JsonDocument doc = JsonDocument.Parse(content))
                    {
                        var root = doc.RootElement;

                        if (root.TryGetProperty("address", out JsonElement addressElement))
                        {
                            string address = BuildAddressString(addressElement);
                            string city = ExtractCityFromAddress(addressElement);
                            
                            _logger.LogInformation($"Extracted - Address: {address}, City: {city}");
                            
                            return (address, city);
                        }

                        _logger.LogWarning("No address found in Nominatim response");
                        throw new Exception("No address in response");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching from Nominatim");
                throw;
            }
        }

        private string BuildAddressString(JsonElement addressObj)
        {
            var parts = new List<string>();

            // Helper function to safely get string value
            string GetAddressPart(string key)
            {
                return addressObj.TryGetProperty(key, out var elem) && elem.ValueKind == JsonValueKind.String
                    ? elem.GetString() ?? ""
                    : "";
            }

            // Add street address with house number
            string road = GetAddressPart("road");
            if (string.IsNullOrEmpty(road))
                road = GetAddressPart("street");

            if (!string.IsNullOrEmpty(road))
            {
                string houseNum = GetAddressPart("house_number");
                if (!string.IsNullOrEmpty(houseNum))
                    road += $" {houseNum}";
                parts.Add(road);
            }

            // Add suburb or neighbourhood
            string suburb = GetAddressPart("suburb");
            if (!string.IsNullOrEmpty(suburb) && !parts.Any(p => p.Contains(suburb)))
                parts.Add(suburb);
            else
            {
                string neighbourhood = GetAddressPart("neighbourhood");
                if (!string.IsNullOrEmpty(neighbourhood) && !parts.Any(p => p.Contains(neighbourhood)))
                    parts.Add(neighbourhood);
            }

            // Add postcode
            string postcode = GetAddressPart("postcode");
            if (!string.IsNullOrEmpty(postcode))
                parts.Add(postcode);

            // Add city/town/village/municipality
            string city = GetAddressPart("city");
            if (string.IsNullOrEmpty(city))
                city = GetAddressPart("town");
            if (string.IsNullOrEmpty(city))
                city = GetAddressPart("village");
            if (string.IsNullOrEmpty(city))
                city = GetAddressPart("municipality");

            if (!string.IsNullOrEmpty(city) && !parts.Any(p => p.Contains(city)))
                parts.Add(city);

            // Add state/province
            string state = GetAddressPart("state");
            if (!string.IsNullOrEmpty(state) && state != city && !parts.Any(p => p.Contains(state)))
                parts.Add(state);

            // Add country
            string country = GetAddressPart("country");
            if (!string.IsNullOrEmpty(country) && !parts.Any(p => p.Contains(country)))
                parts.Add(country);

            if (parts.Count == 0)
                return "Location selected";

            return string.Join(", ", parts);
        }

        private async Task<(string cityId, string cityName)> FindMatchingCity(string nominatimCity)
        {
            if (string.IsNullOrWhiteSpace(nominatimCity))
            {
                _logger.LogWarning("No city name provided to match");
                return ("", "");
            }

            try
            {
                // Get all cities from Bulgaria
                var bulgariaCities = await _cityRepo.FindAsync(c => c.Country == "Bulgaria");
                var cities = bulgariaCities.ToList();

                if (!cities.Any())
                {
                    _logger.LogWarning("No cities found in database for Bulgaria");
                    return ("", "");
                }

                var normalizedTarget = NormalizeForMatching(nominatimCity);
                _logger.LogInformation($"Searching for city: '{nominatimCity}' (normalized: '{normalizedTarget}')");

                // Try exact match first (case-insensitive, normalized)
                var exactMatch = cities.FirstOrDefault(c => 
                    NormalizeForMatching(c.Name) == normalizedTarget);
                if (exactMatch != null)
                {
                    _logger.LogInformation($"✓ Exact match found: {exactMatch.Name} (ID: {exactMatch.Id})");
                    return (exactMatch.Id, exactMatch.Name);
                }

                // Try partial match
                var partialMatch = cities.FirstOrDefault(c => 
                    NormalizeForMatching(c.Name).Contains(normalizedTarget) || 
                    normalizedTarget.Contains(NormalizeForMatching(c.Name)));
                if (partialMatch != null)
                {
                    _logger.LogInformation($"✓ Partial match found: {partialMatch.Name} (ID: {partialMatch.Id})");
                    return (partialMatch.Id, partialMatch.Name);
                }

                // Try word-level match
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
                        _logger.LogInformation($"✓ Word match found: {wordMatch.Name} (ID: {wordMatch.Id})");
                        return (wordMatch.Id, wordMatch.Name);
                    }
                }

                // Try fuzzy match - find closest match by Levenshtein distance
                var fuzzyMatch = cities
                    .Select(c => new 
                    { 
                        City = c, 
                        Distance = LevenshteinDistance(normalizedTarget, NormalizeForMatching(c.Name)) 
                    })
                    .Where(x => x.Distance <= 2) // Allow up to 2 character differences
                    .OrderBy(x => x.Distance)
                    .FirstOrDefault();

                if (fuzzyMatch != null)
                {
                    _logger.LogInformation($"✓ Fuzzy match found (distance: {fuzzyMatch.Distance}): {fuzzyMatch.City.Name} (ID: {fuzzyMatch.City.Id})");
                    return (fuzzyMatch.City.Id, fuzzyMatch.City.Name);
                }

                var availableCities = string.Join(", ", cities.Select(c => $"{c.Name} (normalized: {NormalizeForMatching(c.Name)})"));
                _logger.LogWarning($"✗ No city match found for: '{nominatimCity}' (normalized: '{normalizedTarget}'). Available cities: {availableCities}");
                return ("", "");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error matching city '{nominatimCity}'");
                return ("", "");
            }
        }

        private int LevenshteinDistance(string s1, string s2)
        {
            var len1 = s1.Length;
            var len2 = s2.Length;
            var d = new int[len1 + 1, len2 + 1];

            for (int i = 0; i <= len1; i++)
                d[i, 0] = i;

            for (int j = 0; j <= len2; j++)
                d[0, j] = j;

            for (int i = 1; i <= len1; i++)
            {
                for (int j = 1; j <= len2; j++)
                {
                    int cost = s1[i - 1] == s2[j - 1] ? 0 : 1;
                    d[i, j] = Math.Min(
                        Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1),
                        d[i - 1, j - 1] + cost);
                }
            }

            return d[len1, len2];
        }

        private string NormalizeForMatching(string text)
        {
            // Convert Cyrillic to Latin equivalents
            var cyrillicToLatin = new Dictionary<string, string>
            {
                // Bulgarian Cyrillic alphabet
                {"а", "a"}, {"б", "b"}, {"в", "v"}, {"г", "g"}, {"д", "d"},
                {"е", "e"}, {"ё", "yo"}, {"ж", "zh"}, {"з", "z"}, {"и", "i"},
                {"й", "y"}, {"к", "k"}, {"л", "l"}, {"м", "m"}, {"н", "n"},
                {"о", "o"}, {"п", "p"}, {"р", "r"}, {"с", "s"}, {"т", "t"},
                {"у", "u"}, {"ф", "f"}, {"х", "h"}, {"ц", "ts"}, {"ч", "ch"},
                {"ш", "sh"}, {"щ", "sht"}, {"ъ", "a"}, {"ы", "y"}, {"ь", ""}, 
                {"э", "e"}, {"ю", "yu"}, {"я", "ya"},
                
                // Uppercase
                {"А", "A"}, {"Б", "B"}, {"В", "V"}, {"Г", "G"}, {"Д", "D"},
                {"Е", "E"}, {"Ё", "Yo"}, {"Ж", "Zh"}, {"З", "Z"}, {"И", "I"},
                {"Й", "Y"}, {"К", "K"}, {"Л", "L"}, {"М", "M"}, {"Н", "N"},
                {"О", "O"}, {"П", "P"}, {"Р", "R"}, {"С", "S"}, {"Т", "T"},
                {"У", "U"}, {"Ф", "F"}, {"Х", "H"}, {"Ц", "Ts"}, {"Ч", "Ch"},
                {"Ш", "Sh"}, {"Щ", "Sht"}, {"Ъ", "A"}, {"Ы", "Y"}, {"Ь", ""},
                {"Э", "E"}, {"Ю", "Yu"}, {"Я", "Ya"}
            };

            var result = text;
            
            // Replace Cyrillic characters with Latin equivalents
            foreach (var pair in cyrillicToLatin)
            {
                result = result.Replace(pair.Key, pair.Value);
            }

            // Normalize the result: lowercase, trim, remove extra spaces
            result = result.ToLowerInvariant()
                .Trim()
                .Replace("  ", " "); // Remove double spaces

            return result;
        }
        
        private string ExtractCityFromAddress(JsonElement addressObj)
        {
            // Try to extract city in order of priority
            if (addressObj.TryGetProperty("city", out var elem) && elem.ValueKind == JsonValueKind.String)
                return elem.GetString() ?? "";
            
            if (addressObj.TryGetProperty("town", out elem) && elem.ValueKind == JsonValueKind.String)
                return elem.GetString() ?? "";
            
            if (addressObj.TryGetProperty("village", out elem) && elem.ValueKind == JsonValueKind.String)
                return elem.GetString() ?? "";
            
            if (addressObj.TryGetProperty("municipality", out elem) && elem.ValueKind == JsonValueKind.String)
                return elem.GetString() ?? "";
            
            return "";
        }
        
        private string ExtractCityFromAddressString(string address)
        {
            // Extract city from formatted address string
            // Typical format: "Street, Neighborhood, PostalCode, City, Country"
            var parts = address.Split(',').Select(p => p.Trim()).ToList();
            
            // City is typically near the end but before country
            // Return the second-to-last part as it's usually the city before country
            if (parts.Count >= 2)
                return parts[parts.Count - 2];
            
            return "";
        }

        private static void EnforceCacheLimit()
        {
            while (_geocodingCache.Count >= MaxCacheEntries)
            {
                var oldestKey = _geocodingCache
                    .OrderBy(entry => entry.Value.CachedAtUtc)
                    .Select(entry => entry.Key)
                    .FirstOrDefault();
                if (string.IsNullOrEmpty(oldestKey))
                {
                    break;
                }

                _geocodingCache.Remove(oldestKey);
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
