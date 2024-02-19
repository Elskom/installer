namespace Elskom.Check;

internal static class NuGetHelper
{
    internal static HttpClient? HttpClient { get; set; }

    internal static async Task InstallPackageAsync(string packageName, string version, string outputPath, string runtimeIdentifier)
    {
        var lowerPackageName = packageName.ToLowerInvariant();
        var lowerVersion = version.ToLowerInvariant();
        var packageResponse = await HttpClient!.GetAsync($"https://api.nuget.org/v3-flatcontainer/{lowerPackageName}/{lowerVersion}/{lowerPackageName}.{lowerVersion}.nupkg");
        if (packageResponse.StatusCode.Equals(HttpStatusCode.NotFound))
        {
            throw new InvalidOperationException("bug in package install.");
        }

        if (!packageResponse.IsSuccessStatusCode)
        {
            return;
        }

        // strip out the '.Runtime.{runtimeIdentifier}' part of the package name for the extraction path.
        var extractionPath = $"{outputPath}/{packageName.Replace($".Runtime.{runtimeIdentifier}", string.Empty)}/{version}";
        Directory.CreateDirectory(extractionPath);
        var packageContents = await packageResponse.Content.ReadAsByteArrayAsync();
        if (outputPath.Equals(DotNetSdkHelper.GetDotNetSdkWorkloadRuntimePacksFolder(runtimeIdentifier)))
        {
            // properly unpackage the runtime pack.
            // var runtimeIdentifier = packageName.Replace("Elskom.Sdk.App.Runtime.", "", StringComparison.OrdinalIgnoreCase);
            using var ms = new MemoryStream(packageContents);
            using var zipArchive = new ZipArchive(ms, ZipArchiveMode.Read, true);
            foreach (var entry in zipArchive.Entries)
            {
                if (entry.FullName.Contains($"runtimes/{runtimeIdentifier}/lib/net8.0/")
                    || entry.FullName.Contains($"runtimes/{runtimeIdentifier}/native/"))
                {
                    entry.ExtractToFile(Path.Join(extractionPath, entry.Name), true);
                }
                else if (entry.Name.Equals("Elskom.Sdk.App.versions.txt"))
                {
                    entry.ExtractToFile(Path.Join(extractionPath, ".version"), true);
                }
            }
        }
        else
        {
            var filePath = Path.Combine(extractionPath, $"{packageName}.nupkg");
            await File.WriteAllBytesAsync(filePath, packageContents);
            ZipFile.ExtractToDirectory(filePath, extractionPath, true);
        }
    }

    internal static (string version, string fileName) GetDownloadedPackageVersion(string packageName, string inputPath)
    {
        var files = Directory.EnumerateFiles(
            inputPath,
            $"{packageName}.*").ToList();
        string version = string.Empty;
        string file = string.Empty;
        if (files.Any())
        {
            file = files.First();
            version = file.Replace($"{packageName}.", string.Empty).Replace(".nupkg", string.Empty);
        }

        return (version, file);
    }

    internal static async Task<List<WorkloadInfo>> ResolveWildcardWorkloadPackageVersionsAsync(string runtimeIdentifier)
    {
        var list = new List<WorkloadInfo>();
        var currentSdkPackVersion = await ResolveWildcardPackageVersionAsync(
            Constants.SdkPackName).ConfigureAwait(false);
        var installedSdkPackVersion = DotNetSdkHelper.GetInstalledDotNetSdkWorkloadPackVersion(
            Constants.SdkPackName, runtimeIdentifier);
        var currentRefPackVersion = await ResolveWildcardPackageVersionAsync(
            Constants.RefPackName).ConfigureAwait(false);
        var installedRefPackVersion = DotNetSdkHelper.GetInstalledDotNetSdkWorkloadPackVersion(
            Constants.RefPackName, runtimeIdentifier);
        list.Add(new WorkloadInfo()
        {
            PackageName = Constants.SdkPackName,
            PackageVersion = currentSdkPackVersion,
            InstalledPackageVersion = installedSdkPackVersion
        });
        list.Add(new WorkloadInfo()
        {
            PackageName = Constants.RefPackName,
            PackageVersion = currentRefPackVersion,
            InstalledPackageVersion = installedRefPackVersion
        });
        foreach (var runtimePack in from runtimePack in Constants.RuntimePacks
                                    where runtimePack.Contains(runtimeIdentifier)
                                    select runtimePack)
        {
            var currentRuntimePackVersion = await ResolveWildcardPackageVersionAsync(runtimePack).ConfigureAwait(false);
            var installedRuntimePackVersion = DotNetSdkHelper.GetInstalledDotNetSdkWorkloadRuntimePackVersion(runtimePack.Replace($".Runtime.{runtimeIdentifier}", string.Empty, StringComparison.OrdinalIgnoreCase), runtimeIdentifier);
            list.Add(new WorkloadInfo()
            {
                PackageName = runtimePack,
                PackageVersion = currentRuntimePackVersion,
                InstalledPackageVersion = installedRuntimePackVersion,
            });
        }

        return list;
    }

    internal static async Task<string> ResolveWildcardPackageVersionAsync(string packageName)
    {
        var lowerPackageName = packageName.ToLowerInvariant();
        var versionResponse = await HttpClient!.GetAsync($"https://api.nuget.org/v3-flatcontainer/{lowerPackageName}/index.json").ConfigureAwait(false);
        if (versionResponse.StatusCode.Equals(HttpStatusCode.NotFound))
        {
            throw new InvalidOperationException("bug in package version request.");
        }

        if (!versionResponse.IsSuccessStatusCode)
        {
            return string.Empty;
        }

        var content = await versionResponse.Content.ReadAsStringAsync().ConfigureAwait(false);
        var versionsInfo = JsonSerializer.Deserialize<VersionInfo>(content);
        var version = versionsInfo?.Versions?.LastOrDefault();
        return version ?? string.Empty;
    }

    private record VersionInfo
    {
        [JsonPropertyName("versions")]
        public string[]? Versions { get; init; }
    }
}
