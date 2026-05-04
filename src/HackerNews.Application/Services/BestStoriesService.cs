using System.Collections.Concurrent;
using HackerNews.Application.Configuration;
using HackerNews.Application.Interfaces;
using HackerNews.Domain.Stories;
using Microsoft.Extensions.Options;

namespace HackerNews.Application.Services;

public sealed class BestStoriesService(
    IHackerNewsClient client,
    IOptions<BestStoriesConfig> options) : IBestStoriesService
{
    private readonly BestStoriesConfig _opts = options.Value;

    public async Task<IReadOnlyList<Story>> GetBestStoriesAsync(int count, CancellationToken ct)
    {
        if (count <= 0) return [];
        count = Math.Min(count, _opts.MaxStories);

        var ids = await client.GetBestStoryIdsAsync(ct).ConfigureAwait(false);
        if (ids.Count == 0) return [];

        // Pull a buffer above `count` so we still satisfy the caller even if
        // some IDs map to non-stories or deleted items.
        var fetchSize = Math.Min(ids.Count, Math.Max(count * 2, count));
        var candidates = ids.Take(fetchSize).ToArray();

        var stories = new ConcurrentBag<Story>();
        await Parallel.ForEachAsync(
            candidates,
            new ParallelOptions
            {
                MaxDegreeOfParallelism = _opts.FetchConcurrency,
                CancellationToken = ct
            },
            async (id, token) =>
            {
                var story = await client.GetStoryAsync(id, token).ConfigureAwait(false);
                if (story is not null) stories.Add(story);
            }).ConfigureAwait(false);

        return stories
            .OrderByDescending(s => s.Score)
            .Take(count)
            .ToList();
    }
}
