using MongoDB.Driver;
using FixIt.Data.Repository.Contracts;
using System.Linq.Expressions;
using MongoDB.Bson;

namespace FixIt.Data.Repository
{
    public class Repository<T> : IRepository<T> where T : class
    {
        private readonly IMongoCollection<T> _collection;

        public Repository(IMongoDatabase db, string? collectionName = null)
        {
            var finalCollectionName = collectionName ?? typeof(T).Name.ToLowerInvariant() + "s";
            _collection = db.GetCollection<T>(finalCollectionName);
        }

        public async Task<T?> GetByIdAsync(string id)
        {
            var filter = Builders<T>.Filter.Eq("_id", new ObjectId(id));
            return await _collection.Find(filter).FirstOrDefaultAsync();
        }

        public async Task<IEnumerable<T>> FindAsync(Expression<Func<T, bool>> predicate)
        {
            return await _collection.Find(predicate).ToListAsync();
        }

        public async Task<PagedResult<T>> QueryAsync(Expression<Func<T, bool>> filter, int skip = 0, int limit = 50)
        {
            var find = _collection.Find(filter ?? (_ => true));
            var items = await find.Skip(skip).Limit(limit).ToListAsync();
            var total = await _collection.CountDocumentsAsync(filter ?? (_ => true));
            return new PagedResult<T> { Items = items, Total = total };
        }

        public async Task<T> InsertAsync(T entity)
        {
            await _collection.InsertOneAsync(entity);
            return entity;
        }

        public async Task ReplaceAsync(string id, T entity)
        {
            var filter = Builders<T>.Filter.Eq("_id", new ObjectId(id));
            await _collection.ReplaceOneAsync(filter, entity);
        }

        public async Task DeleteAsync(string id)
        {
            var filter = Builders<T>.Filter.Eq("_id", new ObjectId(id));
            await _collection.DeleteOneAsync(filter);
        }

        public async Task<long> CountAsync(Expression<Func<T, bool>>? predicate = null)
        {
            return await _collection.CountDocumentsAsync(predicate ?? (_ => true));
        }
    }
}
