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
        if (await UpdateWorkloadPackAsync(
            packVersions,
            workloadManifest!.Packs.ElskomSdk,
            Constants.SdkPackName, sdkVersion).ConfigureAwait(false))
        {
            workloadManifest.Version =
                packVersions.GetValueOrDefault(Constants.SdkPackName)!;
        }

        _ = await UpdateWorkloadPackAsync(
            packVersions,
            workloadManifest.Packs.ElskomSdkAppRef,
            Constants.RefPackName,
            sdkVersion).ConfigureAwait(false);
        _ = await UpdateWorkloadPackAsync(
            packVersions,
            workloadManifest.Packs.ElskomSdkApp,
            Constants.RuntimePackName,
            sdkVersion).ConfigureAwait(false);
        _ = await UpdateWorkloadTemplatePackAsync(
            packVersions,
            workloadManifest.Packs.ElskomSdkTemplates,
            Constants.TemplatePackName,
            sdkVersion).ConfigureAwait(false);
        Console.WriteLine($"Workload Manifest is now version: '{workloadManifest.Version}'.");
        Console.WriteLine($"Workload Sdk is now version: '{workloadManifest.Packs.ElskomSdk.Version}'.");
        Console.WriteLine($"Workload Runtime and Reference Packs is now version: runtime:'{workloadManifest.Packs.ElskomSdkApp}', ref:'{workloadManifest.Packs.ElskomSdkAppRef}'.");
        workloadManifest.WriteJsonFile(sdkVersion);
        return 0;
    }

    private static async Task<bool> UpdateWorkloadPackAsync(
        IReadOnlyDictionary<string, string> packVersions,
        WorkloadManifest.WorkloadPacks.WorkloadPack workloadPack,
        string packName,
        string sdkVersion)
    {
        if (!workloadPack.Version.Equals(
            packVersions.GetValueOrDefault(packName)!))
        {
            Console.WriteLine($"Update found for workload package '{packName}'.");
            UninstallCommand.UninstallPackage(
                packName,
                workloadPack.Version);
            workloadPack.UpdateVersion(
                packVersions.GetValueOrDefault(packName)!);
            await InstallCommand.InstallPackageAsync(
                packName,
                workloadPack.Version,
                string.Empty,
                sdkVersion).ConfigureAwait(false);
            Console.WriteLine($"Successfully updated workload package '{packName}'.");
            return true;
        }

        Console.WriteLine($"No updates found for workload package '{Constants.RuntimePackName}'.");
        return false;
    }

    private static async Task<bool> UpdateWorkloadTemplatePackAsync(
        IReadOnlyDictionary<string, string> packVersions,
        WorkloadManifest.WorkloadPacks.WorkloadPack workloadPack,
        string packName,
        string sdkVersion)
    {
        if (!workloadPack.Version.Equals(
            packVersions.GetValueOrDefault(packName)))
        {
            Console.WriteLine($"Update found for workload package '{packName}'.");
            NuGetHelper.DeletePackage(
                packName,
                DotNetSdkHelper.GetDotNetSdkWorkloadTemplatePacksFolder());
            workloadPack.UpdateVersion(
                packVersions.GetValueOrDefault(packName)!);
            await InstallCommand.DownloadPackageAsync(
                Constants.TemplatePackName,
                string.Empty,
                sdkVersion).ConfigureAwait(false);
            Console.WriteLine($"Successfully updated workload package '{packName}'.");
            return true;
        }

        Console.WriteLine($"No updates found for workload package '{packName}'.");
        return false;
    }
}
