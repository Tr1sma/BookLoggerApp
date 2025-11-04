using FluentAssertions;
using BookLoggerApp.Infrastructure.Services;
using Xunit;

namespace BookLoggerApp.Tests.Services;

public class LookupServiceTests
{
    private readonly LookupService _service;

    public LookupServiceTests()
    {
        _service = new LookupService();
    }

    [Fact]
    public async Task LookupByISBNAsync_WithValidISBN_ShouldReturnBookMetadata()
    {
        // Arrange
        var isbn = "9780140449136"; // The Odyssey by Homer

        // Act
        var result = await _service.LookupByISBNAsync(isbn);

        // Assert
        result.Should().NotBeNull();
        result!.Title.Should().NotBeNullOrEmpty();
        result.Author.Should().NotBeNullOrEmpty();
        result.ISBN.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task LookupByISBNAsync_WithInvalidISBN_ShouldReturnNull()
    {
        // Arrange
        var isbn = "0000000000000"; // Invalid ISBN

        // Act
        var result = await _service.LookupByISBNAsync(isbn);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task LookupByISBNAsync_WithEmptyISBN_ShouldReturnNull()
    {
        // Arrange
        var isbn = "";

        // Act
        var result = await _service.LookupByISBNAsync(isbn);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task LookupByISBNAsync_WithISBNContainingDashes_ShouldReturnBookMetadata()
    {
        // Arrange
        var isbn = "978-0-14-044913-6"; // The Odyssey with dashes

        // Act
        var result = await _service.LookupByISBNAsync(isbn);

        // Assert
        result.Should().NotBeNull();
        result!.Title.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task SearchBooksAsync_WithValidQuery_ShouldReturnResults()
    {
        // Arrange
        var query = "The Great Gatsby";

        // Act
        var result = await _service.SearchBooksAsync(query);

        // Assert
        result.Should().NotBeEmpty();
        result.Should().HaveCountGreaterThan(0);
    }

    [Fact]
    public async Task SearchBooksAsync_WithEmptyQuery_ShouldReturnEmptyList()
    {
        // Arrange
        var query = "";

        // Act
        var result = await _service.SearchBooksAsync(query);

        // Assert
        result.Should().BeEmpty();
    }
}
