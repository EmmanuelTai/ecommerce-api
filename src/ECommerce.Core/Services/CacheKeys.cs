namespace ECommerce.Core.Services;

// One place for cache key names, so the admin services (T11) remove exactly the keys these reads set.
public static class CacheKeys
{
    public const string Categories = "categories:all";

    public static string Product(int id) => $"products:{id}";
}
