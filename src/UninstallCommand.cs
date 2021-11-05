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
        var sdkPackVersion = DotNetSdkHelper.GetInstalledDotNetSdkWorkloadPackVersion(
            Constants.SdkPackName);
        var refPackVersion = DotNetSdkHelper.GetInstalledDotNetSdkWorkloadPackVersion(
            Constants.RefPackName);
        var runtimePackVersion = DotNetSdkHelper.GetInstalledDotNetSdkWorkloadPackVersion(
            Constants.RuntimePackName);

        // delete the directories to the workload.
        UninstallManifest(sdkVersion);

        // delete the directories to the workload.
        UninstallPackage(
            Constants.SdkPackName,
            sdkPackVersion,
            DotNetSdkHelper.GetDotNetSdkWorkloadPacksFolder());
        UninstallPackage(
            Constants.RefPackName,
            refPackVersion,
            DotNetSdkHelper.GetDotNetSdkWorkloadPacksFolder());
        UninstallPackage(
            Constants.RuntimePackName,
            runtimePackVersion,
            DotNetSdkHelper.GetDotNetSdkWorkloadRuntimePacksFolder());
        var templateUninstallCommand = new ProcessStartOptions
        {
            WaitForProcessExit = true,
        }.WithStartInformation(
            $"{DotNetSdkHelper.GetDotNetSdkLocation()}{Path.DirectorySeparatorChar}dotnet{(OperatingSystem.IsWindows() ? ".exe" : string.Empty)}",
            $"new -u {Constants.TemplatePackName}",
            false,
            false,
            false,
            true,
            ProcessWindowStyle.Hidden,
            Environment.CurrentDirectory);
        _ = templateUninstallCommand.Start();
        return 0;
    }

    internal static void UninstallPackage(string packName, string packVersion, string packFolder)
    {
        if (!string.IsNullOrEmpty(packVersion))
        {
            Directory.Delete(Path.Join(packFolder, packName), true);
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
