using System.Text.Json;
using System.Text.RegularExpressions;

namespace Pdsr.Http;

internal class SnakeCaseNamingPolicy : JsonNamingPolicy
{
    public override string ConvertName(string name)
    {
        return NameCasings.ToSnakeCase(name);
    }

    public static JsonNamingPolicy SnakeCase
    {
        get
        {
            return new SnakeCaseNamingPolicy();
        }
    }
}


internal class NameCasings
{
    public static string ToKebabCase(string input)
    {
        if (string.IsNullOrEmpty(input)) { return input; }

        var startUnderscores = Regex.Match(input, @"^_+");
        return startUnderscores + Regex.Replace(input, @"([a-z0-9])([A-Z])", "$1-$2").ToLower();
    }
    public static string ToSnakeCase(string input)
    {
        if (string.IsNullOrEmpty(input)) { return input; }

        var startUnderscores = Regex.Match(input, @"^_+");
        return startUnderscores + Regex.Replace(input, @"([a-z0-9])([A-Z])", "$1_$2").ToLower();

    }

}
