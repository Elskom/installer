namespace Elskom.Check;

internal class InstallCommand : AsyncCommand<WorkloadSettings>
{
    public override async Task<int> ExecuteAsync([NotNull] CommandContext context, [NotNull] WorkloadSettings settings)
    {
        DotNetSdkHelper.GetOrUpdateSdkVersion(ref settings);
        var workloadInfos = await NuGetHelper.ResolveWildcardWorkloadPackageVersionsAsync(settings.RuntimeIdentifier!).ConfigureAwait(false);
        await workloadInfos.InstallAsync(settings.SdkVersion!, settings.RuntimeIdentifier!).ConfigureAwait(false);
        return 0;
    }
}
