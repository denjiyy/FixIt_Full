using FixIt.Models.Issues;

namespace FixIt.Services.Contracts;

public interface ITagService
{
    Task<IEnumerable<Tag>> GetPopularTagsAsync(int limit = 20);
    Task<IEnumerable<Tag>> AutocompleteTagsAsync(string prefix);
    Task<Tag> CreateTagAsync(string name, string? category = null, string? description = null);
    Task<Tag?> GetTagByNameAsync(string name);
}