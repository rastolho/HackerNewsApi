using HackerNews.Application.Interfaces;
using HackerNews.Infrastructure.Configuration;
using HackerNews.Infrastructure.HackerNews;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HackerNews.Infrastructure.BackgroundJobs;

/// <summary>
/// Periodically refreshes the Hacker News cache so user requests almost always
/// hit a warm path. Lives in Infrastructure: it knows about the cache and
/// triggers a fresh upstream pull on each tick.
/// </summary>
public sealed class CacheWarmerService(
    IServiceScopeFactory scopeFactory,
    HybridCache cache,
    IOptions<HackerNewsConfig> options,
    ILogger<CacheWarmerService> logger) : BackgroundService
{
    private readonly HackerNewsConfig _opts = options.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_opts.EnableCacheWarmer)
        {
            logger.LogInformation("Cache warmer disabled via configuration");
            return;
        }

        await SafeWarmAsync(stoppingToken).ConfigureAwait(false);

        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(_opts.CacheWarmIntervalSeconds));
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
            {
                await SafeWarmAsync(stoppingToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            // Shutdown.
        }
    }

    private async Task SafeWarmAsync(CancellationToken ct)
    {
        try
        {
            // Force a fresh pull of the best-stories ID list, then ask the
            // service to fetch the top N — this populates the per-item cache
            // via the cached decorator.
            await cache.RemoveByTagAsync(CachedHackerNewsClient.IdsTag, ct).ConfigureAwait(false);

            using var scope = scopeFactory.CreateScope();
            var bestStories = scope.ServiceProvider.GetRequiredService<IBestStoriesService>();
            await bestStories.GetBestStoriesAsync(_opts.WarmTopN, ct).ConfigureAwait(false);

            logger.LogInformation("Cache warmed: top {WarmTopN} items", _opts.WarmTopN);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            // Never let a transient warm failure crash the host — try again next tick.
            logger.LogWarning(ex, "Cache warm iteration failed");
        }
    }
}
