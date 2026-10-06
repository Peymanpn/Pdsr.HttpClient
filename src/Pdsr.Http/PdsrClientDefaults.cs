using System.Text.Json;

namespace Pdsr.Http;

/// <summary>
/// HttpClient Default values
/// </summary>
public static class PdsrClientDefaults
{
    /// <summary>
    /// Default HttpClient name
    /// </summary>
    public const string DefaultClientName = "httpClient";

    // System.Text.Json caches type metadata per options instance, so these must be created once and reused.
    private static readonly JsonSerializerOptions _defaultSerializer = new();

    private static readonly JsonSerializerOptions _camelCaseSerializer = new(JsonSerializerDefaults.Web);

    private static readonly JsonSerializerOptions _snakeSerializer = new()
    {
        PropertyNamingPolicy = SnakeCaseNamingPolicy.SnakeCase,
        PropertyNameCaseInsensitive = true
    };

    /// <summary>
    /// Default Serializer options. Property names are used as-is and matched case-sensitively.
    /// </summary>
    public static JsonSerializerOptions DefaultSerializer => _defaultSerializer;

    /// <summary>
    /// CamelCase JsonSerializer (<see cref="JsonSerializerDefaults.Web"/>): camelCase names, case-insensitive reads.
    /// </summary>
    public static JsonSerializerOptions CamelCaseSerializer => _camelCaseSerializer;

    /// <summary>
    /// SnakeCase JsonSerializer: snake_case names, case-insensitive reads.
    /// </summary>
    public static JsonSerializerOptions SnakeSerializer => _snakeSerializer;

    /// <summary>
    /// Returns the shared serializer options for <paramref name="namingStrategy"/>.
    /// </summary>
    public static JsonSerializerOptions GetSerializerOptions(SerializationNamingStrategy namingStrategy) => namingStrategy switch
    {
        SerializationNamingStrategy.Camel => CamelCaseSerializer,
        SerializationNamingStrategy.Snake => SnakeSerializer,
        _ => DefaultSerializer,
    };
}
