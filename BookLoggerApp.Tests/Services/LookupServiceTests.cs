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
        
        var jsonResponse = @"{
          ""items"": [
            {
              ""volumeInfo"": {
                ""title"": ""The Odyssey"",
                ""authors"": [""Homer""],
                ""industryIdentifiers"": [
                  { ""type"": ""ISBN_13"", ""identifier"": ""9780140449136"" }
                ]
              }
            }
          ]
        }";

        var mockHandler = new MockHttpMessageHandler((request) =>
        {
            request.RequestUri!.ToString().Should().Contain(isbn);
            return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent(jsonResponse)
            };
        });

        var httpClient = new HttpClient(mockHandler);
        var service = new LookupService(httpClient);

        // Act
        var result = await service.LookupByISBNAsync(isbn);

        // Assert
        result.Should().NotBeNull();
        result!.Title.Should().Be("The Odyssey");
        result.Author.Should().Be("Homer");
        result.ISBN.Should().Be(isbn);
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
        var dashedIsbn = "978-0-14-044913-6";
        var cleanIsbn = "9780140449136";
        
        // Mock response
        var jsonResponse = @"{
          ""items"": [
            {
              ""volumeInfo"": {
                ""title"": ""The Odyssey"",
                ""authors"": [""Homer""]
              }
            }
          ]
        }";

        var mockHandler = new MockHttpMessageHandler((request) =>
        {
            // Verify that the service STRIPPED the dashes before calling the API
            request.RequestUri!.ToString().Should().Contain(cleanIsbn);
            request.RequestUri!.ToString().Should().NotContain("-");

            return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent(jsonResponse)
            };
        });

        var httpClient = new HttpClient(mockHandler);
        var service = new LookupService(httpClient);

        // Act
        var result = await service.LookupByISBNAsync(dashedIsbn);

        // Assert
        result.Should().NotBeNull();
        result!.Title.Should().Be("The Odyssey");
    }

    // Helper for mocking HttpClient
    private class MockHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _sendAsync;

        public MockHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> sendAsync)
        {
            _sendAsync = sendAsync;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(_sendAsync(request));
        }
    }

    [Fact]
    public async Task SearchBooksAsync_WithValidQuery_ShouldReturnResults()
    {
        // Arrange
        var query = "The Great Gatsby";
        
        var jsonResponse = @"{
          ""items"": [
            {
              ""volumeInfo"": {
                ""title"": ""The Great Gatsby"",
                ""authors"": [""F. Scott Fitzgerald""],
                ""industryIdentifiers"": [
                  { ""type"": ""ISBN_13"", ""identifier"": ""9780743273565"" }
                ]
              }
            }
          ]
        }";

        string? capturedUrl = null;
        var mockHandler = new MockHttpMessageHandler((request) =>
        {
            capturedUrl = request.RequestUri!.AbsoluteUri;
            return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent(jsonResponse)
            };
        });

        var httpClient = new HttpClient(mockHandler);
        var service = new LookupService(httpClient);

        // Act
        var result = await service.SearchBooksAsync(query);

        // Assert
        result.Should().NotBeEmpty();
        result.Should().HaveCountGreaterThan(0);
        result.First().Title.Should().Be("The Great Gatsby");
        
        capturedUrl.Should().NotBeNull();
        capturedUrl.Should().Contain("The%20Great%20Gatsby");
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
