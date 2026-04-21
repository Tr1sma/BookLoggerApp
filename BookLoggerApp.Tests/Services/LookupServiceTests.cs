using System.Net;
using FluentAssertions;
using BookLoggerApp.Infrastructure.Services;
using Xunit;

namespace BookLoggerApp.Tests.Services;

public class LookupServiceTests
{
    private readonly LookupService _service;

    public LookupServiceTests()
    {
        _service = new LookupService(new HttpClient());
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
        var isbn = "0000000000000";
        var emptyResponse = @"{ ""totalItems"": 0 }";

        var mockHandler = new MockHttpMessageHandler((request) =>
            new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent(emptyResponse)
            });

        var service = new LookupService(new HttpClient(mockHandler));

        // Act
        var result = await service.LookupByISBNAsync(isbn);

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
            var query = System.Web.HttpUtility.ParseQueryString(request.RequestUri!.Query);
            var isbnQuery = query["q"];
            isbnQuery.Should().NotBeNull();
            isbnQuery.Should().Contain(cleanIsbn);
            isbnQuery.Should().NotContain("-");

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

    [Fact]
    public async Task LookupByISBNAsync_WithQuotaErrorOnApiKey_ShouldRetryWithoutApiKey()
    {
        // Arrange
        var isbn = "9780140449136";
        var requestCount = 0;

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
            requestCount++;
            var query = System.Web.HttpUtility.ParseQueryString(request.RequestUri!.Query);
            var key = query["key"];

            if (requestCount == 1)
            {
                key.Should().Be("test-api-key");
                return new HttpResponseMessage(HttpStatusCode.Forbidden)
                {
                    Content = new StringContent(@"{""error"":{""message"":""Quota exceeded for quota metric 'Queries'""}}")
                };
            }

            key.Should().BeNull();
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(jsonResponse)
            };
        });

        var service = new LookupService(new HttpClient(mockHandler), googleBooksApiKey: "test-api-key");

        // Act
        var result = await service.LookupByISBNAsync(isbn);

        // Assert
        result.Should().NotBeNull();
        result!.Title.Should().Be("The Odyssey");
        requestCount.Should().Be(2);
    }

    [Fact]
    public async Task LookupByISBNAsync_AfterQuotaError_ShouldKeepUsingAnonymousRequests()
    {
        // Arrange
        var callKeys = new List<string?>();
        var requestCount = 0;

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
            requestCount++;
            var query = System.Web.HttpUtility.ParseQueryString(request.RequestUri!.Query);
            callKeys.Add(query["key"]);

            if (requestCount == 1)
            {
                return new HttpResponseMessage(HttpStatusCode.Forbidden)
                {
                    Content = new StringContent(@"{""error"":{""message"":""dailyLimitExceeded""}}")
                };
            }

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(jsonResponse)
            };
        });

        var service = new LookupService(new HttpClient(mockHandler), googleBooksApiKey: "test-api-key");

        // Act
        var firstLookup = await service.LookupByISBNAsync("9780140449136");
        var secondLookup = await service.LookupByISBNAsync("9780743273565");

        // Assert
        firstLookup.Should().NotBeNull();
        secondLookup.Should().NotBeNull();
        requestCount.Should().Be(3);
        callKeys.Should().Equal("test-api-key", null, null);
    }

    [Fact]
    public async Task LookupByISBNAsync_WithNonQuotaApiKeyError_ShouldNotRetryWithoutApiKey()
    {
        // Arrange
        var requestCount = 0;
        var mockHandler = new MockHttpMessageHandler((request) =>
        {
            requestCount++;
            var query = System.Web.HttpUtility.ParseQueryString(request.RequestUri!.Query);
            query["key"].Should().Be("test-api-key");

            return new HttpResponseMessage(HttpStatusCode.BadRequest)
            {
                Content = new StringContent(@"{""error"":{""message"":""Invalid ISBN query""}}")
            };
        });

        var service = new LookupService(new HttpClient(mockHandler), googleBooksApiKey: "test-api-key");

        // Act
        var action = async () => await service.LookupByISBNAsync("9780140449136");

        // Assert
        await action.Should().ThrowAsync<HttpRequestException>();
        requestCount.Should().Be(1);
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

    // ─────────────────────────────────────────────────────────────────────────
    // Coverage-ergänzende Tests (whitespace/null inputs, quota fallback,
    // ISBN_10, image URL normalization, PrintedPageCount fallback, 500 error)
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task LookupByISBNAsync_NullIsbn_ReturnsNull()
    {
        var result = await _service.LookupByISBNAsync(null!);

        result.Should().BeNull();
    }

    [Fact]
    public async Task LookupByISBNAsync_WhitespaceIsbn_ReturnsNull()
    {
        var result = await _service.LookupByISBNAsync("   ");

        result.Should().BeNull();
    }

    [Fact]
    public async Task LookupByISBNAsync_IsbnWithDashes_StripsThem()
    {
        string? capturedUrl = null;
        var mockHandler = new MockHttpMessageHandler((request) =>
        {
            capturedUrl = request.RequestUri!.AbsoluteUri;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(@"{""totalItems"": 0}")
            };
        });

        var service = new LookupService(new HttpClient(mockHandler));

        await service.LookupByISBNAsync("978-0-14-044913-6");

        capturedUrl.Should().Contain("9780140449136");
    }

    [Fact]
    public async Task LookupByISBNAsync_OnlyIsbn10InResponse_ExtractsIt()
    {
        var json = @"{
          ""items"": [
            {
              ""volumeInfo"": {
                ""title"": ""T"",
                ""authors"": [""A""],
                ""industryIdentifiers"": [
                  { ""type"": ""ISBN_10"", ""identifier"": ""0140449132"" }
                ]
              }
            }
          ]
        }";
        var mockHandler = new MockHttpMessageHandler((r) =>
            new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json) });
        var service = new LookupService(new HttpClient(mockHandler));

        var result = await service.LookupByISBNAsync("0140449132");

        result.Should().NotBeNull();
        result!.ISBN.Should().Be("0140449132");
    }

    [Fact]
    public async Task LookupByISBNAsync_HttpImage_ConvertsToHttps()
    {
        var json = @"{
          ""items"": [
            {
              ""volumeInfo"": {
                ""title"": ""T"",
                ""authors"": [""A""],
                ""imageLinks"": { ""thumbnail"": ""http://books.google.com/img.jpg"" }
              }
            }
          ]
        }";
        var mockHandler = new MockHttpMessageHandler((r) =>
            new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json) });
        var service = new LookupService(new HttpClient(mockHandler));

        var result = await service.LookupByISBNAsync("9780140449136");

        result!.CoverImageUrl.Should().StartWith("https://");
    }

    [Fact]
    public async Task LookupByISBNAsync_PublishedDate_ExtractsYear()
    {
        var json = @"{
          ""items"": [
            {
              ""volumeInfo"": {
                ""title"": ""T"",
                ""authors"": [""A""],
                ""publishedDate"": ""2015-05-10""
              }
            }
          ]
        }";
        var mockHandler = new MockHttpMessageHandler((r) =>
            new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json) });
        var service = new LookupService(new HttpClient(mockHandler));

        var result = await service.LookupByISBNAsync("9780140449136");

        result!.PublicationYear.Should().Be(2015);
    }

    [Fact]
    public async Task LookupByISBNAsync_InvalidPublishedDate_LeavesYearNull()
    {
        var json = @"{
          ""items"": [
            {
              ""volumeInfo"": {
                ""title"": ""T"",
                ""authors"": [""A""],
                ""publishedDate"": ""abc""
              }
            }
          ]
        }";
        var mockHandler = new MockHttpMessageHandler((r) =>
            new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json) });
        var service = new LookupService(new HttpClient(mockHandler));

        var result = await service.LookupByISBNAsync("9780140449136");

        result!.PublicationYear.Should().BeNull();
    }

    [Fact]
    public async Task LookupByISBNAsync_Http500_ThrowsHttpRequestException()
    {
        var mockHandler = new MockHttpMessageHandler((r) =>
            new HttpResponseMessage(HttpStatusCode.InternalServerError)
            {
                Content = new StringContent("server error")
            });
        var service = new LookupService(new HttpClient(mockHandler), googleBooksApiKey: "");

        Func<Task> act = async () => await service.LookupByISBNAsync("9780140449136");

        await act.Should().ThrowAsync<HttpRequestException>();
    }

    [Fact]
    public async Task LookupByISBNAsync_ForbiddenWithQuotaError_FallsBackToNoKeyAndSucceeds()
    {
        int callCount = 0;
        var successJson = @"{
          ""items"": [
            {
              ""volumeInfo"": {
                ""title"": ""Recovered"",
                ""authors"": [""A""]
              }
            }
          ]
        }";

        var mockHandler = new MockHttpMessageHandler((r) =>
        {
            callCount++;
            if (callCount == 1)
            {
                // First call with API key → quota exceeded
                return new HttpResponseMessage(HttpStatusCode.Forbidden)
                {
                    Content = new StringContent("{\"error\": {\"status\": \"RESOURCE_EXHAUSTED\"}}")
                };
            }
            // Retry without key succeeds
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(successJson)
            };
        });

        var service = new LookupService(new HttpClient(mockHandler), googleBooksApiKey: "test-key");

        var result = await service.LookupByISBNAsync("9780140449136");

        callCount.Should().Be(2);
        result.Should().NotBeNull();
        result!.Title.Should().Be("Recovered");
    }

    [Fact]
    public async Task SearchBooksAsync_NullQuery_ReturnsEmpty()
    {
        var result = await _service.SearchBooksAsync(null!);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task SearchBooksAsync_WhitespaceQuery_ReturnsEmpty()
    {
        var result = await _service.SearchBooksAsync("   ");

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task SearchBooksAsync_NoItemsInResult_ReturnsEmpty()
    {
        var mockHandler = new MockHttpMessageHandler((r) =>
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(@"{}")
            });
        var service = new LookupService(new HttpClient(mockHandler));

        var result = await service.SearchBooksAsync("xyz");

        result.Should().BeEmpty();
    }
}
