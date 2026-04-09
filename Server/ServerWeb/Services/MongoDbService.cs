using MongoDB.Bson;
using MongoDB.Driver;
using System.Linq.Expressions;

namespace ServerWeb.Services
{
    public static class MongoDbExtensions
    {
        // Find by ID
        public static async Task<T?> FindByIdAsync<T>(this IMongoCollection<T> collection, string id) where T : class
        {
            var objectId = ObjectId.Parse(id);
            var filter = Builders<T>.Filter.Eq("_id", objectId);
            return await collection.Find(filter).FirstOrDefaultAsync();
        }

        // Find all matching a filter
        public static async Task<List<T>> FindAsync<T>(
            this IMongoCollection<T> collection,
            Expression<Func<T, bool>> filter) where T : class
        {
            var mongoFilter = Builders<T>.Filter.Where(filter);
            return await collection.Find(mongoFilter).ToListAsync();
        }

        // Find with sorting
        public static async Task<List<T>> FindWithSortAsync<T>(
            this IMongoCollection<T> collection,
            Expression<Func<T, bool>>? filter = null,
            Expression<Func<T, object>>? orderBy = null,
            bool descending = false) where T : class
        {
            var mongoFilter = filter != null ? Builders<T>.Filter.Where(filter) : Builders<T>.Filter.Empty;
            var query = collection.Find(mongoFilter);

            if (orderBy != null)
            {
                var sortDef = descending
                    ? Builders<T>.Sort.Descending(orderBy)
                    : Builders<T>.Sort.Ascending(orderBy);
                query = query.Sort(sortDef);
            }

            return await query.ToListAsync();
        }

        // Count matching records
        public static async Task<long> CountAsync<T>(
            this IMongoCollection<T> collection,
            Expression<Func<T, bool>>? filter = null) where T : class
        {
            var mongoFilter = filter != null ? Builders<T>.Filter.Where(filter) : Builders<T>.Filter.Empty;
            return await collection.CountDocumentsAsync(mongoFilter);
        }

        // Check if any match
        public static async Task<bool> AnyAsync<T>(
            this IMongoCollection<T> collection,
            Expression<Func<T, bool>> filter) where T : class
        {
            var mongoFilter = Builders<T>.Filter.Where(filter);
            var count = await collection.CountDocumentsAsync(mongoFilter);
            return count > 0;
        }

        // First or default
        public static async Task<T?> FirstOrDefaultAsync<T>(
            this IMongoCollection<T> collection,
            Expression<Func<T, bool>> filter) where T : class
        {
            var mongoFilter = Builders<T>.Filter.Where(filter);
            return await collection.Find(mongoFilter).FirstOrDefaultAsync();
        }

        // Update a single document
        public static async Task<bool> UpdateAsync<T>(
            this IMongoCollection<T> collection,
            string id,
            T updatedDocument) where T : class
        {
            try
            {
                var objectId = ObjectId.Parse(id);
                var filter = Builders<T>.Filter.Eq("_id", objectId);
                var result = await collection.ReplaceOneAsync(filter, updatedDocument);
                return result.ModifiedCount > 0;
            }
            catch
            {
                return false;
            }
        }

        // Delete a single document
        public static async Task<bool> DeleteAsync<T>(
            this IMongoCollection<T> collection,
            string id) where T : class
        {
            try
            {
                var objectId = ObjectId.Parse(id);
                var filter = Builders<T>.Filter.Eq("_id", objectId);
                var result = await collection.DeleteOneAsync(filter);
                return result.DeletedCount > 0;
            }
            catch
            {
                return false;
            }
        }

        // Delete many matching filter
        public static async Task<long> DeleteManyAsync<T>(
            this IMongoCollection<T> collection,
            Expression<Func<T, bool>> filter) where T : class
        {
            var result = await collection.DeleteManyAsync(Builders<T>.Filter.Where(filter));
            return result.DeletedCount;
        }

        // Insert or replace (upsert)
        public static async Task<T> UpsertAsync<T>(
            this IMongoCollection<T> collection,
            string id,
            T document) where T : class
        {
            try
            {
                var objectId = ObjectId.Parse(id);
                var filter = Builders<T>.Filter.Eq("_id", objectId);
                var options = new ReplaceOptions { IsUpsert = true };
                await collection.ReplaceOneAsync(filter, document, options);
                return document;
            }
            catch
            {
                return document;
            }
        }
    }
}
