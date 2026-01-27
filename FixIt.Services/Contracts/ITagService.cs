using FixIt.Models.Issues;

namespace FixIt.Services.Contracts;

public interface ITagService
{
    Task<IEnumerable<Tag>> GetPopularTagsAsync(int limit = 20);
    
    Task<IEnumerable<Tag>> AutocompleteTagsAsync(string prefix, int limit = 10);
    
    Task<Tag> CreateTagAsync(string name, string? category = null, string? description = null);
    
    Task<Tag?> GetTagByNameAsync(string name);
    
    Task<Tag?> GetTagByIdAsync(string tagId);
    
    Task IncrementUsageCountAsync(string tagId);
    
    Task DecrementUsageCountAsync(string tagId);
    
    Task<IEnumerable<Tag>> GetAllTagsAsync(int page = 1, int pageSize = 50);
}