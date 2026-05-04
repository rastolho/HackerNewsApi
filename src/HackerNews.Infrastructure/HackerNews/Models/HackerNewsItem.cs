using System.Text.Json.Serialization;

namespace HackerNews.Infrastructure.HackerNews.Models;

internal sealed record HackerNewsItem(
    [property: JsonPropertyName("id")] long Id,
    [property: JsonPropertyName("type")] string? Type,
    [property: JsonPropertyName("by")] string? By,
    [property: JsonPropertyName("time")] long Time,
    [property: JsonPropertyName("title")] string? Title,
    [property: JsonPropertyName("url")] string? Url,
    [property: JsonPropertyName("score")] int Score,
    [property: JsonPropertyName("descendants")] int? Descendants);
