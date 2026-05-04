using HackerNews.Domain.Stories;

namespace HackerNews.Application.Interfaces;

/// <summary>
/// Port for the upstream Hacker News data source. Implementations live in
/// the Infrastructure layer; the Application layer never sees raw upstream
/// shapes (item types, descendants, deleted flags, etc.).
/// </summary>
public interface IHackerNewsClient
{
    /// <summary>
    /// Returns the ordered list of "best stories" IDs as published by Hacker News.
    /// </summary>
    Task<IReadOnlyList<long>> GetBestStoryIdsAsync(CancellationToken ct);

    /// <summary>
    /// Returns the story for <paramref name="id"/>, or <c>null</c> if the item
    /// is not a story (job, poll, comment) or has been deleted.
    /// </summary>
    Task<Story?> GetStoryAsync(long id, CancellationToken ct);
}
