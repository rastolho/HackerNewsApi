using HackerNews.Domain.Stories;
using HackerNews.Infrastructure.Configuration;
using HackerNews.Infrastructure.HackerNews;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace HackerNews.Api.Tests.Infrastructure;

/// <summary>
/// Tests the caching decorator wired around an HTTP client. We can't mock the
/// concrete <see cref="HackerNewsHttpClient"/>, so each test wires a fake
/// HttpMessageHandler that counts upstream hits.
/// </summary>
public class CachedHackerNewsClientTests
{
    [Fact]
    public async Task Repeated_id_list_calls_hit_upstream_once_within_ttl()
    {
        var (sut, handler) = Build();

        await sut.GetBestStoryIdsAsync(CancellationToken.None);
        await sut.GetBestStoryIdsAsync(CancellationToken.None);
        await sut.GetBestStoryIdsAsync(CancellationToken.None);

        handler.HitCount("beststories.json").Should().Be(1);
    }

    [Fact]
    public async Task Repeated_item_calls_hit_upstream_once_per_id_within_ttl()
    {
        var (sut, handler) = Build();

        await sut.GetStoryAsync(1, CancellationToken.None);
        await sut.GetStoryAsync(1, CancellationToken.None);
        await sut.GetStoryAsync(2, CancellationToken.None);

        handler.HitCount("item/1.json").Should().Be(1);
        handler.HitCount("item/2.json").Should().Be(1);
    }

    private static (CachedHackerNewsClient Sut, CountingHandler Handler) Build()
    {
        var handler = new CountingHandler();
        var http = new HttpClient(handler) { BaseAddress = new Uri("https://hacker-news.firebaseio.com/v0/") };
        var inner = new HackerNewsHttpClient(http);

        var sc = new ServiceCollection();
#pragma warning disable EXTEXP0018
        sc.AddHybridCache();
#pragma warning restore EXTEXP0018
        var cache = sc.BuildServiceProvider().GetRequiredService<HybridCache>();

        var options = Options.Create(new HackerNewsConfig());
        var sut = new CachedHackerNewsClient(inner, cache, options);
        return (sut, handler);
    }

    private sealed class CountingHandler : HttpMessageHandler
    {
        private readonly Dictionary<string, int> _hits = new();

        public int HitCount(string pathSuffix) =>
            _hits.Where(kvp => kvp.Key.EndsWith(pathSuffix, StringComparison.Ordinal)).Sum(kvp => kvp.Value);

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var path = request.RequestUri!.AbsolutePath;
            _hits[path] = _hits.GetValueOrDefault(path) + 1;

            string json = path.EndsWith("beststories.json", StringComparison.Ordinal)
                ? "[1, 2, 3]"
                : """{"id":1,"type":"story","by":"alice","time":1700000000,"title":"T","url":"https://x","score":42,"descendants":7}""";

            return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
            });
        }
    }
}
