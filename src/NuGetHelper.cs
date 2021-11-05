namespace Elskom.Check;

internal static class NuGetHelper
{
    internal static HttpClient? HttpClient { get; set; }

    internal static async Task InstallPackageAsync(string packageName, string version, string outputPath)
    {
        var packageResponse = await HttpClient!.GetAsync($"https://api.nuget.org/v3-flatcontainer/{packageName}/{version}/{packageName}.{version}.nupkg");
        if (!packageResponse.IsSuccessStatusCode)
        {
            return;
        }

        var extractionPath = $"{outputPath}/{packageName}/{version}";
        var filePath = Path.Combine(extractionPath, $"{packageName}.nupkg");
        Directory.CreateDirectory(extractionPath);
        await File.WriteAllBytesAsync(filePath, await packageResponse.Content.ReadAsByteArrayAsync());
        ZipFile.ExtractToDirectory(filePath, extractionPath, true);
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
        var versionResponse = await HttpClient!.GetAsync($"https://api.nuget.org/v3-flatcontainer/{packageName}/index.json").ConfigureAwait(false);
        if (!versionResponse.IsSuccessStatusCode)
        {
            return string.Empty;
        }

        var content = await versionResponse.Content.ReadAsStringAsync().ConfigureAwait(false);
        var versionsInfo = JsonSerializer.Deserialize<VersionInfo>(content);
        var version = versionsInfo?.Versions?.LastOrDefault();
        Console.WriteLine($"'{packageName}' Version is '{version ?? "null"}'");
        return version ?? string.Empty;
    }

    private record VersionInfo
    {
        [JsonPropertyName("versions")]
        public string[]? Versions { get; init; }
    }
}
