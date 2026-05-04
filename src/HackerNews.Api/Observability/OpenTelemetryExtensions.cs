using HackerNews.Application.Diagnostics;
using HackerNews.Infrastructure.Diagnostics;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace HackerNews.Api.Observability;

public static class OpenTelemetryExtensions
{
    private const string ServiceName = "HackerNews.Api";

    public static WebApplicationBuilder AddObservability(this WebApplicationBuilder builder)
    {
        var serviceVersion = typeof(Program).Assembly.GetName().Version?.ToString() ?? "0.0.0";

        var hasOtlpEndpoint = !string.IsNullOrWhiteSpace(
            Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT"));

        builder.Logging.AddOpenTelemetry(logging =>
        {
            logging.SetResourceBuilder(ResourceBuilder.CreateDefault()
                .AddService(ServiceName, serviceVersion: serviceVersion));
            logging.IncludeFormattedMessage = true;
            logging.IncludeScopes = true;
            if (hasOtlpEndpoint) logging.AddOtlpExporter();
        });

        builder.Services.AddOpenTelemetry()
            .ConfigureResource(r => r.AddService(ServiceName, serviceVersion: serviceVersion))
            .WithTracing(tracing =>
            {
                tracing
                    .AddSource(ApplicationDiagnostics.SourceName)
                    .AddSource(InfrastructureDiagnostics.SourceName)
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation();
                if (hasOtlpEndpoint) tracing.AddOtlpExporter();
            })
            .WithMetrics(metrics =>
            {
                metrics
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddRuntimeInstrumentation()
                    .AddMeter("Microsoft.Extensions.Http.Resilience")
                    .AddMeter("Polly");
                if (hasOtlpEndpoint) metrics.AddOtlpExporter();
            });

        return builder;
    }
}
