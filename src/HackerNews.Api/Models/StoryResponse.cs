using System.ComponentModel;
using System.Text.Json.Serialization;
using HackerNews.Domain.Stories;

namespace HackerNews.Api.Models;

/// <summary>
/// A single Hacker News story as exposed by this API. The shape is decoupled
/// from the upstream Hacker News JSON — for example, HN's <c>descendants</c>
/// is mapped to <see cref="CommentCount"/>, and unix <c>time</c> to an ISO 8601
/// <see cref="DateTimeOffset"/>.
/// </summary>
public sealed record StoryResponse(
    [property: JsonPropertyName("title")]
    [property: Description("The story title.")]
    string Title,

    [property: JsonPropertyName("uri")]
    [property: Description("The URL the story points to.")]
    string Uri,

    [property: JsonPropertyName("postedBy")]
    [property: Description("Username of the Hacker News account that submitted the story.")]
    string PostedBy,

    [property: JsonPropertyName("time")]
    [property: Description("When the story was submitted, in ISO 8601 with offset (UTC).")]
    DateTimeOffset Time,

    [property: JsonPropertyName("score")]
    [property: Description("Current Hacker News score (sum of upvotes minus downvotes).")]
    int Score,

    [property: JsonPropertyName("commentCount")]
    [property: Description("Total number of replies (HN's `descendants` field), not just top-level comments.")]
    int CommentCount)
{
    /// <summary>Maps a domain <see cref="Story"/> to its API representation.</summary>
    public static StoryResponse From(Story story) => new(
        Title: story.Title,
        Uri: story.Uri,
        PostedBy: story.PostedBy,
        Time: story.PostedAt,
        Score: story.Score,
        CommentCount: story.CommentCount);
}
