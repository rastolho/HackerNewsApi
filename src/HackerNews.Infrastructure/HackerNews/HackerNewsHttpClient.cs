using System.Net.Http.Json;
using HackerNews.Application.Interfaces;
using HackerNews.Domain.Stories;
using HackerNews.Infrastructure.HackerNews.Models;

namespace HackerNews.Infrastructure.HackerNews;

/// <summary>
/// Talks to the public Hacker News REST API. The only place in the codebase
/// aware of the upstream JSON shape — translates raw items to domain
/// <see cref="Story"/> instances and filters out non-story types.
/// </summary>
public sealed class HackerNewsHttpClient(HttpClient http) : IHackerNewsClient
{
    public async Task<IReadOnlyList<long>> GetBestStoryIdsAsync(CancellationToken ct)
    {
        var ids = await http.GetFromJsonAsync<long[]>("beststories.json", ct).ConfigureAwait(false);
        return ids ?? [];
    }

    public async Task<Story?> GetStoryAsync(long id, CancellationToken ct)
    {
        var item = await http.GetFromJsonAsync<HackerNewsItem>($"item/{id}.json", ct).ConfigureAwait(false);
        if (item is null || item.Type != "story") return null;

        return new Story(
            Id: item.Id,
            Title: item.Title ?? string.Empty,
            Uri: item.Url ?? string.Empty,
            PostedBy: item.By ?? string.Empty,
            PostedAt: DateTimeOffset.FromUnixTimeSeconds(item.Time),
            Score: item.Score,
            CommentCount: item.Descendants ?? 0);
    }
}
