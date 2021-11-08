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
        var runtimePackVersion = DotNetSdkHelper.GetInstalledDotNetSdkWorkloadRuntimePackVersion(
            Constants.RuntimePackName);

        // delete the directories to the workload.
        _ = UninstallManifest(sdkVersion);

        // delete the directories to the workload.
        if (UninstallPackage(
            Constants.SdkPackName,
            sdkPackVersion,
            DotNetSdkHelper.GetDotNetSdkWorkloadPacksFolder(),
            sdkVersion))
        {
            Console.WriteLine($"Successfully uninstalled workload package '{Constants.SdkPackName}'.");
        }
        else
        {
            Console.WriteLine($"Workload package '{Constants.SdkPackName}' was already uninstalled.");
        }

        if (UninstallPackage(
            Constants.RefPackName,
            refPackVersion,
            DotNetSdkHelper.GetDotNetSdkWorkloadPacksFolder(),
            sdkVersion))
        {
            Console.WriteLine($"Successfully uninstalled workload package '{Constants.RefPackName}'.");
        }
        else
        {
            Console.WriteLine($"Workload package '{Constants.RefPackName}' was already uninstalled.");
        }

        if (UninstallPackage(
            Constants.RuntimePackName,
            runtimePackVersion,
            DotNetSdkHelper.GetDotNetSdkWorkloadRuntimePacksFolder(),
            sdkVersion))
        {
            Console.WriteLine($"Successfully uninstalled workload package '{Constants.RuntimePackName}'.");
        }
        else
        {
            Console.WriteLine($"Workload package '{Constants.RuntimePackName}' was already uninstalled.");
        }

        var templateUninstallCommand = new ProcessStartOptions
        {
            WaitForProcessExit = true,
        }.WithStartInformation(
            $"{DotNetSdkHelper.GetDotNetSdkLocation()}{Path.DirectorySeparatorChar}dotnet{(OperatingSystem.IsWindows() ? ".exe" : string.Empty)}",
            $"new -u {Constants.TemplatePackName}",
            true,
            true,
            false,
            true,
            ProcessWindowStyle.Hidden,
            Environment.CurrentDirectory);
        var result = templateUninstallCommand.Start();
        if (result.Contains("The template package '") && result.Contains("' is not found."))
        {
            Console.WriteLine($"Workload package '{Constants.TemplatePackName}' was already uninstalled.");
        }
        else
        {
            Console.WriteLine($"Successfully uninstalled workload package '{Constants.TemplatePackName}'.");
        }

        return 0;
    }

    internal static bool UninstallPackage(string packName, string packVersion, string packFolder, string sdkVersion)
    {
        if (!string.IsNullOrEmpty(packVersion))
        {
            Directory.Delete(Path.Join(packFolder, packName), true);
            if (!packName.Equals(Constants.RuntimePackName))
            {
                File.Delete(
                    DotNetSdkHelper.GetDotNetSdkWorkloadMetadataInstalledPacks(
                        Constants.SdkPackName,
                        packVersion,
                        sdkVersion));
            }

            return true;
        }

        return false;
    }
    
    private static bool UninstallManifest(string sdkVersion)
    {
        var workloadFolder = DotNetSdkHelper.GetDotNetSdkWorkloadFolder(
            Constants.WorkloadName,
            sdkVersion);
        if (Directory.Exists(workloadFolder))
        {
            Directory.Delete(workloadFolder, true);
            File.Delete(DotNetSdkHelper.GetDotNetSdkWorkloadMetadataInstalledWorkloads(Constants.WorkloadName, sdkVersion));
            return true;
        }

        return false;
    }
}
