using System.Net;
using System.Net.Http.Json;
using HackerNews.Api.Models;
using HackerNews.Application.Interfaces;
using HackerNews.Domain.Stories;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace HackerNews.Api.Tests.Api;

public class BestStoriesEndpointTests : IClassFixture<BestStoriesEndpointTests.Factory>
{
    private readonly Factory _factory;

    public BestStoriesEndpointTests(Factory factory) => _factory = factory;

    [Fact]
    public async Task Returns_top_n_stories_in_descending_score_order()
    {
        _factory.Client.GetBestStoryIdsAsync(Arg.Any<CancellationToken>())
            .Returns(new long[] { 1, 2, 3 });
        _factory.Client.GetStoryAsync(1, Arg.Any<CancellationToken>())
            .Returns(new Story(1, "T1", "https://1", "a", DateTimeOffset.FromUnixTimeSeconds(1700000000), 50, 5));
        _factory.Client.GetStoryAsync(2, Arg.Any<CancellationToken>())
            .Returns(new Story(2, "T2", "https://2", "b", DateTimeOffset.FromUnixTimeSeconds(1700000000), 100, 10));
        _factory.Client.GetStoryAsync(3, Arg.Any<CancellationToken>())
            .Returns(new Story(3, "T3", "https://3", "c", DateTimeOffset.FromUnixTimeSeconds(1700000000), 75, 7));

        using var http = _factory.CreateClient();
        var response = await http.GetAsync("/api/best-stories/3");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var stories = await response.Content.ReadFromJsonAsync<List<StoryResponse>>();
        stories.Should().NotBeNull();
        stories!.Select(s => s.Score).Should().ContainInOrder(100, 75, 50);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    [InlineData(99999)]
    public async Task Rejects_invalid_count(int count)
    {
        using var http = _factory.CreateClient();
        var response = await http.GetAsync($"/api/best-stories/{count}");
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    public sealed class Factory : WebApplicationFactory<Program>
    {
        public IHackerNewsClient Client { get; } = Substitute.For<IHackerNewsClient>();

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");
            builder.ConfigureAppConfiguration((_, cfg) =>
            {
                cfg.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["HackerNews:EnableCacheWarmer"] = "false"
                });
            });
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IHackerNewsClient>();
                services.AddSingleton(Client);
            });
        }
    }
}
