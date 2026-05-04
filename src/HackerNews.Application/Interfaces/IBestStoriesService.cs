using HackerNews.Domain.Stories;

namespace HackerNews.Application.Interfaces;

/// <summary>
/// Application-layer service that returns the top <c>N</c> Hacker News stories
/// ordered by score (descending). Implementations are expected to:
/// <list type="bullet">
///   <item>Cap <c>count</c> at the configured <c>MaxStories</c>.</item>
///   <item>Skip items the upstream port cannot resolve (deleted / non-story).</item>
///   <item>Be safe to call concurrently — caching and concurrency control
///         live in the <see cref="IHackerNewsClient"/> port adapter.</item>
/// </list>
/// </summary>
public interface IBestStoriesService
{
    /// <summary>
    /// Returns the top <paramref name="count"/> stories ordered by score descending.
    /// May return fewer items than requested if the upstream has fewer stories
    /// available.
    /// </summary>
    /// <param name="count">Desired number of stories. Non-positive values yield an empty result.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<IReadOnlyList<Story>> GetBestStoriesAsync(int count, CancellationToken ct);
}
