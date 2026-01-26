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
        var allTags = await _tagRepo.FindAsync(t => true);
        return allTags
            .OrderByDescending(t => t.UsageCount)
            .Take(limit)
            .ToList();
    }

    public async Task<IEnumerable<Tag>> AutocompleteTagsAsync(string prefix)
    {
        var allTags = await _tagRepo.FindAsync(t => t.Name.StartsWith(prefix.ToLowerInvariant()));
        return allTags.Take(10).ToList();
    }

    public async Task<Tag?> GetTagByNameAsync(string name)
    {
        var tags = await _tagRepo.FindAsync(t => t.Name == name.ToLowerInvariant());
        return tags.FirstOrDefault();
    }

    public async Task<Tag> CreateTagAsync(string name, string? category = null, string? description = null)
    {
        var existing = await GetTagByNameAsync(name);
        if (existing != null)
        {
            return existing;
        }

        var tag = new Tag
        {
            Name = name.ToLowerInvariant(),
            Category = category,
            Description = description,
            IsApproved = true,
            UsageCount = 0,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        return await _tagRepo.InsertAsync(tag);
    }
}