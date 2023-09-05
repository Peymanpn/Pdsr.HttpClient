using System.Text.Json;

namespace Pdsr.Http;

public static class PdsrClientDefaults
{
    /// <summary>
    /// Default HttpClient name
    /// </summary>
    public const string DefaultClientName = "httpClient";

    /// <summary>
    /// Default Serializer options
    /// </summary>
    public static JsonSerializerOptions DefaultSerializer
    {
        get
        {
            return new JsonSerializerOptions();
        }
    }
    /// <summary>
    /// CamelCase JsonSerializer
    /// </summary>
    public static JsonSerializerOptions CamelCaseSerializer => new JsonSerializerOptions
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static JsonSerializerOptions SnakeSerializer => new JsonSerializerOptions
    {
        PropertyNamingPolicy = SnakeCaseNamingPolicy.SnakeCase
    };
}
