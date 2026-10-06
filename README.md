# Pdsr HttpClient Helper

A helper library to use with HTTP API Calls

[![.NET](https://github.com/Peymanpn/Pdsr.HttpClient/actions/workflows/dotnet.yml/badge.svg)](https://github.com/Peymanpn/Pdsr.HttpClient/actions/workflows/dotnet.yml)

[![NuGet version (Pdsr.Http)](https://img.shields.io/nuget/v/Pdsr.Http.svg?style=flat-square)](https://www.nuget.org/packages/Pdsr.Http/)

> **Upgrading from 3.x?** See [UPGRADING.md](UPGRADING.md) for breaking changes in 4.0.

## Getting Started

1. Install the package: `dotnet add package Pdsr.Http`. The fluent extensions are included; `Pdsr.Http.Extensions` is no longer needed.
2. Inherit the abstract class `PdsrClientBase` and implement `SetBaseAddress` and `SetAuthorizationHeader`.
3. Register it as a typed client and configure its `HttpClient`.

```csharp
public interface IOrdersClient : IPdsrClientBase { }

public class OrdersClient : PdsrClientBase, IOrdersClient
{
    public OrdersClient(HttpClient client, ILoggerFactory loggerFactory) : base(client, loggerFactory) { }

    // HttpClient.BaseAddress cannot change after the first request, so only set it when missing.
    protected override Task SetBaseAddress(CancellationToken cancellationToken = default)
    {
        BaseAddress ??= new Uri("https://example.com/api/");
        return Task.CompletedTask;
    }

    // Called before every attempt, including retries.
    protected override Task SetAuthorizationHeader(HttpRequestMessage request, CancellationToken cancellationToken = default)
    {
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", "...");
        return Task.CompletedTask;
    }
}

services.AddPdsrClient<IOrdersClient, OrdersClient>(new PdsrClientConfigs { ClientName = "orders" })
    .ConfigureHttpClient(c => c.Timeout = TimeSpan.FromSeconds(30));
```

Then build and send requests fluently:

```csharp
public class SomeService
{
    private readonly IOrdersClient _client;
    public SomeService(IOrdersClient client) => _client = client;

    public Task<Order?> CreateOrder(string customerId, NewOrder order, CancellationToken cancellationToken = default)
    {
        return _client.Url("customers").AddUrl(customerId).AddUrl("orders")
            .AddQueryString("notify", "true")
            .Accept("application/json")
            .OnBadRequest<IOrdersClient, ProblemDetails>((problem, response, ct) =>
            {
                // handle the validation errors
                return Task.CompletedTask;
            })
            .OnNotFound((response, ct) =>
            {
                // do something when the customer is not found
                return Task.CompletedTask;
            })
            .OnException((response, exception) =>
            {
                // observe the failure; it is rethrown afterwards
            })
            .Post(order, SerializationNamingStrategy.Snake)
            .SnakeCase()
            .SendAsync<Order>(cancellationToken);
    }
}
```

Everything configured on the client (url, query strings, handlers, `EnsureSuccess`, `.SnakeCase()`) applies to the next request only, and is reset when it completes, whether it succeeds or fails.

- `SendAsync<T>` returns `default` for a non-success response or an empty body. Call `.EnsureSuccess()` to throw `HttpRequestException` on a non-success status instead.
- `GetString` and `GetStream` return the raw response contents.
- Override `WriteLog` to log every response in one place, and `IsRetryRequired` to retry responses (up to `_retryCount` times per request).

### Serialization naming

Responses are deserialized with camelCase names (case-insensitive) by default. Use `.SnakeCase()` for a single request, or set `NamingStrategy` in your client's constructor to change the default. Request bodies sent with `Post`, `Put` and `Patch` use the naming strategy passed to them (camelCase by default).

## Contribute

Please refer to [contribute](CONTRIBUTING.md).

## Documents

Under Construction.
