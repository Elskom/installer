namespace Elskom.Check;

public class UninstallCommand : Command<WorkloadSettings>
{
    public override int Execute([NotNull] CommandContext context, [NotNull] WorkloadSettings settings)
    {
        DotNetSdkHelper.GetOrUpdateSdkVersion(ref settings);
        return Uninstall(settings.SdkVersion!);
    }

    private static int Uninstall(string sdkVersion)
    {
        // var sdkPackVersion = NuGetHelper.ResolveWildcardPackageVersion(
        //     Constants.SdkPackName);
        // var refPackVersion = NuGetHelper.ResolveWildcardPackageVersion(
        //     Constants.RefPackName);
        // var runtimePackVersion = NuGetHelper.ResolveWildcardPackageVersion(
        //     Constants.RuntimePackName);
        // var templatePackVersion = NuGetHelper.ResolveWildcardPackageVersion(
        //     Constants.TemplatePackName);
        var sdkPackVersion = DotNetSdkHelper.GetInstalledDotNetSdkWorkloadPackVersion(
            Constants.SdkPackName);
        var refPackVersion = DotNetSdkHelper.GetInstalledDotNetSdkWorkloadPackVersion(
            Constants.RefPackName);
        var runtimePackVersion = DotNetSdkHelper.GetInstalledDotNetSdkWorkloadPackVersion(
            Constants.RuntimePackName);
        // delete the directories to the workload.
        UninstallManifest(sdkVersion);

        // delete the directories to the workload.
        UninstallPackage(Constants.SdkPackName, sdkPackVersion);
        UninstallPackage(Constants.RefPackName, refPackVersion);
        UninstallPackage(Constants.RuntimePackName, runtimePackVersion);
        _ = NuGetHelper.DeletePackage(
            Constants.TemplatePackName,
            DotNetSdkHelper.GetDotNetSdkWorkloadTemplatePacksFolder());
        return 0;
    }

    internal static void UninstallPackage(string packName, string packVersion)
    {
        if (!string.IsNullOrEmpty(packVersion))
        {
            var packFolder = DotNetSdkHelper.GetDotNetSdkWorkloadPacksFolder(
                packName,
                packVersion);
            Directory.Delete(packFolder, true);
        }
    }
    
    private static void UninstallManifest(string sdkVersion)
    {
        var workloadFolder = DotNetSdkHelper.GetDotNetSdkWorkloadFolder(
            Constants.WorkloadName,
            sdkVersion);
        if (Directory.Exists(workloadFolder))
        {
            Directory.Delete(workloadFolder, true);
        }
    }
}
