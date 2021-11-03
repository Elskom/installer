using NuGet.Common;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;

namespace Elskom.Check;

internal static class NuGetHelper
{
    internal static async Task InstallPackageAsync(string packageName, string version, string outputPath)
    {

        /*
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

    internal static async Task<string> ResolveWildcardPackageVersionAsync(string packageName)
    {
        // Connect to the official package repository
        SourceRepository repository = Repository.Factory.GetCoreV3("https://api.nuget.org/v3/index.json");
        PackageMetadataResource resource = await repository.GetResourceAsync<PackageMetadataResource>(CancellationToken.None).ConfigureAwait(false);
        IEnumerable<IPackageSearchMetadata> packages = await resource.GetMetadataAsync(
            packageName,
            includePrerelease: true,
            includeUnlisted: false,
            new SourceCacheContext(),
            NullLogger.Instance,
            CancellationToken.None);
        var version = packages.MaxBy(packages => packages.Identity.Version)?.Identity.Version;
        Console.WriteLine($"'{packageName}' Version is '{version?.ToFullString()}'");
        return version is not null ? version.ToFullString() : string.Empty;
    }
}
