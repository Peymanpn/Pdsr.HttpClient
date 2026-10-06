using System.Text;

namespace Pdsr.Http;

/// <summary>
/// Appends query string parameters to a url.
/// </summary>
internal static class QueryStringBuilder
{
    /// <summary>
    /// Appends <paramref name="parameters"/> to <paramref name="url"/>, keeping any existing query string and fragment.
    /// Parameters with a null value are skipped; keys and values are percent-encoded.
    /// </summary>
    public static string Append(string url, IEnumerable<KeyValuePair<string, string?>> parameters)
    {
        int fragmentIndex = url.IndexOf('#');
        string fragment = fragmentIndex >= 0 ? url.Substring(fragmentIndex) : string.Empty;
        string path = fragmentIndex >= 0 ? url.Substring(0, fragmentIndex) : url;

        StringBuilder sb = new(path);
        bool hasQuery = path.IndexOf('?') >= 0;

        foreach (KeyValuePair<string, string?> parameter in parameters)
        {
            if (parameter.Value is null)
            {
                continue;
            }

            sb.Append(hasQuery ? '&' : '?')
              .Append(Uri.EscapeDataString(parameter.Key))
              .Append('=')
              .Append(Uri.EscapeDataString(parameter.Value));
            hasQuery = true;
        }

        return sb.Append(fragment).ToString();
    }
}
