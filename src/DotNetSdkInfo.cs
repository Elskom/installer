namespace Elskom.Check;

internal class DotNetSdkInfo
{
    internal DotNetSdkInfo(string version, string directory)
        : this(version, new DirectoryInfo(directory))
    {
    }

    internal DotNetSdkInfo(string version, DirectoryInfo directory)
    {
        Version = version;
        Directory = directory;
    }

    internal string Version { get; set; }

    internal DirectoryInfo Directory { get; set; }
}
