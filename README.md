# Pdsr HttpClient Helper

A helper library to use with HTTP API Calls

[![.NET](https://github.com/Peymanpn/Pdsr.HttpClient/actions/workflows/dotnet.yml/badge.svg)](https://github.com/Peymanpn/Pdsr.HttpClient/actions/workflows/dotnet.yml)

## Getting Started

you need to install the package, add to DI and then use it in services.

1. install the package `dotnet add package Pdsr.HttpClient` and for extensions `dotnet add package Pdsr.HttpClient.Extensions`.
2. Inject an `System.Net.HttpClient` to the DI container.
3. Implement the `IPdsrClientBase` or inherit the abstract class `PdsrClientBase` and override any methods required.

```csharp
public class SomeService
{
    private readonly IPdsrClient _client;
    public SomeService(IPdsrClient client) => _client = client;

    public async Task SomeAsyncMethod(string someRouteId, string someQueryStringValue, CancellationToken cancellationToken = default)
    {
        var results = await _client.Url("https://example.com/api").AddUrl(someRouteId)
            .AddQueryString ("key" , someQueryStringValue)
            .Accept("application/vnd.api.custom+custom")
            .OnBadRequest( (res)=>
            {
                // do something
            })
            .OnException( (ex) =>
            {
                // do something about the exception
            })
            .OnNoFound( (res) =>
            {
                // do something when resource not found.
            })
            // and any other status code and so on
            // or add handler to the Client to run on certain situations
            // or add handler to the HttpRequestMessage on certain situations
            .Post(new { Something = "some value" })
            .SnakeCase()
            .SendAsync<SomeModelSupposeToDeserializeTo>(cancellationToken);
        return results;
    }
}
```

you need to override the abstract method `GetAuthorizationHeader` if your API needs authentication and implement the authorization logic there.

```csharp
protected abstract Task SetAuthorizationHeader(HttpRequestMessage request, CancellationToken cancellationToken = default);
```

You can also log all requests and responses in the inherited class, to have one code log all requests.

```csharp
protected abstract Task WriteLog(HttpResponseMessage response, long ellapsed, CancellationToken cancellationToken = default);
```

Use the required Serializer/Deserializer Name casing.

Right now, it only supports `CamelCase` by default and `SnakeCase` can be used as well.

In case of deserialization, if your API does not return any model or you want to get anything else other than Model, such as Stream, String, or the HttpResponseMessage itself, you can use the respected method such as `GetStream` or `GetString` and override the `SendAsync` to return the Message itself.

## Contribute

Please refer to [contribute](CONTRIBUTING.md).

## Documents

Under Construction.
