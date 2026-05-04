using HackerNews.Application.Configuration;
using HackerNews.Application.Interfaces;
using HackerNews.Application.Services;
using HackerNews.Domain.Stories;
using Microsoft.Extensions.Options;

namespace HackerNews.Api.Tests.Application;

public class BestStoriesServiceTests
{
    private static Story Story(long id, int score) => new(
        Id: id,
        Title: $"Title {id}",
        Uri: $"https://example.com/{id}",
        PostedBy: $"user{id}",
        PostedAt: DateTimeOffset.FromUnixTimeSeconds(1700000000 + id),
        Score: score,
        CommentCount: (int)(id % 50));

    private static IBestStoriesService CreateSut(
        IReadOnlyList<long> ids,
        Func<long, Story?> storyFactory,
        BestStoriesConfig? opts = null)
    {
        var client = Substitute.For<IHackerNewsClient>();
        client.GetBestStoryIdsAsync(Arg.Any<CancellationToken>()).Returns(ids);
        client.GetStoryAsync(Arg.Any<long>(), Arg.Any<CancellationToken>())
            .Returns(callInfo => Task.FromResult(storyFactory(callInfo.ArgAt<long>(0))));

        var options = Options.Create(opts ?? new BestStoriesConfig { FetchConcurrency = 4 });
        return new BestStoriesService(client, options);
    }

    [Fact]
    public async Task Returns_top_n_sorted_by_score_descending()
    {
        var ids = new long[] { 1, 2, 3, 4, 5 };
        var scores = new Dictionary<long, int> { [1] = 10, [2] = 50, [3] = 30, [4] = 90, [5] = 70 };
        var sut = CreateSut(ids, id => Story(id, scores[id]));

        var result = await sut.GetBestStoriesAsync(3, CancellationToken.None);

        result.Should().HaveCount(3);
        result.Select(r => r.Score).Should().ContainInOrder(90, 70, 50);
    }

    [Fact]
    public async Task Skips_null_results_from_port()
    {
        var ids = new long[] { 1, 2, 3 };
        var sut = CreateSut(ids, id => id == 2 ? null : Story(id, 50));

        var result = await sut.GetBestStoriesAsync(3, CancellationToken.None);

        result.Should().HaveCount(2);
    }

    [Fact]
    public async Task Empty_id_list_returns_empty_result()
    {
        var sut = CreateSut([], _ => null);

        var result = await sut.GetBestStoriesAsync(10, CancellationToken.None);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task Returns_fewer_than_requested_when_upstream_has_fewer_stories()
    {
        var ids = new long[] { 1, 2 };
        var sut = CreateSut(ids, id => Story(id, (int)id));

        var result = await sut.GetBestStoriesAsync(10, CancellationToken.None);

        result.Should().HaveCount(2);
    }

    [Fact]
    public async Task Caps_count_at_max_stories_option()
    {
        var ids = Enumerable.Range(1, 50).Select(i => (long)i).ToArray();
        var opts = new BestStoriesConfig { FetchConcurrency = 4, MaxStories = 5 };
        var sut = CreateSut(ids, id => Story(id, (int)id), opts);

        var result = await sut.GetBestStoriesAsync(100, CancellationToken.None);

        result.Should().HaveCount(5);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task Non_positive_count_returns_empty(int count)
    {
        var sut = CreateSut(new long[] { 1, 2 }, id => Story(id, 1));

        var result = await sut.GetBestStoriesAsync(count, CancellationToken.None);

        result.Should().BeEmpty();
    }
}
