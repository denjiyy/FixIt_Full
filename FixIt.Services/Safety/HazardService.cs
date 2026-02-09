using FixIt.Data.Repository.Contracts;
using FixIt.Models.Safety;
using FixIt.Models.Locations;
using MongoDB.Driver.GeoJsonObjectModel;

namespace FixIt.Services.Safety;

public interface IHazardService
{
    Task<Hazard> CreateHazardAsync(string cityId, HazardType type, HazardSeverity severity, 
        string title, string description, double latitude, double longitude, 
        string? address = null, string? userId = null, bool isAnonymous = false);
    
    Task<Hazard?> GetHazardAsync(string hazardId);
    
    Task<List<Hazard>> GetCityHazardsAsync(string cityId, bool includeResolved = false);
    
    Task<List<Hazard>> GetNearbyHazardsAsync(string cityId, double latitude, double longitude, 
        double radiusKm = 5.0);
    
    Task<List<Hazard>> GetActiveSafetyHazardsAsync(string cityId);
    
    Task<bool> ConfirmHazardAsync(string hazardId, string userId);
    
    Task<bool> ResolveHazardAsync(string hazardId, string userId, string? notes = null);
    
    Task<Dictionary<HazardType, int>> GetHazardBreakdownAsync(string cityId);
    
    Task<List<Hazard>> SearchHazardsAsync(string cityId, HazardType? type = null, 
        HazardSeverity? severity = null, int limit = 100);

    // New safety feature methods
    Task<List<Hazard>> GetHazardsByTypeAsync(string cityId, HazardType type);
    
    Task<List<Hazard>> GetHazardsBySeverityAsync(string cityId, HazardSeverity severity);
    
    Task<List<Hazard>> GetRecentHazardsAsync(string cityId, int hoursPast = 24);
    
    Task<int> GetUnconfirmedHazardsCountAsync(string cityId);
    
    Task<Dictionary<string, int>> GetHazardSeverityDistributionAsync(string cityId);
}

public class HazardService : IHazardService
{
    private readonly IRepository<Hazard> _hazardRepo;
    private readonly IRepository<City> _cityRepo;

    public HazardService(
        IRepository<Hazard> hazardRepo,
        IRepository<City> cityRepo)
    {
        _hazardRepo = hazardRepo;
        _cityRepo = cityRepo;
    }

    public async Task<Hazard> CreateHazardAsync(string cityId, HazardType type, 
        HazardSeverity severity, string title, string description, double latitude, 
        double longitude, string? address = null, string? userId = null, bool isAnonymous = false)
    {
        var hazard = new Hazard
        {
            Type = type,
            Severity = severity,
            Title = title,
            Description = description,
            Location = new GeoJsonPoint<GeoJson2DGeographicCoordinates>(
                new GeoJson2DGeographicCoordinates(longitude, latitude)),
            Address = address,
            CityId = cityId,
            ReportedByUserId = isAnonymous ? null : userId,
            IsAnonymous = isAnonymous,
            InternalUserId = userId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await _hazardRepo.InsertAsync(hazard);
        return hazard;
    }

    public async Task<Hazard?> GetHazardAsync(string hazardId)
    {
        return await _hazardRepo.GetByIdAsync(hazardId);
    }

    public async Task<List<Hazard>> GetCityHazardsAsync(string cityId, bool includeResolved = false)
    {
        var hazards = await _hazardRepo.FindAsync(h => 
            h.CityId == cityId && 
            (!h.IsResolved || includeResolved) &&
            (h.ExpiresAt == null || h.ExpiresAt > DateTime.UtcNow));

        return hazards.OrderByDescending(h => h.Confirmations)
                     .ThenByDescending(h => h.UpdatedAt)
                     .ToList();
    }

    public async Task<List<Hazard>> GetNearbyHazardsAsync(string cityId, double latitude, 
        double longitude, double radiusKm = 5.0)
    {
        var hazards = await GetCityHazardsAsync(cityId, includeResolved: false);
        
        // Filter by distance (approximate calculation)
        var radiusMeters = radiusKm * 1000;
        var nearby = hazards.Where(h =>
        {
            var distance = CalculateDistance(
                latitude, longitude,
                h.Location.Coordinates.Latitude, h.Location.Coordinates.Longitude);
            return distance <= radiusMeters;
        }).ToList();

        return nearby;
    }

    public async Task<List<Hazard>> GetActiveSafetyHazardsAsync(string cityId)
    {
        var hazards = await _hazardRepo.FindAsync(h =>
            h.CityId == cityId &&
            !h.IsResolved &&
            (h.ExpiresAt == null || h.ExpiresAt > DateTime.UtcNow) &&
            (h.Severity == HazardSeverity.High || h.Severity == HazardSeverity.Critical));

        return hazards.OrderByDescending(h => h.Severity)
                     .ThenByDescending(h => h.Confirmations)
                     .ToList();
    }

    public async Task<bool> ConfirmHazardAsync(string hazardId, string userId)
    {
        var hazard = await _hazardRepo.GetByIdAsync(hazardId);
        if (hazard == null || hazard.IsResolved)
            return false;

        if (!hazard.ConfirmedByUserIds.Contains(userId))
        {
            hazard.ConfirmedByUserIds.Add(userId);
            hazard.Confirmations++;
            hazard.UpdatedAt = DateTime.UtcNow;
            await _hazardRepo.ReplaceAsync(hazardId, hazard);
        }

        return true;
    }

    public async Task<bool> ResolveHazardAsync(string hazardId, string userId, string? notes = null)
    {
        var hazard = await _hazardRepo.GetByIdAsync(hazardId);
        if (hazard == null)
            return false;

        hazard.IsResolved = true;
        hazard.ResolvedAt = DateTime.UtcNow;
        hazard.ResolvedByUserId = userId;
        hazard.ResolutionNotes = notes;
        hazard.UpdatedAt = DateTime.UtcNow;

        await _hazardRepo.ReplaceAsync(hazardId, hazard);
        return true;
    }

    public async Task<Dictionary<HazardType, int>> GetHazardBreakdownAsync(string cityId)
    {
        var hazards = await GetCityHazardsAsync(cityId, includeResolved: false);
        
        return hazards
            .GroupBy(h => h.Type)
            .ToDictionary(g => g.Key, g => g.Count());
    }

    public async Task<List<Hazard>> SearchHazardsAsync(string cityId, HazardType? type = null,
        HazardSeverity? severity = null, int limit = 100)
    {
        var hazards = await _hazardRepo.FindAsync(h =>
            h.CityId == cityId &&
            !h.IsResolved &&
            (h.ExpiresAt == null || h.ExpiresAt > DateTime.UtcNow) &&
            (type == null || h.Type == type) &&
            (severity == null || h.Severity == severity));

        return hazards.OrderByDescending(h => h.UpdatedAt)
                     .Take(limit)
                     .ToList();
    }

    public async Task<List<Hazard>> GetHazardsByTypeAsync(string cityId, HazardType type)
    {
        var hazards = await _hazardRepo.FindAsync(h =>
            h.CityId == cityId &&
            h.Type == type &&
            !h.IsResolved &&
            (h.ExpiresAt == null || h.ExpiresAt > DateTime.UtcNow));

        return hazards.OrderByDescending(h => h.Confirmations)
                     .ThenByDescending(h => h.UpdatedAt)
                     .ToList();
    }

    public async Task<List<Hazard>> GetHazardsBySeverityAsync(string cityId, HazardSeverity severity)
    {
        var hazards = await _hazardRepo.FindAsync(h =>
            h.CityId == cityId &&
            h.Severity == severity &&
            !h.IsResolved &&
            (h.ExpiresAt == null || h.ExpiresAt > DateTime.UtcNow));

        return hazards.OrderByDescending(h => h.Confirmations)
                     .ThenByDescending(h => h.UpdatedAt)
                     .ToList();
    }

    public async Task<List<Hazard>> GetRecentHazardsAsync(string cityId, int hoursPast = 24)
    {
        var cutoffTime = DateTime.UtcNow.AddHours(-hoursPast);
        var hazards = await _hazardRepo.FindAsync(h =>
            h.CityId == cityId &&
            h.CreatedAt >= cutoffTime &&
            !h.IsResolved &&
            (h.ExpiresAt == null || h.ExpiresAt > DateTime.UtcNow));

        return hazards.OrderByDescending(h => h.CreatedAt).ToList();
    }

    public async Task<int> GetUnconfirmedHazardsCountAsync(string cityId)
    {
        var hazards = await _hazardRepo.FindAsync(h =>
            h.CityId == cityId &&
            h.Confirmations == 0 &&
            !h.IsResolved &&
            (h.ExpiresAt == null || h.ExpiresAt > DateTime.UtcNow));

        return hazards.Count();
    }

    public async Task<Dictionary<string, int>> GetHazardSeverityDistributionAsync(string cityId)
    {
        var hazards = await GetCityHazardsAsync(cityId, includeResolved: false);

        return new Dictionary<string, int>
        {
            { "Critical", hazards.Count(h => h.Severity == HazardSeverity.Critical) },
            { "High", hazards.Count(h => h.Severity == HazardSeverity.High) },
            { "Medium", hazards.Count(h => h.Severity == HazardSeverity.Medium) },
            { "Low", hazards.Count(h => h.Severity == HazardSeverity.Low) }
        };
    }

    private static double CalculateDistance(double lat1, double lon1, double lat2, double lon2)
    {
        const double R = 6371000; // Earth radius in meters
        var dLat = (lat2 - lat1) * Math.PI / 180.0;
        var dLon = (lon2 - lon1) * Math.PI / 180.0;
        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                Math.Cos(lat1 * Math.PI / 180.0) * Math.Cos(lat2 * Math.PI / 180.0) *
                Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        return R * c;
    }
}
