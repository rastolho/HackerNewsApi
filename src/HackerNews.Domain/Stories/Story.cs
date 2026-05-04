namespace HackerNews.Domain.Stories;

/// <summary>
/// Represents a Hacker News story in the application's own domain — decoupled
/// from the upstream Hacker News JSON shape. Non-stories (jobs, polls,
/// comments) and deleted items are filtered out before reaching this layer.
/// </summary>
/// <param name="Id">Hacker News item id.</param>
/// <param name="Title">Story title.</param>
/// <param name="Uri">URL the story points to. Empty for "Ask HN" / "Tell HN" stories without an external link.</param>
/// <param name="PostedBy">Username of the submitter.</param>
/// <param name="PostedAt">Submission timestamp (UTC).</param>
/// <param name="Score">Current HN score.</param>
/// <param name="CommentCount">Total reply count (HN's <c>descendants</c>).</param>
public sealed record Story(
    long Id,
    string Title,
    string Uri,
    string PostedBy,
    DateTimeOffset PostedAt,
    int Score,
    int CommentCount);
