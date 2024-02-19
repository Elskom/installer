namespace Elskom.Check;

internal static class DotNetSdkHelper
{
    internal static void GetOrUpdateSdkVersion(ref WorkloadSettings settings)
    {
        if (string.IsNullOrWhiteSpace(settings.RuntimeIdentifier))
        {
            // find the runtime identifier of the .NET SDK currently being used.
            settings.RuntimeIdentifier = GetDotNetSdkRID();
        }

        if (string.IsNullOrWhiteSpace(settings.SdkVersion))
        {
            // find the feature band version of the .NET SDK currently being used.
            settings.SdkVersion = GetDotNetSdkFeatureBandVersion(settings.RuntimeIdentifier!);
        }
    }

    internal static string GetDotNetSdkWorkloadFolder(string workloadName, string sdkVersion, string runtimeIdentifier)
    {
        var sdkLocation = GetDotNetSdkLocation(runtimeIdentifier);
        var sdkVersionBand = string.IsNullOrEmpty(sdkVersion) switch
        {
            true => GetDotNetSdkFeatureBandVersion(runtimeIdentifier),
            false => ConvertVersionToSdkBand(sdkVersion),
        };
        return Path.Join(sdkLocation, "sdk-manifests", sdkVersionBand, workloadName);
    }

    internal static string GetDotNetSdkWorkloadPacksFolder(string runtimeIdentifier)
    {
        var sdkLocation = GetDotNetSdkLocation(runtimeIdentifier);
        return Path.Join(sdkLocation, "packs");
    }

    internal static string GetDotNetSdkWorkloadRuntimePacksFolder(string runtimeIdentifier)
    {
        var sdkLocation = GetDotNetSdkLocation(runtimeIdentifier);
        return Path.Join(sdkLocation, "shared");
    }

    internal static string GetDotNetSdkWorkloadMetadataInstalledWorkloads(string packName, string sdkVersion, string runtimeIdentifier)
    {
        var sdkLocation = GetDotNetSdkLocation(runtimeIdentifier);
        var sdkVersionBand = string.IsNullOrEmpty(sdkVersion) switch
        {
            true => GetDotNetSdkFeatureBandVersion(runtimeIdentifier),
            false => ConvertVersionToSdkBand(sdkVersion),
        };
        var installedWorkloadsPath = Path.Join(
            sdkLocation,
            "metadata",
            "workloads",
            sdkVersionBand,
            "InstalledWorkloads");
        if (!Directory.Exists(installedWorkloadsPath))
        {
            _ = Directory.CreateDirectory(installedWorkloadsPath);
        }

        return Path.Join(
            installedWorkloadsPath,
            packName);
    }

    internal static string GetDotNetSdkWorkloadMetadataInstalledPacks(string packName, string packVersion, string sdkVersion, string runtimeIdentifier, bool delete = false)
    {
        var sdkLocation = GetDotNetSdkLocation(runtimeIdentifier);
        var sdkVersionBand = string.IsNullOrEmpty(sdkVersion) switch
        {
            true => GetDotNetSdkFeatureBandVersion(runtimeIdentifier),
            false => ConvertVersionToSdkBand(sdkVersion),
        };
        var dir = Path.Join(
            sdkLocation,
            "metadata",
            "workloads",
            "InstalledPacks",
            "v1",
            packName,
            packVersion);
        if (!Directory.Exists(dir))
        {
            _ = Directory.CreateDirectory(dir);
        }
        else if (delete)
        {
            Directory.Delete(dir, true);
        }

        return Path.Join(dir, sdkVersionBand);
    }

    internal static string GetInstalledDotNetSdkWorkloadPackVersion(string packName, string runtimeIdentifier)
    {
        var sdkLocation = GetDotNetSdkLocation(runtimeIdentifier);
        var packPath = Path.Join(sdkLocation, "packs", packName);
        if (Directory.Exists(packPath))
        {
            var di = new DirectoryInfo(packPath);
            return di.EnumerateDirectories("*", SearchOption.TopDirectoryOnly).First().Name;
        }

        return string.Empty;
    }

    internal static string GetInstalledDotNetSdkWorkloadRuntimePackVersion(string packName, string runtimeIdentifier)
    {
        var workloadRuntimePackFolder = GetDotNetSdkWorkloadRuntimePacksFolder(runtimeIdentifier);
        var packPath = Path.Join(workloadRuntimePackFolder, packName);
        if (Directory.Exists(packPath))
        {
            var di = new DirectoryInfo(packPath);
            return di.EnumerateDirectories("*", SearchOption.TopDirectoryOnly).MaxBy(
                item => ConvertVersionToNuGetVersion(item.Name))?.Name ?? string.Empty;
        }

        return string.Empty;
    }

    internal static string GetDotNetSdkLocation(string runtimeIdentifier)
    {
        var knownDotNetLocations = (OperatingSystem.IsWindows(), OperatingSystem.IsLinux(), OperatingSystem.IsMacOS()) switch
        {
            (true, false, false) =>
            [
                Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                    "dotnet",
                    "dotnet.exe"),
                Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                    "dotnet",
                    "dotnet.exe"),
            ],
            (false, false, true) =>
            [
                "/usr/local/share/dotnet/dotnet",
            ],
            (false, true, false) =>
            [
                Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                    "share",
                    "dotnet",
                    "dotnet")
            ],
            _ => Array.Empty<string>(),
        };
        var sdkRoot = string.Empty;
        var envSdkRoot = Environment.GetEnvironmentVariable("DOTNET_ROOT");
        if (envSdkRoot is not null && Directory.Exists(envSdkRoot))
        {
            sdkRoot = envSdkRoot;
        }

        if (string.IsNullOrEmpty(sdkRoot) || !Directory.Exists(sdkRoot))
        {
            foreach (var loc in knownDotNetLocations)
            {
                // for x64/arm64 versions of the .NET SDK
                if (OperatingSystem.IsWindows()
                    && (RuntimeInformation.OSArchitecture == Architecture.X64
                    || RuntimeInformation.OSArchitecture == Architecture.Arm64)
                    && loc.Contains("Program Files (x86)")
                    && runtimeIdentifier.Equals(RuntimeInformation.RuntimeIdentifier, StringComparison.Ordinal))
                {
                    // we need the 64 bit sdk location (if installed) instead.
                    var loc2 = knownDotNetLocations[Array.IndexOf(knownDotNetLocations, loc) + 1];
                    if (File.Exists(loc2))
                    {
                        var dotnet = new FileInfo(loc2);
                        var sdkDir = dotnet.Directory;
                        if (sdkDir is not null)
                        {
                            sdkRoot = sdkDir.FullName;
                        }

                        break;
                    }
                }

                if (File.Exists(loc))
                {
                    var dotnet = new FileInfo(loc);
                    var sdkDir = dotnet.Directory;
                    if (sdkDir is not null)
                    {
                        sdkRoot = sdkDir.FullName;
                    }

                    break;
                }
            }
        }

        return sdkRoot;
    }

    internal static NuGetVersion ConvertVersionToNuGetVersion(string version)
    {
        _ = NuGetVersion.TryParse(version, out var version2);
        return version2!;
    }

    internal static async Task<string> InstallTemplate(string templatePackName, string runtimeIdentifier)
    {
        var version = string.Empty;
        var templateInstallCommand = new ProcessStartOptions
        {
            WaitForProcessExit = true,
        }.WithStartInformation(
            $"{GetDotNetSdkLocation(runtimeIdentifier)}{Path.DirectorySeparatorChar}dotnet{(OperatingSystem.IsWindows() ? ".exe" : string.Empty)}",
            $"new install {templatePackName}",
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
            if (resultItem.Contains("is already installed,"))
            {
                Console.WriteLine($"Workload package '{templatePackName}' is already installed. Did you intend to run 'update'?");
                version = "already installed";
                break;
            }
            else if (resultItem.LastIndexOf("::", StringComparison.Ordinal) > -1)
            {
                if (resultItem.Contains(" installed the following templates:"))
                {
                    var resultItems = resultItem.Split(' ')[1].Split("::");
                    version = resultItems[1];
                    await Task.Delay(0).ConfigureAwait(false);
                    break;
                }
            }
        }

        return version;
    }

    internal static async Task<bool> UpdateTemplate(
        string packName,
        string packVersion,
        string runtimeIdentifier)
    {
        if (!packVersion.Equals(
            GetInstalledTemplateVersion(packName), StringComparison.OrdinalIgnoreCase))
        {
            Console.WriteLine($"Update found for workload package '{packName}'.");
            _ = UninstallTemplate(packName, runtimeIdentifier);
            _ = await InstallTemplate(packName, runtimeIdentifier).ConfigureAwait(false);
            Console.WriteLine($"Successfully updated workload package '{packName}'.");
            return true;
        }

        Console.WriteLine($"No updates found for workload package '{packName}'.");
        return false;
    }

    internal static string UninstallTemplate(string templatePackName, string runtimeIdentifier)
    {
        var templateUninstallCommand = new ProcessStartOptions
        {
            WaitForProcessExit = true,
        }.WithStartInformation(
                $"{DotNetSdkHelper.GetDotNetSdkLocation(runtimeIdentifier)}{Path.DirectorySeparatorChar}dotnet{(OperatingSystem.IsWindows() ? ".exe" : string.Empty)}",
                $"new uninstall {templatePackName}",
                false,
                false,
                false,
                true,
                ProcessWindowStyle.Hidden,
                Environment.CurrentDirectory);
        return templateUninstallCommand.Start();
    }

    private static string GetInstalledTemplateVersion(string packName)
    {
        var searchPath = $"{Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)}/.templateengine/packages/";
        var version = string.Empty;
        foreach (var template in from template in Directory.EnumerateFileSystemEntries(searchPath, "*.nupkg", SearchOption.TopDirectoryOnly)
                                 where template.Contains(packName, StringComparison.OrdinalIgnoreCase) && template.EndsWith(".nupkg", StringComparison.OrdinalIgnoreCase)
                                 select template)
        {
            version = Path.GetFileNameWithoutExtension(template).Replace($"{packName}.", string.Empty);
        }

        return version;
    }

    private static string GetDotNetSdkFeatureBandVersion(string runtimeIdentifier)
    {
        var sdkLocation = GetDotNetSdkLocation(runtimeIdentifier);
        var startOptions = new ProcessStartOptions
        {
            WaitForProcessExit = true,
        }.WithStartInformation(
            $"{sdkLocation}{Path.DirectorySeparatorChar}dotnet{(OperatingSystem.IsWindows() ? ".exe" : string.Empty)}",
            "--list-sdks",
            true,
            true,
            false,
            true,
            ProcessWindowStyle.Hidden,
            Environment.CurrentDirectory);
        var result = startOptions.Start().Split(
            Environment.NewLine,
            StringSplitOptions.RemoveEmptyEntries);
        var sdks = new List<DotNetSdkInfo>();
        foreach (var line in result)
        {
            try
            {
                if (line.Contains('[') && line.Contains(']'))
                {
                    var versionStr = line[..line.IndexOf('[')].Trim();
                    var locStr = line[line.IndexOf('[')..].Trim('[', ']');
                    var loc = Path.Combine(locStr, versionStr);
                    if (Directory.Exists(locStr) && Directory.Exists(loc))
                    {
                        // If only 1 file it's probably the
                        // EnableWorkloadResolver.sentinel file that was 
                        // never uninstalled with the rest of the sdk
                        if (Directory.GetFiles(loc).Length > 1)
                        {
                            sdks.Add(new DotNetSdkInfo(versionStr, new DirectoryInfo(loc)));
                        }
                    }
                }
            }
            catch
            {
                // Bad line, ignore
            }
        }

        FilterSdks(ref sdks);
        if (sdks.Count > 1)
        {
            Console.WriteLine("Bug found. There should have just been 1 sdk left over from the filter.");
        }

        var sdk = sdks[0]; // there should only be 1 listed now.
        Console.WriteLine($".NET SDK Location: {sdkLocation}");
        Console.WriteLine($"Detected .NET SDK Version: {sdk.Version}");
        var sdkVersion = ConvertVersionToSdkBand(sdk.Version);
        Console.WriteLine($"Detected .NET SDK Band as: {sdkVersion}");
        var version = new Version(sdkVersion);
        return version.Major < 8
            ? throw new InvalidOperationException("This tool is compatible only with .NET 8.0.100 SDK or newer.")
            : sdkVersion;
    }

    private static string GetDotNetSdkRID()
    {
        var sdkLocation = GetDotNetSdkLocation(RuntimeInformation.RuntimeIdentifier);
        var startOptions = new ProcessStartOptions
        {
            WaitForProcessExit = true,
        }.WithStartInformation(
            $"{sdkLocation}{Path.DirectorySeparatorChar}dotnet{(OperatingSystem.IsWindows() ? ".exe" : string.Empty)}",
            "--info",
            true,
            true,
            false,
            true,
            ProcessWindowStyle.Hidden,
            Environment.CurrentDirectory);
        var result = startOptions.Start().Split(
            Environment.NewLine,
            StringSplitOptions.RemoveEmptyEntries);
        var sdkRuntimeIdentifier = "Any";
        foreach (var line in result)
        {
            if (line.Contains("RID:"))
            {
                sdkRuntimeIdentifier = line.Replace("RID:", string.Empty).Trim();
            }
        }

        return sdkRuntimeIdentifier;
    }

    private static string ConvertVersionToSdkBand(string version)
    {
        var version2 = ConvertVersionToNuGetVersion(version);
        return $"{version2.Major}.{version2.Minor}.{version2.Patch / 100 * 100}";
    }

    private static void FilterSdks(ref List<DotNetSdkInfo> sdks)
    {
        var sdk = sdks.MaxBy(netSdkInfo => ConvertVersionToNuGetVersion(netSdkInfo.Version));
        sdks.Clear();
        sdks.Add(sdk!);
    }
}
