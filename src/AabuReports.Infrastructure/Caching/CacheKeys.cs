namespace AabuReports.Infrastructure.Caching;

/// <summary>
/// the prefixes of the cache keys, each cached service puts its keys under its own prefix
/// so everything of it can be cleared with one RemoveByPrefixAsync call.
/// The prefixes end with ":" so "reports:" can never match another prefix that only starts with "reports".
/// </summary>
public static class CacheKeys
{
    public const string Locations = "locations:";
    public const string Reports = "reports:";
}
