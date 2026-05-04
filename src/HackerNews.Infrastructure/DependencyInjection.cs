using HackerNews.Application.Interfaces;
using HackerNews.Infrastructure.BackgroundJobs;
using HackerNews.Infrastructure.Configuration;
using HackerNews.Infrastructure.HackerNews;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace HackerNews.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddHackerNewsInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services
            .AddOptions<HackerNewsConfig>()
            .Bind(configuration.GetSection(HackerNewsConfig.SectionName))
            .ValidateOnStart();

        services.AddHttpClient<HackerNewsHttpClient>((sp, client) =>
        {
            var opts = sp.GetRequiredService<IOptions<HackerNewsConfig>>().Value;
            client.BaseAddress = new Uri(opts.BaseUrl);
            client.Timeout = TimeSpan.FromSeconds(opts.HttpTimeoutSeconds);
            client.DefaultRequestHeaders.UserAgent.ParseAdd("HackerNewsBestStoriesApi/1.0");
        })
        .AddStandardResilienceHandler(o =>
        {
            o.AttemptTimeout.Timeout = TimeSpan.FromSeconds(5);
            o.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(20);
            o.Retry.MaxRetryAttempts = 3;
            o.Retry.UseJitter = true;
            o.CircuitBreaker.FailureRatio = 0.5;
            o.CircuitBreaker.MinimumThroughput = 10;
        });

#pragma warning disable EXTEXP0018 // HybridCache is in preview at .NET 9 GA.
        services.AddHybridCache(o =>
        {
            o.DefaultEntryOptions = new HybridCacheEntryOptions
            {
                Expiration = TimeSpan.FromMinutes(10),
                LocalCacheExpiration = TimeSpan.FromMinutes(10)
            };
        });
#pragma warning restore EXTEXP0018

        // The use case sees the cached decorator; the decorator wraps the typed HttpClient.
        services.AddScoped<IHackerNewsClient>(sp => new CachedHackerNewsClient(
            sp.GetRequiredService<HackerNewsHttpClient>(),
            sp.GetRequiredService<HybridCache>(),
            sp.GetRequiredService<IOptions<HackerNewsConfig>>()));

        services.AddHostedService<CacheWarmerService>();

        return services;
    }
}
