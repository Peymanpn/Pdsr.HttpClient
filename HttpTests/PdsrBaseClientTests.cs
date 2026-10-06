using Pdsr.Http;
using RichardSzalay.MockHttp;
using System.Net;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using System;
using Pdsr.Http.Extensions;

namespace HttpTests;



/// <summary>
/// A provider that creates instances of <see cref="ListLogger{T}"/> for capturing log messages in tests.
/// </summary>
public class ListLoggerProvider : ILoggerProvider
{
    private readonly ListLogger<PdsrClientBase> _logger;

    /// <summary>
    /// Initializes a new instance of <see cref="ListLoggerProvider"/> with the specified logger.
    /// </summary>
    /// <param name="logger">The <see cref="ListLogger{PdsrClientBase}"/> instance to use for logging.</param>
    public ListLoggerProvider(ListLogger<PdsrClientBase> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Creates a new logger instance.
    /// </summary>
    /// <param name="categoryName">The category name for the logger (ignored in this implementation).</param>
    /// <returns>The <see cref="ListLogger{PdsrClientBase}"/> instance provided in the constructor.</returns>
    public ILogger CreateLogger(string categoryName)
    {
        return _logger;
    }

    /// <summary>
    /// Disposes the provider. No resources are held, so this is a no-op.
    /// </summary>
    public void Dispose()
    {
        // No resources to dispose
    }


}

public class ListLogger<T> : ILogger<T>
{
    public List<(LogLevel Level, string Message)> Logs { get; } = new();
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
    public bool IsEnabled(LogLevel logLevel) => true;


    public IEnumerable<(LogLevel Level, string Message)> GetLogs(LogLevel level)
    {
        return Logs.Where(l => l.Level == level);
    }

    public bool ContainsMessage(string substring, LogLevel? level = null)
    {
        return Logs.Any(l => (level == null || l.Level == level) && l.Message.Contains(substring));
    }

    private readonly object _lock = new object();

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        var message = formatter(state, exception);
        if (exception != null)
        {
            message += $"\nException: {exception}";
        }
        lock (_lock)
        {
            Logs.Add((logLevel, message));
        }
    }
}

public class TestablePdsrClient : PdsrClientBase
{
    public TestablePdsrClient(HttpClient client, ILoggerFactory loggerFactory)
        : base(client, loggerFactory) { }

    protected override Task SetBaseAddress(CancellationToken cancellationToken)
    {
        BaseAddress = new Uri("http://example.com");
        return Task.CompletedTask;
    }

    protected override Task SetAuthorizationHeader(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}

public class PdsrClientBaseTests
{
    private readonly TestablePdsrClient _client;
    private readonly MockHttpMessageHandler _mockHttp;
    private readonly ListLogger<PdsrClientBase> _logger;

    public PdsrClientBaseTests()
    {
        _mockHttp = new MockHttpMessageHandler();
        var httpClient = new HttpClient(_mockHttp);
        _logger = new ListLogger<PdsrClientBase>();
        var loggerFactory = new LoggerFactory();
        loggerFactory.AddProvider(new ListLoggerProvider(_logger));
        _client = new TestablePdsrClient(httpClient, loggerFactory);
    }

    [Fact]
    public async Task GetString_Success_ReturnsContent()
    {
        _mockHttp.When("http://example.com/*")
                 .Respond(HttpStatusCode.OK, "text/plain", "Hello");
        _client.RequestUrlPath = "/test";

        var result = await _client.GetString(cancellationToken: CancellationToken.None);

        Assert.Equal("Hello", result);
    }

    [Fact]
    public async Task GetString_NoSuccess_ReturnsContent()
    {
        _mockHttp.When("http://example.com/*")
            .Respond(HttpStatusCode.BadRequest, "text/plain", "Bad request");
        _client.RequestUrlPath = "/test";
        _client.EnsureSuccess = true;


        var exception = await Assert.ThrowsAsync<HttpRequestException>(async () => await _client.GetString(cancellationToken: CancellationToken.None));
    }

    [Fact]
    public async Task ConfigAndSend_NonSuccessStatusCode_LogsWarningWithDetails()
    {
        // Arrange
        _mockHttp.When("http://example.com/*")
                 .Respond(HttpStatusCode.BadRequest, "text/plain", "Invalid input");
        var request = new HttpRequestMessage(HttpMethod.Get, "/test");
        _client.RequestUrlPath = "/test";

        // Act
        var response = await _client.ConfigAndSend(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        _logger.ContainsMessage("Error Response, contents: Invalid input", LogLevel.Warning).Should().BeTrue();
        _logger.GetLogs(LogLevel.Warning).Should().HaveCount(2);
    }


    [Fact]
    public async Task EnsureSuccess_SuccessfulResponse_DoesNotThrow()
    {
        // Arrange
        _mockHttp.When("http://example.com/*")
                 .Respond(HttpStatusCode.OK, "text/plain", "Success");
        _client.RequestUrlPath = "/test";
        bool actionCalled = false;
        _client.EnsureSuccess((res, status) => actionCalled = true);

        // Act
        var request = new HttpRequestMessage(HttpMethod.Get, "/test");
        var response = await _client.ConfigAndSend(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        actionCalled.Should().BeFalse(); // Action not called for success
    }

    [Fact]
    public async Task EnsureSuccess_NonSuccessfulResponse_InvokesActionAndThrows()
    {
        // Arrange
        _mockHttp.When("http://example.com/*")
                 .Respond(HttpStatusCode.BadRequest, "text/plain", "Bad Request");
        _client.RequestUrlPath = "/test";
        HttpResponseMessage? capturedResponse = null;
        HttpStatusCode? capturedStatus = null;
        _client.EnsureSuccess((res, status) =>
        {
            capturedResponse = res;
            capturedStatus = status;
        });

        // Act
        var request = new HttpRequestMessage(HttpMethod.Get, "/test");
        var exception = await Record.ExceptionAsync(() => _client.ConfigAndSend(request));

        // Assert
        exception.Should().BeOfType<HttpRequestException>();
        exception.Message.Should().Contain("Response status code does not indicate success: BadRequest (Bad Request)");
        capturedResponse.Should().NotBeNull();
        capturedStatus.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task EnsureSuccess_MultipleCalls_AccumulatesHandlers()
    {
        // Arrange
        _mockHttp.When("http://example.com/*")
                 .Respond(HttpStatusCode.BadRequest, "text/plain", "Bad Request");
        _client.RequestUrlPath = "/test";
        int action1CallCount = 0;
        int action2CallCount = 0;
        _client.EnsureSuccess((res, status) => action1CallCount++);
        _client.EnsureSuccess((res, status) => action2CallCount++);

        // Act
        var request = new HttpRequestMessage(HttpMethod.Get, "/test");
        var exception = await Record.ExceptionAsync(() => _client.ConfigAndSend(request));

        // Assert
        exception.Should().BeOfType<HttpRequestException>();
        action1CallCount.Should().Be(1);
        action2CallCount.Should().Be(1);
    }

    [Fact]
    public async Task EnsureSuccess_NullAction_ThrowsWithDefaultBehavior()
    {
        // Arrange
        _mockHttp.When("http://example.com/*")
                 .Respond(HttpStatusCode.NotFound, "text/plain", "Not Found");
        _client.RequestUrlPath = "/test";
        _client.EnsureSuccess(whatToDoIfNoSuccess: null!); // Null action

        // Act
        var request = new HttpRequestMessage(HttpMethod.Get, "/test");
        var exception = await Record.ExceptionAsync(() => _client.ConfigAndSend(request));

        // Assert
        exception.Should().BeOfType<HttpRequestException>();
        exception.Message.Should().Contain("Response status code does not indicate success: NotFound (Not Found)");
    }

    [Fact]
    public async Task EnsureSuccess_Cancellation_RespectsCancellationToken()
    {
        // Arrange
        _mockHttp.When("http://example.com/*")
                 .Respond(() => Task.Delay(1000).ContinueWith(_ => new HttpResponseMessage(HttpStatusCode.OK)));
        _client.RequestUrlPath = "/test";
        bool actionCalled = false;
        _client.EnsureSuccess((res, status) => actionCalled = true);

        var cts = new CancellationTokenSource();

        // Act
        var request = new HttpRequestMessage(HttpMethod.Get, "/test");
        cts.Cancel(); // Cancel immediately
        var exception = await Record.ExceptionAsync(() => _client.ConfigAndSend(request, cts.Token));

        // Assert
        exception.Should().BeOfType<TaskCanceledException>();
        actionCalled.Should().BeFalse();
    }
}
