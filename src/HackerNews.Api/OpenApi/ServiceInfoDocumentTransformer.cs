using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi.Models;

namespace HackerNews.Api.OpenApi;

/// <summary>
/// Populates document-level OpenAPI metadata (title, description, contact,
/// license, server URLs). Runs once per OpenAPI document generation.
/// </summary>
internal sealed class ServiceInfoDocumentTransformer : IOpenApiDocumentTransformer
{
    public Task TransformAsync(
        OpenApiDocument document,
        OpenApiDocumentTransformerContext context,
        CancellationToken cancellationToken)
    {
        document.Info = new OpenApiInfo
        {
            Title = "Hacker News — Best Stories API",
            Version = "v1",
            Description = """
                A small ASP.NET Core (.NET 9) Web API that exposes the top *N*
                stories from the public [Hacker News API](https://github.com/HackerNews/API),
                ordered by score (descending).

                ### Caching strategy
                Three cooperating layers protect the upstream API from being overloaded:
                - **Output cache** — short (~15 s) at the HTTP edge, keyed by `count`.
                - **HybridCache (IDs)** — ~60 s TTL on the best-stories ID list.
                - **HybridCache (items)** — ~10 min TTL per individual story.
                A `BackgroundService` warms the cache every 60 s so user requests
                almost always hit a populated cache.

                ### Resilience
                The upstream HTTP client is wrapped with the standard .NET 9 resilience
                pipeline: per-attempt timeout, retry with jitter, circuit breaker,
                and a total request timeout.
                """,
            Contact = new OpenApiContact
            {
                Name = "Hacker News Best Stories API",
                Url = new Uri("https://github.com/HackerNews/API")
            },
            License = new OpenApiLicense
            {
                Name = "MIT"
            }
        };

        document.Tags =
        [
            new OpenApiTag
            {
                Name = "Stories",
                Description = "Endpoints exposing Hacker News best stories."
            }
        ];

        return Task.CompletedTask;
    }
}
