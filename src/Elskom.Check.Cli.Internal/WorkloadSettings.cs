namespace Elskom.Check;

internal class WorkloadSettings : CommandSettings
{
    [CommandOption("--sdk")]
    public string? SdkVersion { get; set; }

    [CommandOption("--rid")]
    public string? RuntimeIdentifier { get; set; }
}
