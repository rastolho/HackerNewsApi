using HackerNews.Application.Configuration;
using HackerNews.Application.Interfaces;
using HackerNews.Application.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace HackerNews.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddHackerNewsApplication(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services
            .AddOptions<BestStoriesConfig>()
            .Bind(configuration.GetSection(BestStoriesConfig.SectionName));

        services.AddScoped<IBestStoriesService, BestStoriesService>();

        return services;
    }
}
