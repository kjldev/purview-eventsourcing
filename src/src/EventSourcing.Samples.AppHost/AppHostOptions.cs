namespace Purview.EventSourcing.Samples.AppHost;

sealed class AppHostOptions
{
    public const string SectionName = "AppHost:Sample";

    public string DatabaseName { get; set; } = "EventSourcingSamples";
}
