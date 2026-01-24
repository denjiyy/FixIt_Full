using System.Linq.Expressions;

namespace FixIt.Data.Repository.Contracts
{
    public interface IRepository<T> where T : class
    {
        Task<T?> GetByIdAsync(string id);
        Task<IEnumerable<T>> FindAsync(Expression<Func<T, bool>> predicate);
        Task<PagedResult<T>> QueryAsync(Expression<Func<T, bool>> filter, int skip = 0, int limit = 50);
        Task<T> InsertAsync(T entity);
        Task ReplaceAsync(string id, T entity);
        Task DeleteAsync(string id);
        Task<long> CountAsync(Expression<Func<T, bool>>? predicate = null);
    }

    public class PagedResult<T>
    {
        public IEnumerable<T> Items { get; set; } = Array.Empty<T>();
        public long Total { get; set; }
    }
}
