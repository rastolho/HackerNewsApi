using HackerNews.Api.OpenApi;
using HackerNews.Application;
using HackerNews.Infrastructure;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// Composition root — each layer registers its own services via an extension method.
builder.Services.AddHackerNewsApplication(builder.Configuration);
builder.Services.AddHackerNewsInfrastructure(builder.Configuration);

builder.Services.AddControllers();
builder.Services.AddOutputCache();
builder.Services.AddProblemDetails();

builder.Services.AddOpenApi(options =>
{
    options.AddDocumentTransformer<ServiceInfoDocumentTransformer>();
});

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();

if (app.Environment.IsDevelopment())
{
    // Raw JSON document at /openapi/v1.json
    app.MapOpenApi();

    // Browsable UI at /scalar/v1
    app.MapScalarApiReference(options =>
    {
        options.WithTitle("Hacker News — Best Stories API");
        options.WithTheme(ScalarTheme.BluePlanet);
        options.WithDefaultHttpClient(ScalarTarget.CSharp, ScalarClient.HttpClient);
    });
}

app.UseOutputCache();
app.MapControllers();

app.MapGet("/", () => Results.Redirect("/scalar/v1")).ExcludeFromDescription();
app.MapGet("/health", () => Results.Ok(new { status = "ok" }))
    .WithSummary("Liveness probe")
    .WithDescription("Returns 200 with `{ status: \"ok\" }` if the host process is up.")
    .WithTags("Health");

app.Run();

public partial class Program;
