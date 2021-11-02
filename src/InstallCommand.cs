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
        var templatePackVersion = await NuGetHelper.ResolveWildcardPackageVersionAsync(
            Constants.TemplatePackName).ConfigureAwait(false);
        var installedSdkPackVersion = DotNetSdkHelper.GetInstalledDotNetSdkWorkloadPackVersion(
            Constants.SdkPackName);
        var installedRefPackVersion = DotNetSdkHelper.GetInstalledDotNetSdkWorkloadPackVersion(
            Constants.RefPackName);
        var installedRuntimePackVersion = DotNetSdkHelper.GetInstalledDotNetSdkWorkloadPackVersion(
            Constants.RuntimePackName);
        var installedTemplatePackVersion = DotNetSdkHelper.GetInstalledDotNetSdkWorkloadTemplatePackVersion(
            Constants.TemplatePackName);
        InstallManifest(settings.SdkVersion!, new Dictionary<string, string>
        {
            { Constants.SdkPackName, sdkPackVersion },
            { Constants.RefPackName, refPackVersion },
            { Constants.RuntimePackName, runtimePackVersion },
            { Constants.TemplatePackName, templatePackVersion },
        });
        Console.WriteLine("Installing Packages...");
        await InstallPackageAsync(
            Constants.SdkPackName,
            sdkPackVersion,
            installedSdkPackVersion,
            settings.SdkVersion!).ConfigureAwait(false);
        await InstallPackageAsync(
            Constants.RefPackName,
            refPackVersion,
            installedRefPackVersion,
            settings.SdkVersion!).ConfigureAwait(false);
        await InstallPackageAsync(
            Constants.RuntimePackName,
            runtimePackVersion,
            installedRuntimePackVersion,
            settings.SdkVersion!).ConfigureAwait(false);
        Console.WriteLine("Installing Templates...");
        await DownloadPackageAsync(
            Constants.TemplatePackName,
            installedTemplatePackVersion,
            settings.SdkVersion!).ConfigureAwait(false);
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
        }

        var workloadManifest = WorkloadManifest.Create(packVersions);
        workloadManifest.WriteJsonFile(sdkVersion);
        workloadManifest.WriteTargetsFile(sdkVersion);
        using var fs1 = File.Create(
            DotNetSdkHelper.GetDotNetSdkWorkloadMetadataInstalledWorkloads(
                Constants.WorkloadName,
                sdkVersion));
    }

    internal static async Task InstallPackageAsync(string packName, string packVersion, string installedPackVersion, string sdkVersion)
    {
        if (string.IsNullOrEmpty(installedPackVersion))
        {
            if (!string.IsNullOrEmpty(packVersion))
            {
                await NuGetHelper.InstallPackageAsync(
                    packName,
                    DotNetSdkHelper.GetDotNetSdkWorkloadPacksFolder(
                        packName,
                        packVersion)).ConfigureAwait(false);
                await using var fs = File.Create(
                    DotNetSdkHelper.GetDotNetSdkWorkloadMetadataInstalledPacks(
                        packName,
                        packVersion,
                        sdkVersion)).ConfigureAwait(false);
                Console.WriteLine($"Successfully installed workload package '{packName}'.");
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
    }

    internal static async Task DownloadPackageAsync(string packName, string installedPackVersion, string sdkVersion)
    {
        if (string.IsNullOrEmpty(installedPackVersion))
        {
            var outputPath = DotNetSdkHelper.GetDotNetSdkWorkloadTemplatePacksFolder();
            await NuGetHelper.DownloadPackageAsync(
                packName,
                outputPath).ConfigureAwait(false);
            await using var fs = File.Create(
                DotNetSdkHelper.GetDotNetSdkWorkloadMetadataInstalledPacks(
                    packName,
                    NuGetHelper.GetDownloadedPackageVersion(
                        packName,
                        outputPath).version,
                    sdkVersion)).ConfigureAwait(false);
            Console.WriteLine($"Successfully installed workload package '{packName}'.");
        }
        else
        {
            Console.WriteLine($"Workload package '{packName}' is already installed. Did you intend to run 'update'?");
        }
    }
}
