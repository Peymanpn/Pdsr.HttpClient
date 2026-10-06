namespace Pdsr.Http;

/// <summary>
/// Naming strategies for different casings.
/// </summary>
public enum SerializationNamingStrategy
{
    /// <summary>
    /// Property names are used as-is and matched case-sensitively.
    /// </summary>
    None,
    /// <summary>
    /// Camel Case
    /// </summary>
    Camel,

    /// <summary>
    /// SnakeCase
    /// </summary>
    Snake
}
