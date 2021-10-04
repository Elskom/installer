using NuGet.Common;
using NuGet.Configuration;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;
using NuGet.PackageManagement;
using NuGet.Packaging.Core;
using NuGet.ProjectManagement;
using NuGet.Resolver;

namespace Elskom.Check;

internal static class NuGetHelper
{
    internal static async Task InstallPackageAsync(string packageName, string outputPath)
    {
        var version = await ResolveWildcardPackageVersionAsync(packageName).ConfigureAwait(false);
        var providers = new List<Lazy<INuGetResourceProvider>>();
        providers.AddRange(Repository.Provider.GetCoreV3());
        var packageSource = new PackageSource("https://api.nuget.org/v3/index.json");
        var repo = new SourceRepository(packageSource, providers);
        var settings = Settings.LoadDefaultSettings(
            GetNugetConfigLocation(),
            "NuGet.Config",
            new XPlatMachineWideSetting());
        var packageSourceProvider = new PackageSourceProvider(settings);
        var sourceRepositoryProvider = new SourceRepositoryProvider(packageSourceProvider, providers);
        var folderNugetProject = new FolderNuGetProject(outputPath);
        var resolutionContext = new ResolutionContext(
            DependencyBehavior.Lowest,
            true,
            false,
            VersionConstraints.ExactRelease);
        var packageManager = new NuGetPackageManager(
            sourceRepositoryProvider,
            settings, outputPath)
        {
            PackagesFolderNuGetProject = folderNugetProject,
        };
        var identity = new PackageIdentity(packageName, NuGetVersion.Parse(version));
        await packageManager.InstallPackageAsync(
            folderNugetProject,
            identity,
            resolutionContext,
            new EmptyNuGetProjectContext(),
            repo,
            sourceRepositoryProvider.GetRepositories(),
            CancellationToken.None).ConfigureAwait(false);
    }

    internal static async Task DownloadPackageAsync(string packageName, string outputPath)
    {
        var version = await ResolveWildcardPackageVersionAsync(packageName).ConfigureAwait(false);
        var providers = new List<Lazy<INuGetResourceProvider>>();
        providers.AddRange(Repository.Provider.GetCoreV3());
        var settings = Settings.LoadDefaultSettings(
            GetNugetConfigLocation(),
            "NuGet.Config",
            new XPlatMachineWideSetting());
        var packageSourceProvider = new PackageSourceProvider(settings);
        var sourceRepositoryProvider = new SourceRepositoryProvider(packageSourceProvider, providers);
        foreach (var repo in sourceRepositoryProvider.GetRepositories())
        {
            if (repo.PackageSource.IsHttp)
            {
                var resource = repo.GetResource<RemoteV3FindPackageByIdResource>();
                if (resource is null)
                {
                    continue;
                }

                await using var fs = File.OpenWrite(
                    $"{outputPath}{Path.DirectorySeparatorChar}{packageName}.{version}.nupkg").ConfigureAwait(false);
                await resource.CopyNupkgToStreamAsync(
                    packageName,
                    NuGetVersion.Parse(version),
                    fs,
                    new SourceCacheContext(),
                    NullLogger.Instance,
                    CancellationToken.None).ConfigureAwait(false);
            }
            else
            {
                var resource = repo.GetResource<LocalV3FindPackageByIdResource>();
                if (resource is null)
                {
                    continue;
                }

                await using var fs = File.OpenWrite(
                    $"{outputPath}{Path.DirectorySeparatorChar}{packageName}.{version}.nupkg").ConfigureAwait(false);
                await resource.CopyNupkgToStreamAsync(
                    packageName,
                    NuGetVersion.Parse(version),
                    fs,
                    new SourceCacheContext(),
                    NullLogger.Instance,
                    CancellationToken.None).ConfigureAwait(false);
            }
        }
    }

    internal static (string version, string fileName) GetDownloadedPackageVersion(string packageName, string inputPath)
    {
        var files = Directory.EnumerateFiles(
            inputPath,
            $"{packageName}-*").ToList();
        string version = string.Empty;
        string file = string.Empty;
        if (files.Any())
        {
            file = files.First();
            version = file[(file.IndexOf("-", StringComparison.Ordinal) + 1)..file.IndexOf(".nupkg", StringComparison.Ordinal)];
        }

        return (version, file);
    }

    internal static string DeletePackage(string packageName, string inputPath)
    {
        var result = GetDownloadedPackageVersion(packageName, inputPath);
        File.Delete(result.fileName);
        return result.version;
    }

    internal static async Task<string> ResolveWildcardPackageVersionAsync(string packageName)
    {
        // Connect to the official package repository
        var providers = new List<Lazy<INuGetResourceProvider>>();
        providers.AddRange(Repository.Provider.GetCoreV3());
        var settings = Settings.LoadDefaultSettings(
            GetNugetConfigLocation(),
            "NuGet.Config",
            new XPlatMachineWideSetting());
        var packageSourceProvider = new PackageSourceProvider(settings);
        var sourceRepositoryProvider = new SourceRepositoryProvider(packageSourceProvider, providers);
        foreach (var repo in sourceRepositoryProvider.GetRepositories())
        {
            Console.WriteLine(repo.PackageSource.Name);
            if (repo.PackageSource.IsHttp)
            {
                var resource = repo.GetResource<RemoteV3FindPackageByIdResource>();
                if (resource is null)
                {
                    continue;
                }

                var packagesEnumerable = await resource.GetAllVersionsAsync(
                    packageName,
                    new SourceCacheContext(),
                    NullLogger.Instance,
                    CancellationToken.None).ConfigureAwait(false);
                var packages = packagesEnumerable.ToList();
                if (packages.Any())
                {
                    for (var i = 0; i < packages.Count; i++)
                    {
                        if (i is not 0
                            && packages[i] > packages[i - 1])
                        {
                            packages.Remove(packages[i - 1]);
                        }
                    }

                    return packages[0].ToFullString();
                }
            }
            else
            {
                var resource = repo.GetResource<LocalV3FindPackageByIdResource>();
                if (resource is null)
                {
                    continue;
                }

                var packagesEnumerable = await resource.GetAllVersionsAsync(
                    packageName,
                    new SourceCacheContext(),
                    NullLogger.Instance,
                    CancellationToken.None).ConfigureAwait(false);
                var packages = packagesEnumerable.ToList();
                if (packages.Any())
                {
                    for (var i = 0; i < packages.Count; i++)
                    {
                        if (i is not 0
                            && packages[i] > packages[i - 1])
                        {
                            packages.Remove(packages[i - 1]);
                        }
                    }

                    return packages[0].ToFullString();
                }
            }
        }

        return string.Empty;
    }

    private static string GetNugetConfigLocation()
    {
        return (OperatingSystem.IsWindows(), OperatingSystem.IsLinux(), OperatingSystem.IsMacOS()) switch
        {
            (true, false, false) => Path.Join(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "NuGet"),
            (false, true, false) or (false, false, true) => Path.Join(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".nuget" /* ".config" */,
                "NuGet"),
            _ => throw new InvalidOperationException("Update OS specific nuget.config finder!"),
        };
    }
}
