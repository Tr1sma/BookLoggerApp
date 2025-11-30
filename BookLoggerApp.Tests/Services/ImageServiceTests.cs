using BookLoggerApp.Core.Services.Abstractions;
using BookLoggerApp.Infrastructure.Services;
using FluentAssertions;
using Xunit;

namespace BookLoggerApp.Tests.Services;

public class ImageServiceTests : IDisposable
{
    private readonly ImageService _service;
    private readonly string _testImagePath;

    public ImageServiceTests()
    {
        IFileSystem fileSystem = new FileSystemAdapter();
        _service = new ImageService(fileSystem);

        // Create a test image file
        _testImagePath = Path.Combine(Path.GetTempPath(), "test_image.jpg");
        File.WriteAllBytes(_testImagePath, new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 }); // Minimal JPEG header
    }

    [Fact]
    public async Task SaveCoverImageAsync_ShouldSaveImageAndReturnPath()
    {
        // Arrange
        var bookId = Guid.NewGuid();
        using var imageStream = File.OpenRead(_testImagePath);

        // Act
        var result = await _service.SaveCoverImageAsync(imageStream, bookId);

        // Assert
        result.Should().NotBeNullOrEmpty();
        result.Should().Contain("covers");
        result.Should().Contain(bookId.ToString());

        // Verify the file was saved
        var savedPath = await _service.GetCoverImagePathAsync(bookId);
        savedPath.Should().NotBeNull();
        File.Exists(savedPath).Should().BeTrue();
    }

    [Fact]
    public async Task GetCoverImagePathAsync_WhenImageDoesNotExist_ShouldReturnNull()
    {
        // Arrange
        var nonExistentBookId = Guid.NewGuid();

        // Act
        var result = await _service.GetCoverImagePathAsync(nonExistentBookId);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task DeleteCoverImageAsync_ShouldDeleteImage()
    {
        // Arrange
        var bookId = Guid.NewGuid();
        using var imageStream = File.OpenRead(_testImagePath);
        await _service.SaveCoverImageAsync(imageStream, bookId);

        // Act
        await _service.DeleteCoverImageAsync(bookId);

        // Assert
        var savedPath = await _service.GetCoverImagePathAsync(bookId);
        savedPath.Should().BeNull();
    }

    [Fact]
    public async Task SaveCoverImageAsync_WithNullStream_ShouldThrowException()
    {
        // Arrange
        var bookId = Guid.NewGuid();

        // Act
        Func<Task> act = async () => await _service.SaveCoverImageAsync(null!, bookId);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>();
    }

    public void Dispose()
    {
        // Clean up test file
        if (File.Exists(_testImagePath))
        {
            File.Delete(_testImagePath);
        }
    }
}
