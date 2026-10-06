namespace HttpTests;

using Microsoft.Extensions.Logging;
using Pdsr.Http;
using Pdsr.Http.Extensions;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
public class TestPdsrClient : PdsrClientBase
{
    public TestPdsrClient(HttpClient client, ILoggerFactory loggerFactory)
        : base(client, loggerFactory)
    {
    }

    protected override Task SetBaseAddress(CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    protected override Task SetAuthorizationHeader(HttpRequestMessage request, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    // Implement or override anything needed for tests.
}



public class AddUrlWithTestPdsrClientTests
{
    private TestPdsrClient CreateClientWithPath(string? initialPath)
    {
        var loggerFactory = new LoggerFactory();
        var httpClient = new HttpClient();

        var client = new TestPdsrClient(httpClient, loggerFactory);
        client.RequestUrlPath = initialPath ?? "";
        return client;
    }

    [Fact]
    public void AddUrl_Appends_WhenPathIsEmpty()
    {
        var client = CreateClientWithPath(string.Empty);

        var result = client.AddUrl("endpoint");

        Assert.Equal("endpoint", result.RequestUrlPath);
    }

    [Fact]
    public void AddUrl_AppendsWithSlash_WhenPathMissingSlash()
    {
        var client = CreateClientWithPath("api");

        var result = client.AddUrl("endpoint");

        Assert.Equal("api/endpoint", result.RequestUrlPath);
    }

    [Fact]
    public void AddUrl_RespectsExistingSlash()
    {
        var client = CreateClientWithPath("api/");

        var result = client.AddUrl("endpoint");

        Assert.Equal("api/endpoint", result.RequestUrlPath);
    }

    [Fact]
    public void AddUrl_AppendsTrailingSlash_WhenMakeAbsoluteTrueAndEndsWithSlash()
    {
        var client = CreateClientWithPath("api/");

        var result = client.AddUrl("endpoint", makeAbsolute: true);

        Assert.Equal("api/endpoint/", result.RequestUrlPath);
    }

    [Fact]
    public void AddUrl_DoesNotAppendTrailingSlash_WhenPathDoesNotEndWithSlash()
    {
        var client = CreateClientWithPath("api");

        var result = client.AddUrl("endpoint", makeAbsolute: false);

        Assert.Equal("api/endpoint", result.RequestUrlPath);
    }

    [Fact]
    public void AddUrl_Works_WhenPathIsNull()
    {
        var client = CreateClientWithPath(null);

        var result = client.AddUrl("endpoint");

        Assert.Equal("endpoint", result.RequestUrlPath);
    }
}
