using System.Text;
using FixIt.Services.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace FixIt.Tests.Services;

public class LocalFileStorageTests : IDisposable
{
    private readonly string _testRoot;
    private readonly LocalFileStorage _storage;

    public LocalFileStorageTests()
    {
        _testRoot = Path.Combine(Path.GetTempPath(), $"fixit-local-storage-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testRoot);

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Media:StoragePath"] = _testRoot
            })
            .Build();

        _storage = new LocalFileStorage(configuration, Mock.Of<ILogger<LocalFileStorage>>());
    }

    [Fact]
    public async Task SaveFileAsync_WithTraversalPath_ThrowsUnauthorizedAccessException()
    {
        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes("test"));

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => _storage.SaveFileAsync("../outside.txt", stream));
    }

    [Fact]
    public async Task SaveFileAsync_WithValidPath_SavesAndCanBeRead()
    {
        const string relativePath = "uploads/2026/05/01/sample.txt";
        const string contents = "hello";

        await using var writeStream = new MemoryStream(Encoding.UTF8.GetBytes(contents));
        await _storage.SaveFileAsync(relativePath, writeStream);

        var exists = await _storage.FileExistsAsync(relativePath);
        Assert.True(exists);

        await using var readStream = await _storage.GetFileStreamAsync(relativePath);
        Assert.NotNull(readStream);

        using var reader = new StreamReader(readStream!);
        var loaded = await reader.ReadToEndAsync();
        Assert.Equal(contents, loaded);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testRoot))
        {
            Directory.Delete(_testRoot, recursive: true);
        }
    }
}
