using FixIt.Data.Repository;
using FixIt.Models.Issues;
using MongoDB.Driver;
using Moq;

namespace FixIt.Tests.Data;

public class RepositoryObjectIdValidationTests
{
    [Fact]
    public async Task GetByIdAsync_WithInvalidObjectId_ThrowsInvalidEntityIdException()
    {
        var collectionMock = new Mock<IMongoCollection<Issue>>();
        var databaseMock = new Mock<IMongoDatabase>();
        databaseMock
            .Setup(db => db.GetCollection<Issue>(It.IsAny<string>(), It.IsAny<MongoCollectionSettings>()))
            .Returns(collectionMock.Object);

        var repository = new Repository<Issue>(databaseMock.Object, "issues");

        await Assert.ThrowsAsync<InvalidEntityIdException>(() => repository.GetByIdAsync("not-an-object-id"));
    }
}
