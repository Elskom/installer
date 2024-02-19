namespace Elskom.Check;

internal static class Constants
{
    internal const string WorkloadName = "elskom";
    internal const string SdkPackName = "Elskom.Sdk";
    internal const string RefPackName = "Elskom.Sdk.App.Ref";
    internal const string RuntimePackName = "Elskom.Sdk.App";
    internal const string TemplatePackName = "Elskom.Sdk.Templates";
    internal const string Sdk = "sdk";
    internal const string Framework = "framework";
    internal const string Template = "template";
    internal static readonly string[] RuntimePacks = [
        "Elskom.Sdk.App.Runtime.win-x86",
        "Elskom.Sdk.App.Runtime.win-x64",
        "Elskom.Sdk.App.Runtime.win-arm64",
        "Elskom.Sdk.App.Runtime.linux-x64",
        "Elskom.Sdk.App.Runtime.linux-arm",
        "Elskom.Sdk.App.Runtime.linux-arm64",
        "Elskom.Sdk.App.Runtime.osx-x64",
        "Elskom.Sdk.App.Runtime.osx-arm64"
    ];
}
