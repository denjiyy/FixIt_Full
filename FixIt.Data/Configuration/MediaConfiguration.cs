using MongoDB.Driver;
using FixIt.Data.Configuration.Contracts;
using FixIt.Models.Media;

namespace FixIt.Data.Configuration;

public class MediaConfiguration : ICollectionConfigurator
{
    public async Task ConfigureAsync(IMongoDatabase db)
    {
        var media = db.GetCollection<Media>("media");

        // For finding all media owned by a user
        var ownerIndex = new CreateIndexModel<Media>(
            Builders<Media>.IndexKeys
                .Ascending(m => m.OwnerId)
                .Descending(m => m.CreatedAt),
            new CreateIndexOptions { Name = "idx_media_owner_created" }
        );
        await media.Indexes.CreateOneAsync(ownerIndex);

        var mediaRefs = db.GetCollection<MediaReference>("media_references");

        // For finding all places a media file is used
        var mediaIdIndex = new CreateIndexModel<MediaReference>(
            Builders<MediaReference>.IndexKeys.Ascending(r => r.MediaId),
            new CreateIndexOptions { Name = "ix_media_refs_mediaid" }
        );
        await mediaRefs.Indexes.CreateOneAsync(mediaIdIndex);

        // For finding all media attached to an entity
        var referenceIndex = new CreateIndexModel<MediaReference>(
            Builders<MediaReference>.IndexKeys
                .Ascending(r => r.ReferenceType)
                .Ascending(r => r.ReferenceId),
            new CreateIndexOptions { Name = "ix_media_refs_reference" }
        );
        await mediaRefs.Indexes.CreateOneAsync(referenceIndex);
    }
}