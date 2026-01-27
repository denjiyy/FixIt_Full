using FixIt.Data.Repository.Contracts;
using FixIt.Models.Issues;
using FixIt.Services.Contracts;

namespace FixIt.Services;

public class TagService : ITagService
{
    private readonly IRepository<Tag> _tagRepo;

    public TagService(IRepository<Tag> tagRepo)
    {
        _tagRepo = tagRepo;
    }

    public async Task<IEnumerable<Tag>> GetPopularTagsAsync(int limit = 20)
    {
        if (limit < 1) limit = 20;
        if (limit > 100) limit = 100; // Max limit

        // Get all tags and sort by usage (necessary evil with current MongoDB setup)
        // In production, you'd want a database index on UsageCount and limit in MongoDB query
        var allTags = await _tagRepo.FindAsync(t => t.IsApproved);
        
        return allTags
            .OrderByDescending(t => t.UsageCount)
            .ThenByDescending(t => t.CreatedAt)
            .Take(limit)
            .ToList();
    }

    public async Task<IEnumerable<Tag>> AutocompleteTagsAsync(string prefix, int limit = 10)
    {
        if (string.IsNullOrWhiteSpace(prefix))
            return Enumerable.Empty<Tag>();

        var lowerPrefix = prefix.ToLowerInvariant().Trim();
        
        var allTags = await _tagRepo.FindAsync(t => 
            t.IsApproved && (
                t.Name.StartsWith(lowerPrefix) || 
                t.Aliases.Any(a => a.StartsWith(lowerPrefix))
            ));
        
        return allTags.Take(Math.Min(limit, 10)).ToList();
    }

    public async Task<Tag?> GetTagByNameAsync(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return null;

        var tags = await _tagRepo.FindAsync(t => t.Name == name.ToLowerInvariant());
        return tags.FirstOrDefault();
    }

    public async Task<Tag?> GetTagByIdAsync(string tagId)
    {
        return await _tagRepo.GetByIdAsync(tagId);
    }

    public async Task<Tag> CreateTagAsync(string name, string? category = null, string? description = null)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Tag name is required", nameof(name));

        if (name.Length > 50)
            throw new ArgumentException("Tag name must be 50 characters or less", nameof(name));

        var normalizedName = name.ToLowerInvariant().Trim();
        
        var existing = await GetTagByNameAsync(normalizedName);
        if (existing != null)
        {
            return existing;
        }

        var tag = new Tag
        {
            Name = normalizedName,
            Category = category?.Trim(),
            Description = description?.Trim(),
            IsApproved = true,
            UsageCount = 0,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        return await _tagRepo.InsertAsync(tag);
    }

    public async Task IncrementUsageCountAsync(string tagId)
    {
        var tag = await _tagRepo.GetByIdAsync(tagId);
        if (tag != null)
        {
            tag.UsageCount++;
            tag.UpdatedAt = DateTime.UtcNow;
            await _tagRepo.ReplaceAsync(tagId, tag);
        }
    }

    public async Task DecrementUsageCountAsync(string tagId)
    {
        var tag = await _tagRepo.GetByIdAsync(tagId);
        if (tag != null)
        {
            tag.UsageCount = Math.Max(0, tag.UsageCount - 1);
            tag.UpdatedAt = DateTime.UtcNow;
            await _tagRepo.ReplaceAsync(tagId, tag);
        }
    }

    public async Task<IEnumerable<Tag>> GetAllTagsAsync(int page = 1, int pageSize = 50)
    {
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 50;
        if (pageSize > 100) pageSize = 100;

        var skip = (page - 1) * pageSize;
        var result = await _tagRepo.QueryAsync(t => t.IsApproved, skip, pageSize);
        
        return result.Items.OrderByDescending(t => t.CreatedAt);
    }
}