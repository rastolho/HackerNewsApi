namespace HackerNews.Application.Configuration;

public sealed class BestStoriesConfig
{
    public const string SectionName = "HackerNews";

    public int MaxStories { get; init; } = 200;
    public int FetchConcurrency { get; init; } = 10;
}
