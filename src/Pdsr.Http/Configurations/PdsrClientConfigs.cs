namespace Pdsr.Http
{
    public interface IPdsrClientConfigs
    {
        string ClientName { get; init; }
    }

    /// <summary>
    /// Client configurations
    /// </summary>
    public class PdsrClientConfigs : IPdsrClientConfigs
    {

        /// <summary>
        /// HttpClient name to use when using named clients
        /// </summary>
        public string ClientName { get; init; } = PdsrClientDefaults.DefaultClientName;
    }
}

#if NETSTANDARD2_0 || NETSTANDARD2_1

namespace System.Runtime.CompilerServices
{
    internal static class IsExternalInit { }
}

#endif
