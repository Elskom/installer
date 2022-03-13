namespace Elskom.Check;

public class InstallCommand : AsyncCommand<WorkloadSettings>
{
    public override async Task<int> ExecuteAsync([NotNull] CommandContext context, [NotNull] WorkloadSettings settings)
    {
        DotNetSdkHelper.GetOrUpdateSdkVersion(ref settings);
        var sdkPackVersion = await NuGetHelper.ResolveWildcardPackageVersionAsync(
            Constants.SdkPackName).ConfigureAwait(false);
        var refPackVersion = await NuGetHelper.ResolveWildcardPackageVersionAsync(
            Constants.RefPackName).ConfigureAwait(false);
        var runtimePackVersion = await NuGetHelper.ResolveWildcardPackageVersionAsync(
            Constants.RuntimePackName).ConfigureAwait(false);
        var installedSdkPackVersion = DotNetSdkHelper.GetInstalledDotNetSdkWorkloadPackVersion(
            Constants.SdkPackName);
        var installedRefPackVersion = DotNetSdkHelper.GetInstalledDotNetSdkWorkloadPackVersion(
            Constants.RefPackName);
        var installedRuntimePackVersion = DotNetSdkHelper.GetInstalledDotNetSdkWorkloadRuntimePackVersion(
            Constants.RuntimePackName);
        if (!installedRefPackVersion.Equals(string.Empty))
        {
            if (installedRefPackVersion.EndsWith("-dev")
                || DotNetSdkHelper.ConvertVersionToNuGetVersion(installedRefPackVersion)
                > DotNetSdkHelper.ConvertVersionToNuGetVersion(refPackVersion))
            {
                Console.WriteLine("Picked up newer installed reference pack, using that instead.");
                refPackVersion = installedRefPackVersion;
            }
        }

        if (!installedRuntimePackVersion.Equals(string.Empty))
        {
            if (installedRuntimePackVersion.EndsWith("-dev")
                || DotNetSdkHelper.ConvertVersionToNuGetVersion(installedRuntimePackVersion)
                > DotNetSdkHelper.ConvertVersionToNuGetVersion(runtimePackVersion))
            {
                Console.WriteLine("Picked up newer installed runtime pack, using that instead.");
                runtimePackVersion = installedRuntimePackVersion;
            }
        }

        if (await InstallPackageAsync(
                Constants.SdkPackName,
                sdkPackVersion,
                installedSdkPackVersion,
                settings.SdkVersion!,
                DotNetSdkHelper.GetDotNetSdkWorkloadPacksFolder()).ConfigureAwait(false))
        {
            Console.WriteLine($"Successfully installed workload package '{Constants.SdkPackName}'.");
        }

        if (await InstallPackageAsync(
                Constants.RefPackName,
                refPackVersion,
                installedRefPackVersion,
                settings.SdkVersion!,
                DotNetSdkHelper.GetDotNetSdkWorkloadPacksFolder()).ConfigureAwait(false))
        {
            Console.WriteLine($"Successfully installed workload package '{Constants.RefPackName}'.");
        }

        if (await InstallPackageAsync(
                Constants.RuntimePackName,
                runtimePackVersion,
                installedRuntimePackVersion,
                settings.SdkVersion!,
                DotNetSdkHelper.GetDotNetSdkWorkloadRuntimePacksFolder()).ConfigureAwait(false))
        {
            Console.WriteLine($"Successfully installed workload package '{Constants.RuntimePackName}'.");
        }

        // avoid installing the templates and the workload manifest if sdkPackVersion, refPackVersion,
        // and runtimePackVersion is null or empty.
        if (!string.IsNullOrEmpty(sdkPackVersion)
            && !string.IsNullOrEmpty(refPackVersion)
            && !string.IsNullOrEmpty(runtimePackVersion))
        {
            var templatePackVersion = await DownloadPackageAsync(
                Constants.TemplatePackName).ConfigureAwait(false);
            if (!templatePackVersion.Equals("already installed"))
            {
                Console.WriteLine($"Successfully installed workload package '{Constants.TemplatePackName}'.");
            }

            InstallManifest(settings.SdkVersion!, new Dictionary<string, string>
            {
                { Constants.SdkPackName, sdkPackVersion },
                { Constants.RefPackName, refPackVersion },
                { Constants.RuntimePackName, runtimePackVersion },
                { Constants.TemplatePackName, templatePackVersion },
            });
        }
        return 0;
    }

    private static void InstallManifest(string sdkVersion, IReadOnlyDictionary<string, string> packVersions)
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
        workloadManifest.WriteJsonFile(sdkVersion);
        workloadManifest.WriteTargetsFile(sdkVersion);
        using var fs1 = File.Create(
            DotNetSdkHelper.GetDotNetSdkWorkloadMetadataInstalledWorkloads(
                Constants.WorkloadName,
                sdkVersion));
    }

    internal static async Task<bool> InstallPackageAsync(string packName, string packVersion, string installedPackVersion, string sdkVersion, string outputPath)
    {
        if (string.IsNullOrEmpty(installedPackVersion))
        {
            if (!string.IsNullOrEmpty(packVersion))
            {
                await NuGetHelper.InstallPackageAsync(
                    packName,
                    packVersion,
                    outputPath).ConfigureAwait(false);
                if (!packName.Equals(Constants.RuntimePackName))
                {
                    await using var fs = File.Create(
                        DotNetSdkHelper.GetDotNetSdkWorkloadMetadataInstalledPacks(
                            packName,
                            packVersion,
                            sdkVersion)).ConfigureAwait(false);
                }

                return true;
            }
            else
            {
                Console.WriteLine($"No version for workload package '{packName}' is published to any registered nuget feeds.");
            }
        }
        else
        {
            Console.WriteLine($"Workload package '{packName}' is already installed. Did you intend to run 'update'?");
        }

        return false;
    }

    internal static async Task<string> DownloadPackageAsync(string packName)
    {
        var version = string.Empty;
        var templateInstallCommand = new ProcessStartOptions
        {
            WaitForProcessExit = true,
        }.WithStartInformation(
            $"{DotNetSdkHelper.GetDotNetSdkLocation()}{Path.DirectorySeparatorChar}dotnet{(OperatingSystem.IsWindows() ? ".exe" : string.Empty)}",
            $"new -i {packName}",
            true,
            false,
            false,
            true,
            ProcessWindowStyle.Hidden,
            Environment.CurrentDirectory);
        var result = templateInstallCommand.Start();
        var results = result.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
        foreach (var resultItem in results)
        {
            if (resultItem.IndexOf("::", StringComparison.Ordinal) > -1)
            {
                if (resultItem.Contains(" installed the following templates:"))
                {
                    var resultItems = resultItem.Split(' ');
                    resultItems = resultItems[1].Split("::");
                    version = resultItems[1];
                    await Task.Delay(0).ConfigureAwait(false);
                }
            }
            else if (resultItem.Contains(" is already installed, version:"))
            {
                Console.WriteLine($"Workload package '{packName}' is already installed. Did you intend to run 'update'?");
                version = "already installed";
                break;
            }
        }

        return version;
    }
}
