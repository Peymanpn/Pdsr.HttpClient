using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Pdsr.Http;
using Pdsr.Http.Extensions;
using RichardSzalay.MockHttp;
using System.Net;

namespace HttpTests;

public class RequestLifecycleTests
{
    private readonly TestablePdsrClient _client;
    private readonly MockHttpMessageHandler _mockHttp;
    private readonly ListLogger<PdsrClientBase> _logger;

    public RequestLifecycleTests()
    {
        _mockHttp = new MockHttpMessageHandler();
        _logger = new ListLogger<PdsrClientBase>();
        var loggerFactory = new LoggerFactory();
        loggerFactory.AddProvider(new ListLoggerProvider(_logger));
        _client = new TestablePdsrClient(new HttpClient(_mockHttp), loggerFactory);
    }

    public class Person
    {
        public string? FirstName { get; set; }
    }

    [Fact]
    public async Task FailedRequest_ClearsPerRequestState()
    {
        _mockHttp.When("http://example.com/*").Respond(HttpStatusCode.BadRequest);
        _client.Url("first").AddQueryString("k", "v").OnAnyResponse(_ => { }).EnsureSuccess();

        await Assert.ThrowsAsync<HttpRequestException>(() => _client.GetString());

        _client.RequestUrlPath.Should().BeEmpty();
        _client.QueryParameters.Should().BeEmpty();
        _client.EnsureSuccess.Should().BeFalse();
        _client.HandleStatusCodeBase.Should().BeNull();
        _client.ConfigHttpClient.Should().BeNull();
    }

    [Fact]
    public async Task ThrowingStatusHandler_ClearsPerRequestState()
    {
        _mockHttp.When("http://example.com/*").Respond(HttpStatusCode.NotFound);
        _client.Url("first").OnNotFound((_, _) => throw new InvalidOperationException("handler"));

        await Assert.ThrowsAsync<InvalidOperationException>(() => _client.GetString());

        _client.RequestUrlPath.Should().BeEmpty();
        _client.HandleStatusCodeBase.Should().BeNull();
    }

    [Fact]
    public async Task ExceptionHandlers_ReceiveNullResponse_AndExceptionIsRethrown()
    {
        _mockHttp.When("*").Throw(new HttpRequestException("boom"));
        HttpResponseMessage? seenResponse = new();
        Exception? seenException = null;
        _client.Url("x").OnException((r, ex) => { seenResponse = r; seenException = ex; });

        var exception = await Record.ExceptionAsync(() => _client.GetString());

        exception.Should().BeOfType<HttpRequestException>().Which.Message.Should().Be("boom");
        seenResponse.Should().BeNull();
        seenException.Should().BeSameAs(exception);
    }

    [Fact]
    public async Task CallerCancellation_DoesNotRunExceptionHandlers()
    {
        _mockHttp.When("*").Respond(() => Task.Delay(1000).ContinueWith(_ => new HttpResponseMessage(HttpStatusCode.OK)));
        bool handlerCalled = false;
        _client.Url("x").OnException((_, _) => handlerCalled = true);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => _client.GetString(cts.Token));

        handlerCalled.Should().BeFalse();
    }

    [Fact]
    public async Task SendAsync_DeserializesCamelCaseByDefault()
    {
        _mockHttp.When("*").Respond("application/json", """{"firstName":"Ada"}""");

        var person = await _client.Url("people/1").SendAsync<Person>();

        person!.FirstName.Should().Be("Ada");
    }

    [Fact]
    public async Task SnakeCase_AppliesToOneRequestOnly()
    {
        _mockHttp.When("*").Respond("application/json", """{"first_name":"Ada"}""");

        var snake = await _client.Url("people/1").SnakeCase().SendAsync<Person>();

        snake!.FirstName.Should().Be("Ada");
        _client.NamingStrategy.Should().Be(SerializationNamingStrategy.Camel);
    }

    [Fact]
    public async Task SendAsync_NonSuccess_ReturnsDefaultWithoutDeserializingErrorBody()
    {
        _mockHttp.When("*").Respond(HttpStatusCode.BadRequest, "application/json", """{"firstName":"not a person"}""");

        var person = await _client.Url("people/1").SendAsync<Person>();

        person.Should().BeNull();
    }

    [Fact]
    public async Task SendAsync_EmptyBody_ReturnsDefault()
    {
        _mockHttp.When("*").Respond(HttpStatusCode.NoContent);

        var person = await _client.Url("people/1").SendAsync<Person>();

        person.Should().BeNull();
    }

    [Fact]
    public async Task SendAsync_InvalidJson_ThrowsOnlyWithEnsureSuccess()
    {
        _mockHttp.When("*").Respond("application/json", "not json");

        (await _client.Url("people/1").SendAsync<Person>()).Should().BeNull();

        await Assert.ThrowsAnyAsync<System.Text.Json.JsonException>(() => _client.Url("people/1").EnsureSuccess().SendAsync<Person>());
        _client.EnsureSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task OnBadRequest_ReceivesTypedModel_UsingClientNaming()
    {
        _mockHttp.When("*").Respond(HttpStatusCode.BadRequest, "application/json", """{"first_name":"Ada"}""");
        Person? error = null;

        await _client.Url("people/1").SnakeCase()
            .OnBadRequest<TestablePdsrClient, Person>((e, _, _) => { error = e; return Task.CompletedTask; })
            .GetString();

        error!.FirstName.Should().Be("Ada");
    }

    [Fact]
    public async Task AbsoluteUrl_WithQueryString_IsSentAsIs()
    {
        _mockHttp.Expect("https://other.example/api?k=a%20b").Respond("text/plain", "ok");

        var result = await _client.Url("https://other.example/api").AddQueryString("k", "a b").GetString();

        result.Should().Be("ok");
        _mockHttp.VerifyNoOutstandingExpectation();
    }

    [Fact]
    public async Task Logs_DoNotContainAuthorizationHeader()
    {
        _mockHttp.When("*").Respond(HttpStatusCode.Unauthorized);
        _client.BearerToken = "secret-token";

        await _client.Url("x").GetString();

        _logger.Logs.Should().NotContain(l => l.Message.Contains("secret-token"));
    }

    [Fact]
    public async Task Retry_BudgetIsPerRequest()
    {
        int calls = 0;
        _mockHttp.When("*").Respond(_ => { calls++; return new HttpResponseMessage(HttpStatusCode.ServiceUnavailable); });
        _client.MaxRetries = 2;
        _client.RetryWhen = r => r.StatusCode == HttpStatusCode.ServiceUnavailable;

        await _client.Url("x").GetString();
        await _client.Url("x").GetString();

        calls.Should().Be(6); // 1 attempt + 2 retries, for each request
    }

    [Fact]
    public async Task Retry_ResendsBody_AndReappliesAuthorization()
    {
        var bodies = new List<string>();
        var tokens = new List<string?>();
        _mockHttp.When("*").Respond(async req =>
        {
            bodies.Add(await req.Content!.ReadAsStringAsync());
            tokens.Add(req.Headers.Authorization?.Parameter);
            return new HttpResponseMessage(bodies.Count == 1 ? HttpStatusCode.Unauthorized : HttpStatusCode.OK);
        });
        _client.BearerToken = "expired";
        _client.RetryWhen = r =>
        {
            if (r.StatusCode != HttpStatusCode.Unauthorized) return false;
            _client.BearerToken = "refreshed";
            return true;
        };

        var response = await _client.Url("x").Post(new { Name = "Ada" }).GetString();

        bodies.Should().Equal("""{"name":"Ada"}""", """{"name":"Ada"}""");
        tokens.Should().Equal("expired", "refreshed");
    }
}

public class UrlTests
{
    private static TestablePdsrClient CreateClient(string path) =>
        new(new HttpClient(), new LoggerFactory()) { RequestUrlPath = path };

    [Fact]
    public void Url_ReplacesByDefault()
    {
        CreateClient("api").Url("x").RequestUrlPath.Should().Be("x");
    }

    [Fact]
    public void Url_AppendsWhenAsked()
    {
        CreateClient("api").Url("x", append: true).RequestUrlPath.Should().Be("api/x");
    }

    [Fact]
    public void AddQueryString_AllowsEmptyValue()
    {
        CreateClient("api").AddQueryString("flag", "").QueryParameters["flag"].Should().BeEmpty();
    }
}

public class QueryStringTests
{
    [Theory]
    [InlineData("api", "http://example.com/api?a=1&b=x%26y")]
    [InlineData("api?x=0", "http://example.com/api?x=0&a=1&b=x%26y")]
    [InlineData("https://h.example/api", "https://h.example/api?a=1&b=x%26y")]
    public async Task QueryParameters_AreEncodedAndAppended(string url, string expected)
    {
        var mockHttp = new MockHttpMessageHandler();
        string? sent = null;
        mockHttp.When("*").Respond(req => { sent = req.RequestUri!.AbsoluteUri; return new HttpResponseMessage(HttpStatusCode.OK); });
        var client = new TestablePdsrClient(new HttpClient(mockHttp), new LoggerFactory());

        await client.Url(url).AddQueryString("a", "1").AddQueryString("b", "x&y").GetString();

        sent.Should().Be(expected);
    }
}

public class SerializerDefaultsTests
{
    [Fact]
    public void SerializerOptions_AreCached()
    {
        PdsrClientDefaults.CamelCaseSerializer.Should().BeSameAs(PdsrClientDefaults.CamelCaseSerializer);
        PdsrClientDefaults.SnakeSerializer.Should().BeSameAs(PdsrClientDefaults.SnakeSerializer);
        PdsrClientDefaults.DefaultSerializer.Should().BeSameAs(PdsrClientDefaults.DefaultSerializer);
    }
}

public class ServiceCollectionTests
{
    public interface IFirstClient { HttpClient Http { get; } }
    public interface ISecondClient { HttpClient Http { get; } }
    public class FirstClient(HttpClient http) : IFirstClient { public HttpClient Http { get; } = http; }
    public class SecondClient(HttpClient http) : ISecondClient { public HttpClient Http { get; } = http; }

    [Fact]
    public void TypedClient_ReceivesNamedConfiguredHttpClient()
    {
        var services = new ServiceCollection();
        services.AddPdsrClient<IFirstClient, FirstClient>(new PdsrClientConfigs { ClientName = "first" })
            .ConfigureHttpClient(h => h.BaseAddress = new Uri("https://first.example/"));

        using var provider = services.BuildServiceProvider();

        provider.GetRequiredService<IFirstClient>().Http.BaseAddress.Should().Be(new Uri("https://first.example/"));
        provider.GetRequiredService<IPdsrClientConfigs>().ClientName.Should().Be("first");
    }

    [Fact]
    public void TypedClients_WithoutConfigs_DoNotConflict()
    {
        var services = new ServiceCollection();
        services.AddPdsrClient<IFirstClient, FirstClient>()
            .ConfigureHttpClient(h => h.BaseAddress = new Uri("https://first.example/"));
        services.AddPdsrClient<ISecondClient, SecondClient>()
            .ConfigureHttpClient(h => h.BaseAddress = new Uri("https://second.example/"));

        using var provider = services.BuildServiceProvider();

        provider.GetRequiredService<IFirstClient>().Http.BaseAddress!.Host.Should().Be("first.example");
        provider.GetRequiredService<ISecondClient>().Http.BaseAddress!.Host.Should().Be("second.example");
    }
}
