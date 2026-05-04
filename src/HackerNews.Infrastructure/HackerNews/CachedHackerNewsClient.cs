using HackerNews.Application.Interfaces;
using HackerNews.Domain.Stories;
using HackerNews.Infrastructure.Configuration;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Options;

namespace HackerNews.Infrastructure.HackerNews;

/// <summary>
/// Decorator that puts a stampede-protected <see cref="HybridCache"/> in front
/// of <see cref="HackerNewsHttpClient"/>. The cache layer is fully invisible
/// to the Application layer — the use case sees only <see cref="IHackerNewsClient"/>.
/// </summary>
public sealed class CachedHackerNewsClient(
    HackerNewsHttpClient inner,
    HybridCache cache,
    IOptions<HackerNewsConfig> options) : IHackerNewsClient
{
    private readonly HackerNewsConfig _opts = options.Value;

    private const string IdsCacheKey = "hn:beststories:ids";
    internal const string IdsTag = "hn:ids";
    internal const string ItemsTag = "hn:items";
    private static readonly string[] IdsTags = [IdsTag];
    private static readonly string[] ItemsTags = [ItemsTag];

    private static string ItemKey(long id) => $"hn:item:{id}";

    public async Task<IReadOnlyList<long>> GetBestStoryIdsAsync(CancellationToken ct) =>
        await cache.GetOrCreateAsync(
            IdsCacheKey,
            inner,
            static async (client, token) =>
            {
                var ids = await client.GetBestStoryIdsAsync(token).ConfigureAwait(false);
                return ids as long[] ?? [.. ids];
            },
            new HybridCacheEntryOptions { Expiration = TimeSpan.FromSeconds(_opts.IdListCacheSeconds) },
            tags: IdsTags,
            cancellationToken: ct).ConfigureAwait(false);

    public async Task<Story?> GetStoryAsync(long id, CancellationToken ct) =>
        await cache.GetOrCreateAsync(
            ItemKey(id),
            (inner, id),
            static async (state, token) => await state.inner.GetStoryAsync(state.id, token).ConfigureAwait(false),
            new HybridCacheEntryOptions { Expiration = TimeSpan.FromMinutes(_opts.ItemCacheMinutes) },
            tags: ItemsTags,
            cancellationToken: ct).ConfigureAwait(false);
}
