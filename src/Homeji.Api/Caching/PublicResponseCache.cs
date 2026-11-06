namespace Homeji.Api.Caching;

public static class PublicCachePolicies
{
    public const string Catalog = "public-catalog";
    public const string Listings = "public-listings";
    public const string Stock = "public-stock";
    public const string Pricing = "public-pricing";
}

public static class PublicResponseCacheExtensions
{
    /// <summary>
    /// Short-lived server cache for anonymous reads. The built-in policy skips signed-in requests,
    /// responses that set cookies and anything other than 200, so personal data is never stored.
    /// </summary>
    public static IServiceCollection AddPublicResponseCache(this IServiceCollection services)
    {
        services.AddOutputCache(options =>
        {
            // The instance has 512 MB, so keep the cache small and evict rather than grow.
            options.SizeLimit = 16L * 1024 * 1024;
            options.MaximumBodySize = 1024 * 1024;
            options.AddPolicy(PublicCachePolicies.Catalog, policy => Vary(policy, TimeSpan.FromSeconds(60)));
            options.AddPolicy(PublicCachePolicies.Listings, policy => Vary(policy, TimeSpan.FromSeconds(30)));
            // Stock changes with every order; checkout still validates it against the database.
            options.AddPolicy(PublicCachePolicies.Stock, policy => Vary(policy, TimeSpan.FromSeconds(15)));
            // Checkout re-reads the price and rejects a mismatch, so a stale display cannot charge it.
            options.AddPolicy(PublicCachePolicies.Pricing, policy => Vary(policy, TimeSpan.FromSeconds(30)));
        });
        return services;
    }

    private static void Vary(Microsoft.AspNetCore.OutputCaching.OutputCachePolicyBuilder policy, TimeSpan lifetime) =>
        policy.Expire(lifetime).SetVaryByQuery("*").SetVaryByHeader("Origin");
}
