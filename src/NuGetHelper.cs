using System.Text.RegularExpressions;
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
                    $"{outputPath}{Path.DirectorySeparatorChar}{packageName}.{version}.nupkg");
                await resource.CopyNupkgToStreamAsync(
                    packageName,
                    NuGetVersion.Parse(version),
                    fs,
                    new SourceCacheContext(),
                    NullLogger.Instance,
                    CancellationToken.None).ConfigureAwait(false);
            }
            // else
            // {
            //     var resource = repo.GetResource<LocalV3FindPackageByIdResource>();
            //     if (resource is null)
            //     {
            //         continue;
            //     }

            //     await using var fs = File.OpenWrite(
            //         $"{outputPath}{Path.DirectorySeparatorChar}{packageName}.{version}.nupkg");
            //     await resource.CopyNupkgToStreamAsync(
            //         packageName,
            //         NuGetVersion.Parse(version),
            //         fs,
            //         new SourceCacheContext(),
            //         NullLogger.Instance,
            //         CancellationToken.None).ConfigureAwait(false);
            // }
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
            if (repo.PackageSource.IsHttp)
            {
                // Console.WriteLine(repo.PackageSource.Name);
                var resource = repo.GetResource<RemoteV2FindPackageByIdResource>();
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
                // var packages = await GetAllVersions(repo, packageName).ConfigureAwait(false);
                if (packages.Count == 1)
                {
                    Console.WriteLine($"'{packageName}' Version is '{packages[0].ToFullString()}'");
                    return packages[0].ToFullString();
                }

                for (var i = 0; i < packages.Count; i++)
                {
                    if (i > 0)
                    {
                        if (packages[i] > packages[i - 1])
                        {
                            continue;
                        }

                        Console.WriteLine($"'{packageName}' Version is '{packages[i].ToFullString()}'");
                        return packages[i].ToFullString();
                    }
                }
            }
            else
            {
                // Console.WriteLine($"Source Name: '{repo.PackageSource.Name}'");
                // Console.WriteLine($"Source Location: '{repo.PackageSource.Source}'");
                // Console.WriteLine($"Package Name: '{packageName}'");
                // var project = new FolderNuGetProject(repo.PackageSource.Source);
                // project.GetInstalledPath()
                // var resource = repo.GetResource<LocalV2FindPackageByIdResource>();
                // if (resource is null)
                // {
                //     continue;
                // }

                // var packagesEnumerable = await resource.GetAllVersionsAsync(
                //     packageName,
                //     new SourceCacheContext(),
                //     NullLogger.Instance,
                //     CancellationToken.None).ConfigureAwait(false);
                // var packages = packagesEnumerable.ToList();
                // var packages = await GetAllVersions(repo, packageName).ConfigureAwait(false);
                var packages = GetLocalPackageVersions(
                    repo.PackageSource.Source,
                    packageName);
                if (packages.Count == 1)
                {
                    Console.WriteLine($"'{packageName}' Version is '{packages[0].ToFullString()}'");
                    return packages[0].ToFullString();
                }

                for (var i = 0; i < packages.Count; i++)
                {
                    if (i > 0)
                    {
                        if (packages[i] > packages[i - 1])
                        {
                            continue;
                        }

                        Console.WriteLine($"'{packageName}' Version is '{packages[i].ToFullString()}'");
                        return packages[i].ToFullString();
                    }
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

    private static List<NuGetVersion> GetLocalPackageVersions(string inputPath, string packageName)
    {
        var results = new List<NuGetVersion>();
        foreach (var file in Directory.EnumerateFiles(
            inputPath,
            $"{packageName}.*.nupkg"))
        {
            var regex = new Regex(@"[a-zA-Z._]*(\d.\d.\d)", RegexOptions.IgnoreCase);
            var match = regex.Match(
                file.Replace(
                $"{inputPath}{Path.DirectorySeparatorChar}",
                string.Empty));
            var version = match.Groups[1].Value;
            if (!file.Equals($"{inputPath}{Path.DirectorySeparatorChar}{packageName}.{version}.nupkg"))
            {
                continue;
            }

            results.Add(NuGetVersion.Parse(version));
        }

        return results;
    }
}
