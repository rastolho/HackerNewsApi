using System.Diagnostics;

namespace HackerNews.Application.Diagnostics;

public static class ApplicationDiagnostics
{
    public const string SourceName = "HackerNews.Application";
    public static readonly ActivitySource ActivitySource = new(SourceName);
}
