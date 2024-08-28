namespace Elskom.Check;

internal static class EnumerableExtensions
{
    public static DirectoryInfo? MaxByDevVersion(this IEnumerable<DirectoryInfo> directoryInfos)
    {
        DirectoryInfo? info = null;
        foreach (var directoryInfo in directoryInfos)
        {
            if (directoryInfo.Name.EndsWith("-dev"))
            {
                info = directoryInfo;
            }
        }

        info ??= directoryInfos.MaxBy(item => DotNetSdkHelper.ConvertVersionToNuGetVersion(item.Name));
        return info;
    }
}
