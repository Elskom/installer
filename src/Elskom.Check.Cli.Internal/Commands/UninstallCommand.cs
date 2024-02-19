namespace Elskom.Check;

internal class UninstallCommand : AsyncCommand<WorkloadSettings>
{
    public override async Task<int> ExecuteAsync([NotNull] CommandContext context, [NotNull] WorkloadSettings settings)
    {
        DotNetSdkHelper.GetOrUpdateSdkVersion(ref settings);
        var workloadInfos = await NuGetHelper.ResolveWildcardWorkloadPackageVersionsAsync(settings.RuntimeIdentifier!).ConfigureAwait(false);
        workloadInfos.Uninstall(settings.SdkVersion!, settings.RuntimeIdentifier!);
        return 0;
    }
}
