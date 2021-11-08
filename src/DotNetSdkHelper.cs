namespace Elskom.Check;

internal static class DotNetSdkHelper
{
    internal static void GetOrUpdateSdkVersion(ref WorkloadSettings settings)
    {
        if (string.IsNullOrWhiteSpace(settings.SdkVersion))
        {
            // find the feature band version of the .NET SDK currently being used.
            settings.SdkVersion = GetDotNetSdkFeatureBandVersion();
        }
    }

    internal static string GetDotNetSdkWorkloadFolder(string workloadName, string sdkVersion)
    {
        var sdkLocation = GetDotNetSdkLocation();
        var sdkVersionBand = string.IsNullOrEmpty(sdkVersion) switch
        {
            true => GetDotNetSdkFeatureBandVersion(),
            false => ConvertVersionToSdkBand(sdkVersion),
        };
        return Path.Join(sdkLocation, "sdk-manifests", sdkVersionBand, workloadName);
    }

    internal static string GetDotNetSdkWorkloadPacksFolder()
    {
        var sdkLocation = GetDotNetSdkLocation();
        return Path.Join(sdkLocation, "packs");
    }

    internal static string GetDotNetSdkWorkloadRuntimePacksFolder()
    {
        var sdkLocation = GetDotNetSdkLocation();
        return Path.Join(sdkLocation, "shared");
    }

    internal static string GetDotNetSdkWorkloadMetadataInstalledWorkloads(string packName, string sdkVersion)
    {
        var sdkLocation = GetDotNetSdkLocation();
        var sdkVersionBand = string.IsNullOrEmpty(sdkVersion) switch
        {
            true => GetDotNetSdkFeatureBandVersion(),
            false => ConvertVersionToSdkBand(sdkVersion),
        };
        return Path.Join(
            sdkLocation,
            "metadata",
            "workloads",
            sdkVersionBand,
            "InstalledWorkloads",
            packName);
    }

    internal static string GetDotNetSdkWorkloadMetadataInstalledPacks(string packName, string packVersion, string sdkVersion, bool delete = false)
    {
        var sdkLocation = GetDotNetSdkLocation();
        var sdkVersionBand = string.IsNullOrEmpty(sdkVersion) switch
        {
            true => GetDotNetSdkFeatureBandVersion(),
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

    internal static string GetInstalledDotNetSdkWorkloadPackVersion(string packName)
    {
        var sdkLocation = GetDotNetSdkLocation();
        var packPath = Path.Join(sdkLocation, "packs", packName);
        if (Directory.Exists(packPath))
        {
            var di = new DirectoryInfo(packPath);
            return di.EnumerateDirectories("*", SearchOption.TopDirectoryOnly).First().Name;
        }

        return string.Empty;
    }

    internal static string GetInstalledDotNetSdkWorkloadRuntimePackVersion(string packName)
    {
        var workloadRuntimePackFolder = GetDotNetSdkWorkloadRuntimePacksFolder();
        var packPath = Path.Join(workloadRuntimePackFolder, packName);
        if (Directory.Exists(packPath))
        {
            var di = new DirectoryInfo(packPath);
            return di.EnumerateDirectories("*", SearchOption.TopDirectoryOnly).First().Name;
        }

        return string.Empty;
    }

    internal static string GetDotNetSdkLocation()
    {
        var knownDotNetLocations = (OperatingSystem.IsWindows(), OperatingSystem.IsLinux(), OperatingSystem.IsMacOS()) switch
        {
            (true, false, false) => new[]
            {
                Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                    "dotnet",
                    "dotnet.exe"),
                Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                    "dotnet",
                    "dotnet.exe"),
            },
            (false, false, true) => new[]
            {
                "/usr/local/share/dotnet/dotnet",
            },
            (false, true, false) => new[]
            {
                Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                    "share",
                    "dotnet",
                    "dotnet")
            },
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
                if (OperatingSystem.IsWindows()
                    && RuntimeInformation.OSArchitecture == Architecture.X64
                    && loc.Contains("Program Files (x86)"))
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

    private static string GetDotNetSdkFeatureBandVersion()
    {
        var sdkLocation = GetDotNetSdkLocation();
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
        Console.WriteLine($"Detected .NET SDK Version: {sdk.Version}");
        var sdkVersion = ConvertVersionToSdkBand(sdk.Version);
        Console.WriteLine($"Detected .NET SDK Band as: {sdkVersion}");
        return sdkVersion;
    }

    private static string ConvertVersionToSdkBand(string version)
    {
        var version2 = ConvertVersionToNuGetVersion(version);
        return $"{version2.Major}.{version2.Minor}.{version2.Patch / 100 * 100}";
    }

    private static NuGetVersion ConvertVersionToNuGetVersion(string version)
    {
        _ = NuGetVersion.TryParse(version, out var version2);
        return version2;
    }

    private static void FilterSdks(ref List<DotNetSdkInfo> sdks)
    {
        var sdk = sdks.MaxBy(netSdkInfo => ConvertVersionToNuGetVersion(netSdkInfo.Version));
        sdks.Clear();
        sdks.Add(sdk!);
    }
}
