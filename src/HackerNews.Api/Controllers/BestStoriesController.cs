using HackerNews.Api.Models;
using HackerNews.Application.Configuration;
using HackerNews.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.Extensions.Options;

namespace HackerNews.Api.Controllers;

/// <summary>
/// Endpoints for retrieving Hacker News best stories.
/// </summary>
[ApiController]
[Route("api/best-stories")]
[Produces("application/json")]
[Tags("Stories")]
public sealed class BestStoriesController(
    IBestStoriesService bestStoriesService,
    IOptions<BestStoriesConfig> options) : ControllerBase
{
    /// <summary>
    /// Returns the top <paramref name="count"/> Hacker News best stories,
    /// ordered by <c>score</c> descending.
    /// </summary>
    /// <remarks>
    /// Responses are served from a multi-layer cache (HTTP output cache,
    /// per-ID-list cache, per-item cache), so consecutive requests do not hit
    /// the upstream Hacker News API.
    ///
    /// **Sample request:**
    ///
    ///     GET /api/best-stories/3
    ///
    /// **Sample response:**
    ///
    ///     [
    ///       {
    ///         "title": "A uBlock Origin update was rejected from the Chrome Web Store",
    ///         "uri": "https://github.com/uBlockOrigin/uBlock-issues/issues/745",
    ///         "postedBy": "ismaildonmez",
    ///         "time": "2019-10-12T13:43:01+00:00",
    ///         "score": 1716,
    ///         "commentCount": 572
    ///       }
    ///     ]
    /// </remarks>
    /// <param name="count">How many stories to return. Must be between 1 and the configured maximum (default 200).</param>
    /// <param name="ct">Cancellation token, propagated all the way down to the upstream HTTP call.</param>
    /// <response code="200">The requested stories, ordered by score descending. May contain fewer items than requested if the upstream returns fewer.</response>
    /// <response code="400">The <c>count</c> parameter is out of range.</response>
    [HttpGet("{count:int}", Name = "GetBestStories")]
    [EndpointSummary("Get the top N Hacker News best stories")]
    [EndpointDescription("Returns the top N Hacker News best stories ordered by score (descending). Heavily cached upstream.")]
    [OutputCache(Duration = 15, VaryByRouteValueNames = new[] { "count" })]
    [ProducesResponseType(typeof(IReadOnlyList<StoryResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyList<StoryResponse>>> Get(
        [FromRoute] int count,
        CancellationToken ct)
    {
        var max = options.Value.MaxStories;
        if (count <= 0 || count > max)
        {
            ModelState.AddModelError(nameof(count), $"count must be between 1 and {max}.");
            return ValidationProblem(ModelState);
        }

        var stories = await bestStoriesService.GetBestStoriesAsync(count, ct);
        return Ok(stories.Select(StoryResponse.From).ToList());
    }
}
