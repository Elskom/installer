using System.Text.RegularExpressions;
using NuGet.Common;
using NuGet.Configuration;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;
using NuGet.PackageManagement;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGet.ProjectManagement;
using NuGet.Resolver;

namespace Elskom.Check;

internal static class NuGetHelper
{
    internal static async Task InstallPackageAsync(string packageName, string outputPath)
    {
        /*
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
        */
    }

    internal static async Task DownloadPackageAsync(string packageName, string outputPath)
    {
        var version = await ResolveWildcardPackageVersionAsync(packageName).ConfigureAwait(false);
        var cache = new SourceCacheContext();
        var repository = Repository.Factory.GetCoreV3("https://api.nuget.org/v3/index.json");
        var resource = await repository.GetResourceAsync<FindPackageByIdResource>();
        await using var packageStream = File.OpenWrite(
            $"{outputPath}{Path.DirectorySeparatorChar}{packageName}.{version}.nupkg");
        _ = await resource.CopyNupkgToStreamAsync(
            packageName,
            new NuGetVersion(version),
            packageStream,
            cache,
            NullLogger.Instance,
            CancellationToken.None).ConfigureAwait(false);
        Console.WriteLine($"Downloaded package {packageName} {version}!");
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
        var cache = new SourceCacheContext();
        var repository = Repository.Factory.GetCoreV3("https://api.nuget.org/v3/index.json");
        var resource = await repository.GetResourceAsync<FindPackageByIdResource>();
        var versions = (await resource.GetAllVersionsAsync(
            packageName,
            cache,
            NullLogger.Instance,
            CancellationToken.None).ConfigureAwait(false)).ToList();
        if (versions.Count == 1)
        {
            Console.WriteLine($"'{packageName}' Version is '{versions[0].ToFullString()}'");
            return versions[0].ToFullString();
        }

        for (var i = 0; i < versions.Count; i++)
        {
            if (i > 0)
            {
                if (versions[i] > versions[i - 1])
                {
                    continue;
                }

                Console.WriteLine($"'{packageName}' Version is '{versions[i].ToFullString()}'");
                return versions[i].ToFullString();
            }
        }

        return string.Empty;
    }
}
