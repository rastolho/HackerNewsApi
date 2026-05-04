namespace HackerNews.Infrastructure.Configuration;

public sealed class HackerNewsConfig
{
    public const string SectionName = "HackerNews";

    public string BaseUrl { get; init; } = "https://hacker-news.firebaseio.com/v0/";
    public int HttpTimeoutSeconds { get; init; } = 10;
    public int IdListCacheSeconds { get; init; } = 60;
    public int ItemCacheMinutes { get; init; } = 10;
    public int CacheWarmIntervalSeconds { get; init; } = 60;
    public int WarmTopN { get; init; } = 200;
    public bool EnableCacheWarmer { get; init; } = true;
}
