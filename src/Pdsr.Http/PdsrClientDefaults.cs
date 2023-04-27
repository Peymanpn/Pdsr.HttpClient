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
    public static JsonSerializerOptions CamelCaseSerializer
    {
        get
        {
            return new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };
        }
    }

    public static JsonSerializerOptions SnakeSerializer
    {
        get
        {
            return new JsonSerializerOptions
            {
                PropertyNamingPolicy = SnakeCaseNamingPolicy.SnakeCase
            };
        }
    }
}
