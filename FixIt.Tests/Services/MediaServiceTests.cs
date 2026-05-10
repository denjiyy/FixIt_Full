using Xunit;
using Moq;
using CloudinaryDotNet;
using FixIt.Services;
using FixIt.Data.Repository.Contracts;
using FixIt.Models.Media;
using FixIt.Models.Enums;
using FixIt.Services.Storage;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace FixIt.Tests.Services;

public class MediaServiceTests
{
    private readonly Mock<IRepository<Media>> _mediaRepoMock;
    private readonly Mock<IRepository<MediaReference>> _mediaRefRepoMock;
    private readonly Mock<IFileStorage> _fileStorageMock;
    private readonly Mock<CloudinaryService> _cloudinaryServiceMock;
    private readonly IConfiguration _configuration;
    private readonly Mock<ILogger<MediaService>> _loggerMock;
    private readonly MediaService _mediaService;

    private readonly long _maxFileSizeBytes = 5 * 1024 * 1024; // 5MB
    private readonly long _maxVideoFileSizeBytes = 100 * 1024 * 1024; // 100MB

    public MediaServiceTests()
    {
        _mediaRepoMock = new Mock<IRepository<Media>>();
        _mediaRefRepoMock = new Mock<IRepository<MediaReference>>();
        _fileStorageMock = new Mock<IFileStorage>();
        _cloudinaryServiceMock = new Mock<CloudinaryService>(
            new Mock<Cloudinary>(new Account("test", "test", "test")).Object,
            new ConfigurationBuilder().Build(),
            new Mock<ILogger<CloudinaryService>>().Object
        );
        _loggerMock = new Mock<ILogger<MediaService>>();

        _configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Media:MaxFileSizeBytes"] = _maxFileSizeBytes.ToString(),
                ["Media:MaxVideoFileSizeBytes"] = _maxVideoFileSizeBytes.ToString(),
                ["Media:MaxFilesPerUpload"] = "10"
            })
            .Build();

        _mediaService = new MediaService(
            _mediaRepoMock.Object,
            _mediaRefRepoMock.Object,
            _fileStorageMock.Object,
            _cloudinaryServiceMock.Object,
            _configuration,
            _loggerMock.Object
        );
    }

    private IFormFile CreateMockFile(string fileName, long fileSize, string contentType = "image/jpeg")
    {
        var fileMock = new Mock<IFormFile>();
        var streamLength = (int)Math.Min(fileSize, 1024L);
        var streamMock = new MemoryStream(new byte[streamLength]);

        fileMock.Setup(f => f.FileName).Returns(fileName);
        fileMock.Setup(f => f.Length).Returns(fileSize);
        fileMock.Setup(f => f.ContentType).Returns(contentType);
        fileMock.Setup(f => f.OpenReadStream()).Returns(streamMock);

        return fileMock.Object;
    }

    [Theory]
    [InlineData("test.jpg", 5 * 1024 * 1024, "image/jpeg")]
    [InlineData("test.png", 2 * 1024 * 1024, "image/png")]
    [InlineData("test.jpeg", 3 * 1024 * 1024, "image/jpeg")]
    public async Task UploadFileAsync_WithValidImage_Succeeds(string fileName, long fileSize, string contentType)
    {
        // Arrange
        var file = CreateMockFile(fileName, fileSize, contentType);
        const string ownerId = "user1";
        const string referenceId = "issue1";

        _fileStorageMock.Setup(f => f.SaveFileAsync(It.IsAny<string>(), It.IsAny<Stream>()))
            .Returns(Task.CompletedTask);

        _mediaRepoMock.Setup(r => r.InsertAsync(It.IsAny<Media>()))
            .ReturnsAsync((Media m) => m);

        // Act
        var result = await _mediaService.UploadFileAsync(file, ownerId, MediaReferenceType.Issue, referenceId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(ownerId, result.OwnerId);
        _fileStorageMock.Verify(f => f.SaveFileAsync(It.IsAny<string>(), It.IsAny<Stream>()), Times.Once);
    }

    [Fact]
    public async Task UploadFileAsync_WithOversizedImage_ThrowsInvalidOperationException()
    {
        // Arrange
        var file = CreateMockFile("large.jpg", 6 * 1024 * 1024, "image/jpeg"); // 6MB > 5MB limit

        // Act & Assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _mediaService.UploadFileAsync(file, "user1", MediaReferenceType.Issue, "issue1"));
        Assert.Contains("exceeds maximum", ex.Message);
    }

    [Fact]
    public async Task UploadFileAsync_WithInvalidImageExtension_ThrowsInvalidOperationException()
    {
        // Arrange
        var file = CreateMockFile("image.gif", 1 * 1024 * 1024, "image/gif"); // .gif not allowed

        // Act & Assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _mediaService.UploadFileAsync(file, "user1", MediaReferenceType.Issue, "issue1"));
    }

    [Fact]
    public async Task UploadFileAsync_WithInvalidImageMimeType_ThrowsInvalidOperationException()
    {
        // Arrange
        var file = CreateMockFile("image.jpg", 1 * 1024 * 1024, "text/plain"); // Wrong MIME type

        // Act & Assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _mediaService.UploadFileAsync(file, "user1", MediaReferenceType.Issue, "issue1"));
    }

    [Theory]
    [InlineData("video.mp4", 50 * 1024 * 1024, "video/mp4")]
    [InlineData("video.webm", 75 * 1024 * 1024, "video/webm")]
    public async Task UploadFileAsync_WithValidVideo_Succeeds(string fileName, long fileSize, string contentType)
    {
        // Arrange
        var file = CreateMockFile(fileName, fileSize, contentType);
        const string ownerId = "user1";
        const string referenceId = "issue1";

        _fileStorageMock.Setup(f => f.SaveFileAsync(It.IsAny<string>(), It.IsAny<Stream>()))
            .Returns(Task.CompletedTask);

        _mediaRepoMock.Setup(r => r.InsertAsync(It.IsAny<Media>()))
            .ReturnsAsync((Media m) => m);

        // Act
        var result = await _mediaService.UploadFileAsync(file, ownerId, MediaReferenceType.Issue, referenceId);

        // Assert
        Assert.NotNull(result);
        _fileStorageMock.Verify(f => f.SaveFileAsync(It.IsAny<string>(), It.IsAny<Stream>()), Times.Once);
    }

    [Fact]
    public async Task UploadFileAsync_WithOversizedVideo_ThrowsInvalidOperationException()
    {
        // Arrange
        var file = CreateMockFile("video.mp4", 101 * 1024 * 1024, "video/mp4"); // 101MB > 100MB limit

        // Act & Assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _mediaService.UploadFileAsync(file, "user1", MediaReferenceType.Issue, "issue1"));
        Assert.Contains("exceeds maximum", ex.Message);
    }

    [Fact]
    public async Task UploadFileAsync_WithInvalidVideoExtension_ThrowsInvalidOperationException()
    {
        // Arrange
        var file = CreateMockFile("video.avi", 10 * 1024 * 1024, "video/avi"); // .avi not allowed

        // Act & Assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _mediaService.UploadFileAsync(file, "user1", MediaReferenceType.Issue, "issue1"));
    }

    [Fact]
    public async Task UploadFileAsync_WithEmptyFile_ThrowsInvalidOperationException()
    {
        // Arrange
        var file = CreateMockFile("empty.jpg", 0, "image/jpeg"); // Empty file

        // Act & Assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _mediaService.UploadFileAsync(file, "user1", MediaReferenceType.Issue, "issue1"));
    }

    [Fact]
    public async Task UploadFileAsync_WithNullFileName_ThrowsException()
    {
        // Arrange
        var fileMock = new Mock<IFormFile>();
        fileMock.Setup(f => f.FileName).Returns(string.Empty); // Empty filename instead of null
        fileMock.Setup(f => f.Length).Returns(1024);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _mediaService.UploadFileAsync(fileMock.Object, "user1", MediaReferenceType.Issue, "issue1"));
    }
}
