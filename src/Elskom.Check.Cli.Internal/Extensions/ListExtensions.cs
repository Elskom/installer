namespace Elskom.Check;

internal static class ListExtensions
{
    internal static async Task InstallAsync(this List<WorkloadInfo> workloadInfos, string sdkVersion, string runtimeIdentifier)
    {
        _ = workloadInfos.CheckInstalledRefPackVersion();
        _ = workloadInfos.CheckInstalledRuntimePackVersion();
        workloadInfos.SetWorkloadOutputPaths(
            DotNetSdkHelper.GetDotNetSdkWorkloadPacksFolder(runtimeIdentifier),
            DotNetSdkHelper.GetDotNetSdkWorkloadRuntimePacksFolder(runtimeIdentifier));
        var result = false;
        if (workloadInfos != null)
        {
            foreach (var info in workloadInfos)
            {
                result = await InstallCoreAsync(info, sdkVersion, runtimeIdentifier).ConfigureAwait(false);
            }
        }

        // avoid installing the templates and the workload manifest if the packs failed to install.
        if (result)
        {
            var templatePackVersion = await DotNetSdkHelper.InstallTemplate(
            Constants.TemplatePackName, runtimeIdentifier).ConfigureAwait(false);
            if (!templatePackVersion.Equals("already installed", StringComparison.Ordinal))
            {
                Console.WriteLine($"Successfully installed workload package '{Constants.TemplatePackName}'.");
            }

            InstallManifest(sdkVersion, runtimeIdentifier, await workloadInfos!.ToManifestDictionaryAsync(runtimeIdentifier, templatePackVersion).ConfigureAwait(false));
        }
        else
        {
            throw new InvalidOperationException("Could not install the workload due to failure to obtain package versions.");
        }
    }

    internal static void Uninstall(this List<WorkloadInfo> workloadInfos, string sdkVersion, string runtimeIdentifier)
    {
        _ = workloadInfos.CheckInstalledRefPackVersion();
        _ = workloadInfos.CheckInstalledRuntimePackVersion();
        workloadInfos.SetWorkloadOutputPaths(
            DotNetSdkHelper.GetDotNetSdkWorkloadPacksFolder(runtimeIdentifier),
            DotNetSdkHelper.GetDotNetSdkWorkloadRuntimePacksFolder(runtimeIdentifier));

        // delete the directories to the workload.
        _ = UninstallManifest(sdkVersion, runtimeIdentifier);

        if (workloadInfos != null)
        {
            foreach (var info in workloadInfos)
            {
                UninstallCore(info, sdkVersion, runtimeIdentifier);
            }
        }

        var result = DotNetSdkHelper.UninstallTemplate(Constants.TemplatePackName, runtimeIdentifier);
        if (result.Contains("The template package '") && result.Contains("' is not found."))
        {
            Console.WriteLine($"Workload package '{Constants.TemplatePackName}' was already uninstalled.");
        }
        else
        {
            Console.WriteLine($"Successfully uninstalled workload package '{Constants.TemplatePackName}'.");
        }
    }

    internal static async Task UpdateAsync(this List<WorkloadInfo> workloadInfos, string sdkVersion, string runtimeIdentifier)
    {
        var results = new Dictionary<WorkloadInfo, bool>();
        var sdkPackUpdated = false;
        var workloadFolder = DotNetSdkHelper.GetDotNetSdkWorkloadFolder(
            Constants.WorkloadName,
            sdkVersion,
            runtimeIdentifier,
            out _);
        var text = await File.ReadAllTextAsync(
            Path.Join(
                workloadFolder,
                "WorkloadManifest.json")).ConfigureAwait(false);
        var workloadManifest = JsonSerializer.Deserialize<WorkloadManifest>(text);
        workloadManifest!.Packs.SetPacksDictionary();
        var refPackUpdated = workloadInfos.CheckInstalledRefPackVersion(workloadManifest, runtimeIdentifier);
        var runtimePackUpdated = workloadInfos.CheckInstalledRuntimePackVersion(workloadManifest, runtimeIdentifier);
        workloadInfos.SetWorkloadOutputPaths(
            DotNetSdkHelper.GetDotNetSdkWorkloadPacksFolder(runtimeIdentifier),
            DotNetSdkHelper.GetDotNetSdkWorkloadRuntimePacksFolder(runtimeIdentifier));
        foreach (var info in workloadInfos)
        {
            var result = false;
            var workloadPack = GetWorkloadPack(info, workloadManifest, runtimeIdentifier);
            if (!workloadPack.Name.Equals(Constants.RuntimePackName) && !workloadPack.Name.Equals(Constants.RefPackName))
            {
                result = await UpdateCore(info, workloadManifest, sdkVersion, runtimeIdentifier).ConfigureAwait(false);
            }
            else if (!runtimePackUpdated)
            {
                runtimePackUpdated = await UpdateCore(info, workloadManifest, sdkVersion, runtimeIdentifier).ConfigureAwait(false);
            }
            else if (!refPackUpdated)
            {
                refPackUpdated = await UpdateCore(info, workloadManifest, sdkVersion, runtimeIdentifier).ConfigureAwait(false);
            }

            results.Add(info, result);
        }

        var currentTemplatePackVersion = await NuGetHelper.ResolveWildcardPackageVersionAsync(
            Constants.TemplatePackName).ConfigureAwait(false);
        var templatePackUpdated = await DotNetSdkHelper.UpdateTemplate(
            Constants.TemplatePackName,
            currentTemplatePackVersion,
            runtimeIdentifier).ConfigureAwait(false);
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
            _ = workloadManifest.Packs.ElskomSdkTemplates.UpdateVersion(currentTemplatePackVersion);
            Console.WriteLine($"Workload Template Pack is now version: '{workloadManifest.Packs.ElskomSdkTemplates.Version}'.");
        }

        if (sdkPackUpdated || runtimePackUpdated || refPackUpdated || templatePackUpdated)
        {
            workloadManifest.WriteJsonFile(sdkVersion, runtimeIdentifier);
            workloadManifest.WriteTargetsFile(sdkVersion, runtimeIdentifier);
        }
    }

    internal static async Task<Dictionary<string, string>> ToManifestDictionaryAsync(this List<WorkloadInfo> workloadInfos, string runtimeIdentifier, string? templatePackVersion = null)
    {
        var manifest = new Dictionary<string, string>();
        if (workloadInfos != null)
        {
            foreach (var info in workloadInfos)
            {
                manifest.Add(info.PackageName.Replace($".Runtime.{runtimeIdentifier}", string.Empty, StringComparison.OrdinalIgnoreCase), info.PackageVersion);
            }

            templatePackVersion ??= await NuGetHelper.ResolveWildcardPackageVersionAsync(
                Constants.TemplatePackName).ConfigureAwait(false);
            manifest.Add(Constants.TemplatePackName, templatePackVersion);
        }

        return manifest;
    }

    private static bool CheckInstalledRefPackVersion(this List<WorkloadInfo> workloadInfos, WorkloadManifest? workloadManifest = null, string? runtimeIdentifier = null)
    {
        if (workloadInfos != null)
        {
            foreach (var info in from info in workloadInfos
                                 where info.PackageName.Equals(Constants.RefPackName)
                                 where !info.InstalledPackageVersion.Equals(string.Empty)
                                 where (info.InstalledPackageVersion.EndsWith("-dev") && !info.PackageVersion.EndsWith("-dev"))
                                        || DotNetSdkHelper.ConvertVersionToNuGetVersion(info.InstalledPackageVersion)
                                        > DotNetSdkHelper.ConvertVersionToNuGetVersion(info.PackageVersion)
                                 select info)
            {
                Console.WriteLine("Picked up newer installed reference pack, using that instead.");
                info.PackageVersion = info.InstalledPackageVersion;
                return workloadManifest == null
                    || GetWorkloadPack(info, workloadManifest, runtimeIdentifier!).UpdateVersion(info.PackageVersion);
            }
        }

        return false;
    }

    private static bool CheckInstalledRuntimePackVersion(this List<WorkloadInfo> workloadInfos, WorkloadManifest? workloadManifest = null, string? runtimeIdentifier = null)
    {
        if (workloadInfos != null)
        {
            foreach (var info in from info in workloadInfos
                                 where Constants.RuntimePacks.Contains(info.PackageName)
                                 where !info.InstalledPackageVersion.Equals(string.Empty)
                                 where (info.InstalledPackageVersion.EndsWith("-dev") && !info.PackageVersion.EndsWith("-dev"))
                                        || DotNetSdkHelper.ConvertVersionToNuGetVersion(info.InstalledPackageVersion)
                                        > DotNetSdkHelper.ConvertVersionToNuGetVersion(info.PackageVersion)
                                 select info)
            {
                Console.WriteLine("Picked up newer installed runtime pack, using that instead.");
                info.PackageVersion = info.InstalledPackageVersion;
                return workloadManifest == null
                    || GetWorkloadPack(info, workloadManifest, runtimeIdentifier!).UpdateVersion(info.PackageVersion);
            }
        }

        return false;
    }

    private static void SetWorkloadOutputPaths(this List<WorkloadInfo> workloadInfos, string outputPath, string runtimeOutputPath)
    {
        if (workloadInfos != null)
        {
            foreach (var info in workloadInfos)
            {
                info.OutputPath = Constants.RuntimePacks.Contains(info.PackageName) ? runtimeOutputPath : outputPath;
            }
        }
    }

    private static WorkloadManifest.WorkloadPacks.WorkloadPack GetWorkloadPack(WorkloadInfo info, WorkloadManifest workloadManifest, string runtimeIdentifier)
    {
        var packageName = info.PackageName.Replace($".Runtime.{runtimeIdentifier}", string.Empty, StringComparison.OrdinalIgnoreCase);
        var result = workloadManifest.Packs.Packs[packageName];
        if (string.IsNullOrEmpty(result.Name))
        {
            result.Name = packageName;
        }

        return result;
    }

    private static async Task<bool> InstallCoreAsync(WorkloadInfo info, string sdkVersion, string runtimeIdentifier, bool update = false)
    {
        if (string.IsNullOrEmpty(info.InstalledPackageVersion) || update == true)
        {
            if (!string.IsNullOrEmpty(info.PackageVersion))
            {
                await NuGetHelper.InstallPackageAsync(
                    info.PackageName,
                    info.PackageVersion,
                    info.OutputPath,
                    runtimeIdentifier).ConfigureAwait(false);
                if (!Constants.RuntimePacks.Contains(info.PackageName))
                {
                    await using var fs = File.Create(
                        DotNetSdkHelper.GetDotNetSdkWorkloadMetadataInstalledPacks(
                            info.PackageName,
                            info.PackageVersion,
                            sdkVersion,
                            runtimeIdentifier)).ConfigureAwait(false);
                }

                Console.WriteLine(!update
                    ? $"Successfully installed workload package '{info.PackageName.Replace($".Runtime.{runtimeIdentifier}", string.Empty, StringComparison.OrdinalIgnoreCase)}'."
                    : $"Successfully updated workload package '{info.PackageName.Replace($".Runtime.{runtimeIdentifier}", string.Empty, StringComparison.OrdinalIgnoreCase)}'.");
                return true;
            }
            else
            {
                Console.WriteLine($"No version for workload package '{info.PackageName.Replace($".Runtime.{runtimeIdentifier}", string.Empty, StringComparison.OrdinalIgnoreCase)}' is published to nuget.org.");
            }
        }
        else
        {
            Console.WriteLine($"Workload package '{info.PackageName.Replace($".Runtime.{runtimeIdentifier}", string.Empty, StringComparison.OrdinalIgnoreCase)}' is already installed. Did you intend to run 'update'?");
            return true;
        }

        return false;
    }

    private static void UninstallCore(WorkloadInfo info, string sdkVersion, string runtimeIdentifier)
    {
        if (UninstallPackage(
            info.PackageName.Replace($".Runtime.{runtimeIdentifier}", string.Empty, StringComparison.OrdinalIgnoreCase),
            info.PackageVersion,
            info.OutputPath,
            sdkVersion,
            runtimeIdentifier))
        {
            Console.WriteLine($"Successfully uninstalled workload package '{info.PackageName.Replace($".Runtime.{runtimeIdentifier}", string.Empty, StringComparison.OrdinalIgnoreCase)}'.");
        }
        else
        {
            Console.WriteLine($"Workload package '{info.PackageName.Replace($".Runtime.{runtimeIdentifier}", string.Empty, StringComparison.OrdinalIgnoreCase)}' was already uninstalled.");
        }
    }

    private static async Task<bool> UpdateCore(WorkloadInfo info, WorkloadManifest workloadManifest, string sdkVersion, string runtimeIdentifier)
    {
        var workloadPack = GetWorkloadPack(info, workloadManifest, runtimeIdentifier);
        if (!workloadPack.Version.Equals(info.PackageVersion, StringComparison.Ordinal))
        {
            Console.WriteLine($"Update found for workload package '{info.PackageName.Replace($".Runtime.{runtimeIdentifier}", string.Empty, StringComparison.OrdinalIgnoreCase)}'.");
            _ = UninstallPackage(
                info.PackageName.Replace($".Runtime.{runtimeIdentifier}", string.Empty, StringComparison.OrdinalIgnoreCase),
                workloadPack.Version,
                info.OutputPath,
                sdkVersion,
                runtimeIdentifier);
            _ = workloadPack.UpdateVersion(info.PackageVersion);
            _ = await InstallCoreAsync(
                info,
                sdkVersion,
                runtimeIdentifier,
                true).ConfigureAwait(false);
            return true;
        }

        Console.WriteLine($"No updates found for workload package '{info.PackageName.Replace($".Runtime.{runtimeIdentifier}", string.Empty, StringComparison.OrdinalIgnoreCase)}'.");
        return false;
    }

    private static bool UninstallPackage(string packName, string packVersion, string packFolder, string sdkVersion, string runtimeIdentifier)
    {
        if (!string.IsNullOrEmpty(packVersion))
        {
            Directory.Delete(Path.Join(packFolder, packName), true);
            if (!packName.Equals(Constants.RuntimePackName, StringComparison.Ordinal))
            {
                File.Delete(
                    DotNetSdkHelper.GetDotNetSdkWorkloadMetadataInstalledPacks(
                        Constants.SdkPackName,
                        packVersion,
                        sdkVersion,
                        runtimeIdentifier));
            }

            return true;
        }

        return false;
    }

    private static void InstallManifest(string sdkVersion, string runtimeIdentifier, IReadOnlyDictionary<string, string> packVersions)
    {
        foreach (var packVersion in packVersions)
        {
            if (string.IsNullOrEmpty(packVersion.Value))
            {
                throw new InvalidOperationException($"Workload package '{packVersion.Key}' not found.");
            }
            else if (packVersion.Value.Equals("already installed"))
            {
                throw new InvalidOperationException("The workload was already installed. Check above for the proper command to update.");
            }
        }

        var workloadManifest = WorkloadManifest.Create(packVersions);
        workloadManifest.WriteJsonFile(sdkVersion, runtimeIdentifier);
        workloadManifest.WriteTargetsFile(sdkVersion, runtimeIdentifier);
        using var fs1 = File.Create(
            DotNetSdkHelper.GetDotNetSdkWorkloadMetadataInstalledWorkloads(
                Constants.WorkloadName,
                sdkVersion,
                runtimeIdentifier));
    }

    private static bool UninstallManifest(string sdkVersion, string runtimeIdentifier)
    {
        var workloadFolder = DotNetSdkHelper.GetDotNetSdkWorkloadFolder(
            Constants.WorkloadName,
            sdkVersion,
            runtimeIdentifier,
            out _);
        if (Directory.Exists(workloadFolder))
        {
            Directory.Delete(workloadFolder, true);
            File.Delete(DotNetSdkHelper.GetDotNetSdkWorkloadMetadataInstalledWorkloads(Constants.WorkloadName, sdkVersion, runtimeIdentifier));
            return true;
        }

        return false;
    }
}
