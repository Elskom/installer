namespace Elskom.Check;

public class UpdateCommand : AsyncCommand<WorkloadSettings>
{
    public override async Task<int> ExecuteAsync([NotNull] CommandContext context, [NotNull] WorkloadSettings settings)
    {
        DotNetSdkHelper.GetOrUpdateSdkVersion(ref settings);
        return await UpdateAsync(settings.SdkVersion!).ConfigureAwait(false);
    }

    private static async Task<int> UpdateAsync(string sdkVersion)
    {
        var currentSdkPackVersion = await NuGetHelper.ResolveWildcardPackageVersionAsync(
            Constants.SdkPackName).ConfigureAwait(false);
        var currentRefPackVersion = await NuGetHelper.ResolveWildcardPackageVersionAsync(
            Constants.RefPackName).ConfigureAwait(false);
        var currentRuntimePackVersion = await NuGetHelper.ResolveWildcardPackageVersionAsync(
            Constants.RuntimePackName).ConfigureAwait(false);
        var currentTemplatePackVersion = await NuGetHelper.ResolveWildcardPackageVersionAsync(
            Constants.TemplatePackName).ConfigureAwait(false);
        var packVersions = new Dictionary<string, string>
        {
            { Constants.SdkPackName, currentSdkPackVersion },
            { Constants.RefPackName, currentRefPackVersion },
            { Constants.RuntimePackName, currentRuntimePackVersion },
            { Constants.TemplatePackName, currentTemplatePackVersion },
        };
        return await UpdateManifestAsync(packVersions, sdkVersion).ConfigureAwait(false);
    }

    private static async Task<int> UpdateManifestAsync(IReadOnlyDictionary<string, string> packVersions, string sdkVersion)
    {
        var workloadFolder = DotNetSdkHelper.GetDotNetSdkWorkloadFolder(
            Constants.WorkloadName,
            sdkVersion);
        var text = await File.ReadAllTextAsync(
            Path.Join(
                workloadFolder,
                "WorkloadManifest.json")).ConfigureAwait(false);
        var workloadManifest = JsonSerializer.Deserialize<WorkloadManifest>(text);
        var sdkPackUpdated = await UpdateWorkloadPackAsync(
            packVersions,
            workloadManifest!.Packs.ElskomSdk,
            Constants.SdkPackName,
            sdkVersion,
            DotNetSdkHelper.GetDotNetSdkWorkloadPacksFolder()).ConfigureAwait(false);
        if (sdkPackUpdated)
        {
            workloadManifest.Version =
                packVersions.GetValueOrDefault(Constants.SdkPackName)!;
        }

        var refPackUpdated = false;
        var runtimePackUpdated = false;
        var installedRefPackVersion = DotNetSdkHelper.GetInstalledDotNetSdkWorkloadPackVersion(
            Constants.RefPackName);
        var installedRuntimePackVersion = DotNetSdkHelper.GetInstalledDotNetSdkWorkloadRuntimePackVersion(
            Constants.RuntimePackName);
        if (!installedRefPackVersion.Equals(string.Empty))
        {
            if (installedRefPackVersion.EndsWith("-dev")
                || DotNetSdkHelper.ConvertVersionToNuGetVersion(installedRefPackVersion)
                > DotNetSdkHelper.ConvertVersionToNuGetVersion(workloadManifest.Packs.ElskomSdkAppRef.Version))
            {
                Console.WriteLine("Picked up newer installed reference pack, using that instead.");
                workloadManifest.Packs.ElskomSdkAppRef.UpdateVersion(installedRefPackVersion);
                refPackUpdated = true;
            }
        }

        if (!installedRuntimePackVersion.Equals(string.Empty))
        {
            if (installedRuntimePackVersion.EndsWith("-dev")
                || DotNetSdkHelper.ConvertVersionToNuGetVersion(installedRuntimePackVersion)
                > DotNetSdkHelper.ConvertVersionToNuGetVersion(workloadManifest.Packs.ElskomSdkApp.Version))
            {
                Console.WriteLine("Picked up newer installed runtime pack, using that instead.");
                workloadManifest.Packs.ElskomSdkApp.UpdateVersion(installedRuntimePackVersion);
                runtimePackUpdated = true;
            }
        }

        if (!refPackUpdated)
        {
            refPackUpdated = await UpdateWorkloadPackAsync(
                packVersions,
                workloadManifest.Packs.ElskomSdkAppRef,
                Constants.RefPackName,
                sdkVersion,
                DotNetSdkHelper.GetDotNetSdkWorkloadPacksFolder()).ConfigureAwait(false);
        }

        if (!runtimePackUpdated)
        {
            runtimePackUpdated = await UpdateWorkloadPackAsync(
                packVersions,
                workloadManifest.Packs.ElskomSdkApp,
                Constants.RuntimePackName,
                sdkVersion,
                DotNetSdkHelper.GetDotNetSdkWorkloadRuntimePacksFolder()).ConfigureAwait(false);
        }

        var templatePackUpdated = await UpdateWorkloadTemplatePackAsync(
            packVersions,
            workloadManifest.Packs.ElskomSdkTemplates,
            Constants.TemplatePackName).ConfigureAwait(false);
        if (sdkPackUpdated)
        {
            Console.WriteLine($"Workload Manifest is now version: '{workloadManifest.Version}'.");
            Console.WriteLine($"Workload Sdk is now version: '{workloadManifest.Packs.ElskomSdk.Version}'.");
        }

        if (runtimePackUpdated)
        {
            Console.WriteLine($"Workload Runtime Pack is now version: '{workloadManifest.Packs.ElskomSdkApp.Version}'.");
        }

        if (refPackUpdated)
        {
            Console.WriteLine($"Workload Reference Pack is now version: '{workloadManifest.Packs.ElskomSdkAppRef.Version}'.");
        }

        if (templatePackUpdated)
        {
            Console.WriteLine($"Workload Template Pack is now version: '{workloadManifest.Packs.ElskomSdkTemplates.Version}'.");
        }

        if (sdkPackUpdated || runtimePackUpdated || refPackUpdated || templatePackUpdated)
        {
            workloadManifest.WriteJsonFile(sdkVersion);
            workloadManifest.WriteTargetsFile(sdkVersion);
        }

        return 0;
    }

    private static async Task<bool> UpdateWorkloadPackAsync(
        IReadOnlyDictionary<string, string> packVersions,
        WorkloadManifest.WorkloadPacks.WorkloadPack workloadPack,
        string packName,
        string sdkVersion,
        string outputPath)
    {
        if (!workloadPack.Version.Equals(
            packVersions.GetValueOrDefault(packName)!))
        {
            Console.WriteLine($"Update found for workload package '{packName}'.");
            UninstallCommand.UninstallPackage(
                packName,
                workloadPack.Version,
                outputPath,
                sdkVersion);
            workloadPack.UpdateVersion(
                packVersions.GetValueOrDefault(packName)!);
            await InstallCommand.InstallPackageAsync(
                packName,
                workloadPack.Version,
                string.Empty,
                sdkVersion,
                outputPath).ConfigureAwait(false);
            Console.WriteLine($"Successfully updated workload package '{packName}'.");
            return true;
        }

        Console.WriteLine($"No updates found for workload package '{packName}'.");
        return false;
    }

    private static async Task<bool> UpdateWorkloadTemplatePackAsync(
        IReadOnlyDictionary<string, string> packVersions,
        WorkloadManifest.WorkloadPacks.WorkloadPack workloadPack,
        string packName)
    {
        if (!workloadPack.Version.Equals(
            packVersions.GetValueOrDefault(packName)))
        {
            Console.WriteLine($"Update found for workload package '{packName}'.");
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
            var packVersion = await InstallCommand.DownloadPackageAsync(
                packName).ConfigureAwait(false);
            workloadPack.UpdateVersion(packVersion);
            Console.WriteLine($"Successfully updated workload package '{packName}'.");
            return true;
        }

        Console.WriteLine($"No updates found for workload package '{packName}'.");
        return false;
    }
}
