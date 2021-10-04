namespace Elskom.Check;

public class WorkloadSettings : CommandSettings
{
    [CommandOption("--sdk")]
    public string? SdkVersion { get; set; }
}
