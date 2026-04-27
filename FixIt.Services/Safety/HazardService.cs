using FixIt.Data.Repository.Contracts;
using FixIt.Services.Gamification;
using FixIt.Models.Safety;
using FixIt.Models.Locations;
using FixIt.Models.Users;
using FixIt.Models.Enums;
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

    /// <summary>
    /// Update hazard fields (partial update)
    /// </summary>
    Task<Hazard> UpdateHazardAsync(string hazardId, HazardType? type = null, HazardSeverity? severity = null,
        string? title = null, string? description = null, double? latitude = null, double? longitude = null,
        string? address = null, DateTime? expiresAt = null);

    /// <summary>
    /// Soft delete a hazard - marks it as deleted but keeps data for recovery
    /// </summary>
    Task SoftDeleteHazardAsync(string hazardId, string? deletedByUserId = null);

    /// <summary>
    /// Restore a soft-deleted hazard
    /// </summary>
    Task RestoreHazardAsync(string hazardId);
}

public class HazardService : IHazardService
{
    private readonly IRepository<Hazard> _hazardRepo;
    private readonly IRepository<City> _cityRepo;
    private readonly IRepository<ApplicationUser> _userRepo;
    private readonly IReputationService _reputationService;

    public HazardService(
        IRepository<Hazard> hazardRepo,
        IRepository<City> cityRepo,
        IRepository<ApplicationUser> userRepo,
        IReputationService reputationService)
    {
        _hazardRepo = hazardRepo;
        _cityRepo = cityRepo;
        _userRepo = userRepo;
        _reputationService = reputationService;
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
        var hazard = await _hazardRepo.GetByIdAsync(hazardId);
        // Return null if hazard is deleted to treat it as not found
        return hazard?.IsDeleted == false ? hazard : null;
    }

    public async Task<List<Hazard>> GetCityHazardsAsync(string cityId, bool includeResolved = false)
    {
        var hazards = await _hazardRepo.FindAsync(h => 
            h.CityId == cityId && 
            !h.IsDeleted &&
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
            !h.IsDeleted &&
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
            
            // Award 10 points to the hazard reporter when first confirmed
            // Only award on first confirmation to avoid duplicate rewards
            if (hazard.Confirmations == 1 && !string.IsNullOrEmpty(hazard.ReportedByUserId))
            {
                await _reputationService.AddPointsAsync(
                    hazard.ReportedByUserId,
                    10,
                    "hazard_confirmed",
                    issueId: hazardId);
            }
        }

        return true;
    }

    public async Task<bool> ResolveHazardAsync(string hazardId, string userId, string? notes = null)
    {
        if (string.IsNullOrWhiteSpace(userId))
            throw new UnauthorizedAccessException("Authenticated admin user is required to resolve hazards.");

        var user = await _userRepo.GetByIdAsync(userId);
        if (user == null || user.Role != UserRole.Admin)
            throw new UnauthorizedAccessException("Only administrators can resolve hazards.");

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
            !h.IsDeleted &&
            !h.IsResolved &&
            (h.ExpiresAt == null || h.ExpiresAt > DateTime.UtcNow) &&
            (type == null || h.Type == type) &&
            (severity == null || h.Severity == severity));

        return hazards.OrderByDescending(h => h.UpdatedAt)
                     .Take(limit)
                     .ToList();
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

    /// <summary>
    /// Soft delete a hazard - marks it as deleted but keeps data for recovery
    /// </summary>
    public async Task SoftDeleteHazardAsync(string hazardId, string? deletedByUserId = null)
    {
        var hazard = await _hazardRepo.GetByIdAsync(hazardId);
        if (hazard == null)
            throw new KeyNotFoundException($"Hazard {hazardId} not found");

        hazard.IsDeleted = true;
        hazard.DeletedAt = DateTime.UtcNow;
        hazard.DeletedByUserId = deletedByUserId;
        hazard.UpdatedAt = DateTime.UtcNow;

        await _hazardRepo.ReplaceAsync(hazardId, hazard);
    }

    /// <summary>
    /// Restore a soft-deleted hazard
    /// </summary>
    public async Task RestoreHazardAsync(string hazardId)
    {
        var hazard = await _hazardRepo.GetByIdAsync(hazardId);
        if (hazard == null)
            throw new KeyNotFoundException($"Hazard {hazardId} not found");

        hazard.IsDeleted = false;
        hazard.DeletedAt = null;
        hazard.DeletedByUserId = null;
        hazard.UpdatedAt = DateTime.UtcNow;

        await _hazardRepo.ReplaceAsync(hazardId, hazard);
    }

    public async Task<Hazard> UpdateHazardAsync(string hazardId, HazardType? type = null, HazardSeverity? severity = null,
        string? title = null, string? description = null, double? latitude = null, double? longitude = null,
        string? address = null, DateTime? expiresAt = null)
    {
        var hazard = await _hazardRepo.GetByIdAsync(hazardId);
        if (hazard == null)
            throw new KeyNotFoundException($"Hazard {hazardId} not found");

        var updated = false;

        if (type != null && hazard.Type != type.Value)
        {
            hazard.Type = type.Value;
            updated = true;
        }

        if (severity != null && hazard.Severity != severity.Value)
        {
            hazard.Severity = severity.Value;
            updated = true;
        }

        if (!string.IsNullOrEmpty(title) && hazard.Title != title)
        {
            hazard.Title = title;
            updated = true;
        }

        if (!string.IsNullOrEmpty(description) && hazard.Description != description)
        {
            hazard.Description = description;
            updated = true;
        }

        if (latitude != null && longitude != null)
        {
            hazard.Location = new MongoDB.Driver.GeoJsonObjectModel.GeoJsonPoint<MongoDB.Driver.GeoJsonObjectModel.GeoJson2DGeographicCoordinates>(
                new MongoDB.Driver.GeoJsonObjectModel.GeoJson2DGeographicCoordinates(longitude.Value, latitude.Value));
            updated = true;
        }

        if (address != null && hazard.Address != address)
        {
            hazard.Address = address;
            updated = true;
        }

        if (expiresAt != null)
        {
            hazard.ExpiresAt = expiresAt;
            updated = true;
        }

        if (updated)
        {
            hazard.UpdatedAt = DateTime.UtcNow;
            hazard.Version += 1;
            await _hazardRepo.ReplaceAsync(hazardId, hazard);
        }

        return hazard;
    }
}
