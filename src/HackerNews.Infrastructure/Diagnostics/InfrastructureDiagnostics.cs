using System.Diagnostics;

namespace HackerNews.Infrastructure.Diagnostics;

public static class InfrastructureDiagnostics
{
    public const string SourceName = "HackerNews.Infrastructure";
    public static readonly ActivitySource ActivitySource = new(SourceName);
}
