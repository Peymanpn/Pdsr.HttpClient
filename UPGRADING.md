# Upgrading to Pdsr.Http 4.0

This guide covers moving from **3.x (3.8.0)** to **4.0**. 4.0 fixes a number of behavior bugs, and several of those fixes change what your code observes at runtime even where it still compiles. Read the [behavior changes](#behavior-changes) section even if your build is green.

## Quick checklist

1. Remove the `Pdsr.Http.Extensions` package reference and keep only `Pdsr.Http` 4.0.
2. Fix compile errors from [removed delegate types and overloads](#api-changes-compile-time).
3. Search for `.Url(` calls that pass `append:` or rely on appending: the [default changed](#url-now-replaces-by-default).
4. Search for `OnException` handlers that were used to **suppress** exceptions: [exceptions are now rethrown](#exceptions-are-always-rethrown).
5. Check clients that set `EnsureSuccess`, `QueryParameters` or `NamingStrategy` once and expected them to stick: [per-request state is reset](#per-request-state-is-reset-after-every-request).
6. If you call `AddPdsrClient<TInterface, TClient>()`, review [DI changes](#dependency-injection).

---

## Packages and platforms

| | 3.x | 4.0 |
|---|---|---|
| Target frameworks | netstandard2.0, netstandard2.1, net8.0, net9.0 | netstandard2.0, netstandard2.1, **net10.0** |
| Packages | `Pdsr.Http` + `Pdsr.Http.Extensions` | `Pdsr.Http` only |
| `Microsoft.Extensions.*`, `System.Text.Json` | 9.0.3 | 10.0.12 |
| `Microsoft.AspNetCore.WebUtilities` | required | **removed** |

- **.NET 8 / .NET 9 apps** still work: they consume the `netstandard2.1` build. The `Microsoft.Extensions.*` 10.x packages support .NET 8 and 9, but your app will be lifted to those versions.
- **`Pdsr.Http.Extensions` is merged into `Pdsr.Http`.** The namespace is unchanged (`Pdsr.Http.Extensions`), so `using` statements stay as they are. Remove the old package reference, or you will get duplicate type errors.

## API changes (compile-time)

### Removed delegate types

The custom delegate types are replaced by standard `Func`/`Action` signatures with the same parameters. Lambdas passed inline keep compiling; variables, fields or method groups declared with these types must change.

| Removed (3.x) | Use instead |
|---|---|
| `GeneralStatusHandler` | `Action<HttpResponseMessage, HttpRequestMessage?, HttpStatusCode>` |
| `AsyncGeneralStatusHandler` | `Func<HttpResponseMessage, HttpRequestMessage?, HttpStatusCode, CancellationToken, Task>` |
| `ErrorRequestHandlerAsync` | `Func<object?, HttpResponseMessage, CancellationToken, Task>` |
| `ErrorequestHandler` | `Action<object?, HttpResponseMessage>` |
| `ExceptionHandler` | `Action<HttpResponseMessage?, Exception>` |
| `ExceptionHandlerAsync` | `Func<HttpResponseMessage?, Exception, CancellationToken, Task>` |

The `OnAnyResponse(GeneralStatusHandler)` overload was removed; use `OnAnyResponse(Action<HttpResponseMessage>)` or the async 4-parameter overload.

### `OnBadRequest<TClient, TError>` is typed

The error model is now passed as `TError?` instead of `object?`:

```csharp
// 3.x
client.OnBadRequest<MyClient, ProblemDetails>((error, res, ct) =>
{
    var problem = (ProblemDetails?)error;
    ...
});

// 4.0
client.OnBadRequest<MyClient, ProblemDetails>((problem, res, ct) =>
{
    // problem is ProblemDetails?
    ...
});
```

The error body is now deserialized with the client's `NamingStrategy` (3.x always used case-sensitive default options).

### Removed overloads

| Removed | Replacement |
|---|---|
| `GetString(CancellationToken, string? requestUrl, bool dontAuthenticate)` | `client.Url(requestUrl).GetString(ct)`. Neither extra parameter was ever used. |
| `SendAsync<T>(CancellationToken, string? requestUrl, bool dontAuthenticate)` | `client.Url(requestUrl).SendAsync<T>(ct)` |
| `Url(this IPdsrClientBase, string, bool)` (non-generic) | The generic `Url<TClient>` covers the same calls. |

### `AddQueryString` accepts empty values

`AddQueryString("flag", "")` is now allowed (`?flag=`). A `null` value throws `ArgumentNullException` (previously `ArgumentException` for null or empty).

## Behavior changes

### `Url` now replaces by default

In 3.x the `append` flag was inverted: `Url("x")` **appended** and `Url("x", append: true)` **replaced**. 4.0 matches the documentation:

| Call | 3.x | 4.0 |
|---|---|---|
| `Url("x")` | appends | **replaces** |
| `Url("x", append: true)` | replaces | **appends** |

If you chained `Url(...)` calls to build a path, switch to `AddUrl`:

```csharp
client.Url("api").AddUrl("orders").AddUrl(id);
```

`SetUrl` and `AddUrl` are unchanged.

### Exceptions are always rethrown

In 3.x, when `SendAsync` threw (DNS failure, connection refused, timeout) and an `OnException` handler was registered, the exception was **swallowed**. Processing then continued with an empty, fake `200 OK` response: `GetString` returned `""`, `SendAsync<T>` returned `default`, and status-code handlers ran as if the call had succeeded.

In 4.0:

- Exception handlers still run, and receive `null` as the response (there is none).
- The exception is then **rethrown**.
- Handlers do **not** run when the caller's `CancellationToken` is cancelled; the `OperationCanceledException` propagates directly. Timeouts still reach the handlers.

If you relied on handlers to suppress failures, catch the exception at the call site:

```csharp
try
{
    return await client.Url("orders").SendAsync<Order>(ct);
}
catch (HttpRequestException)
{
    return null;
}
```

### `SendAsync<T>` results

| Situation | 3.x | 4.0 |
|---|---|---|
| Non-2xx response, `EnsureSuccess` off | tried to deserialize the error body as `T` | returns `default` |
| Empty body (e.g. `204 No Content`) | `default`, or `JsonException` when `EnsureSuccess` was set | returns `default` |
| Invalid JSON, `EnsureSuccess` off | `default` | `default` (logged as a warning) |
| Invalid JSON, `EnsureSuccess` on | throws `JsonException` | throws `JsonException` |
| Cancellation during deserialization | swallowed, returned `default` | throws `OperationCanceledException` |

Use `OnBadRequest`, `OnStatusCode` or `OnAnyResponse` to read error bodies.

### The `EnsureSuccess` property now throws on non-2xx

In 3.x, setting `client.EnsureSuccess = true` directly only made `SendAsync<T>` rethrow deserialization errors; a non-2xx response did not throw unless you used the `.EnsureSuccess()` extension. In 4.0 the property alone makes any non-2xx response throw `HttpRequestException`, after the status-code handlers have run. The `.EnsureSuccess()` extension behaves as before.

### Default naming strategy is camelCase

`NamingStrategy` now defaults to `Camel`. In 3.x it defaulted to `None` (exact, case-sensitive property names), despite the docs saying camelCase. That meant camelCase JSON silently bound to empty objects.

The camelCase and snake_case options now also **read case-insensitively**. `Camel` uses `JsonSerializerDefaults.Web`, which additionally accepts numbers written as strings.

To keep the 3.x behavior, set it in your client's constructor:

```csharp
NamingStrategy = SerializationNamingStrategy.None;
```

`PdsrClientDefaults.DefaultSerializer`, `CamelCaseSerializer` and `SnakeSerializer` now return shared, cached instances, so treat them as read-only. Mutating one changes it for every client, and System.Text.Json throws if options are modified after first use. Create your own `JsonSerializerOptions` (e.g. by overriding `SerializerOptions`) if you need different settings.

### Per-request state is reset after every request

After `GetString`, `GetStream` or `SendAsync<T>` completes, **whether it succeeds or throws**, the client resets:

- `ConfigRequestMessage`, `ConfigHttpClient`, `HandleStatusCodeBase`, `HandleExceptionAsync`
- `RequestUrlPath`
- `QueryParameters` (**new in 4.0**)
- `EnsureSuccess` (**new in 4.0**)
- `NamingStrategy`, when it was changed by `.SnakeCase()` / `.CamelCase()` for that request (**new in 4.0**)

In 3.x the reset was skipped whenever a request threw, so the next request on the same instance inherited the previous URL, query string, handlers and `EnsureSuccess`. After a failed retry it could also skip your `ConfigRequest` delegates entirely.

**If you set a value once and relied on it sticking** (for example an API-key query parameter or `EnsureSuccess = true` in the constructor), re-apply it by overriding `ClearConfigs`:

```csharp
protected override void ClearConfigs()
{
    base.ClearConfigs();
    EnsureSuccess = true;
    QueryParameters["api_key"] = _apiKey;
}
```

Setting `NamingStrategy` directly (in the constructor or on the property) still sticks. Only the fluent `.SnakeCase()` / `.CamelCase()` calls are per-request.

If you call `ConfigAndSend` directly from a subclass, it resets the state itself when it completes.

### Subclass changes

- **`SendAsync<T>` no longer calls `GetStream`.** If you overrode `GetStream` to change how `SendAsync<T>` reads responses, override `SendAsync<T>` or `Deserialize<T>` instead.
- **Retries (`IsRetryRequired` / `_retryCount`):**
  - `_retryCount` is now the maximum number of retries **per request**. In 3.x it was a lifetime total, so after 5 retries an instance never retried again.
  - `SetAuthorizationHeader`, `WriteLog` and the status-code handlers run on every attempt, so you can refresh a token in an `OnAuthorizationFail` handler or in `IsRetryRequired`.
  - `SetBaseAddress` runs once per request, not once per attempt.
  - Each retry sends its own copy of the request body. This fixes `ObjectDisposedException` when retrying POST/PUT/PATCH on .NET Framework.
  - The previous attempt's response is disposed before retrying.
- **`SetBaseAddress`** is still called for every request. `HttpClient.BaseAddress` cannot be changed after the first request, so only set it when it is `null`:

  ```csharp
  protected override Task SetBaseAddress(CancellationToken ct)
  {
      BaseAddress ??= new Uri("https://api.example.com/");
      return Task.CompletedTask;
  }
  ```

- **`Dispose()` no longer disposes the `HttpClient`** passed to the constructor. The client belongs to `IHttpClientFactory` or to whoever created it. If you construct clients with `new HttpClient()` yourself, dispose that `HttpClient` yourself.

### Logging

Logs no longer include request or response objects. In 3.x those were logged at `Warning` for every non-2xx response, which wrote `Authorization` headers (bearer tokens) to the logs.

| Event | Level | Message |
|---|---|---|
| Non-2xx response | Warning | `{Method} {Url} responded {StatusCode} in {Elapsed} ms` |
| Non-2xx response body | **Debug** (was Warning) | `Error response contents: {Contents}` |
| 2xx response | Trace | `{Method} {Url} responded {StatusCode} in {Elapsed} ms` |
| Send failure | Warning | `{Method} {Url} failed after {Elapsed} ms`, with the exception |
| Retry | Information | `Retrying {Method} {Url} after {StatusCode}, retries remaining: {Retries}` |

Update any log queries or alerts that matched the old message text. Enable `Debug` for the `Pdsr.Http.PdsrClientBase` category to see error bodies.

## Dependency injection

### `AddPdsrClient<TInterface, TClient>()` registers a typed client

In 3.x this registered `TClient` as **scoped** and separately registered a named `HttpClient`. `TClient` was then given the *default unnamed* `HttpClient`, so anything configured on the returned builder (base address, handlers, resilience policies) never reached it.

4.0 registers `TClient` as a standard typed client (`AddHttpClient<TInterface, TClient>`), so the builder's configuration applies:

```csharp
services.AddPdsrClient<IOrdersClient, OrdersClient>(new PdsrClientConfigs { ClientName = "orders" })
    .ConfigureHttpClient(c => c.BaseAddress = new Uri("https://orders.example/"));
```

What changes for you:

- **Lifetime is transient** (typed-client default) instead of scoped. Don't rely on one instance being shared across a scope.
- **Client names must be unique per typed client.** Registering two typed clients with the same `ClientName` throws at startup. When you pass no config, the client is now named after `TClient` instead of the shared `"httpClient"` name, so registering several clients without configs works.
- The config you pass is registered as `IPdsrClientConfigs` (singleton, if not already registered).

### `AddPdsrClient<TConfig>(config)` registers the config

The config instance is now registered as `TConfig` (singleton, if not already registered), so `PdsrNamedClientBase<TConfig>` can resolve it without extra registration.
