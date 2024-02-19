namespace Elskom.Check;

internal class WorkloadManifest
{
    [JsonPropertyName("version")]
    public string Version { get; set; } = null!;

    [JsonPropertyName("workloads")]
    public Workload Workloads { get; set; } = null!;

    [JsonPropertyName("packs")]
    public WorkloadPacks Packs { get; set; } = null!;

    internal static WorkloadManifest Create(IReadOnlyDictionary<string, string> packVersions)
        => new()
        {
            Version = packVersions.GetValueOrDefault(Constants.SdkPackName)!,
            Workloads = Workload.Create(),
            Packs = WorkloadPacks.Create(packVersions),
        };

    internal void WriteJsonFile(string sdkVersion, string runtimeIdentifier)
    {
        var workloadFolder = DotNetSdkHelper.GetDotNetSdkWorkloadFolder(
            Constants.WorkloadName,
            sdkVersion,
            runtimeIdentifier);
        if (!Directory.Exists(workloadFolder))
        {
            _ = Directory.CreateDirectory(workloadFolder);
        }

        var json = JsonSerializer.Serialize(
            this,
            new JsonSerializerOptions
            {
                WriteIndented = true,
            });
        File.WriteAllText(
            Path.Join(
                workloadFolder,
                "WorkloadManifest.json"),
            json);
    }

    internal void WriteTargetsFile(string sdkVersion, string runtimeIdentifier)
    {
        var workloadFolder = DotNetSdkHelper.GetDotNetSdkWorkloadFolder(
            Constants.WorkloadName,
            sdkVersion,
            runtimeIdentifier);
        File.WriteAllText(
            $"{workloadFolder}{Path.DirectorySeparatorChar}WorkloadManifest.targets",
            $"""
            <Project>

              <PropertyGroup>
                <!--Set the Framework version for the Sdk to use. -->
                <ElskomSdkFrameworkVersion>{this.Packs.ElskomSdkApp.Version}</ElskomSdkFrameworkVersion>
              </PropertyGroup>

              <!--
                  If we import the workload Sdk here for some reason
                  it would get imported for all projects and not just specific ones.

                  That behavior is never intended so all applications needing to use it
                  will have to set their Project Sdk node to Elskom.Sdk.
              -->

            </Project>

            """);
    }

    public class Workload
    {
        [JsonPropertyName("elskom")]
        public ElskomWorkload Elskom { get; set; } = null!;

        public class ElskomWorkload
        {
            [JsonPropertyName("description")]
            public string Description { get; set; } = null!;

            [JsonPropertyName("packs")]
            public List<string> Packs { get; set; } = null!;

            internal static ElskomWorkload Create(string description, List<string> packs)
                => new()
                {
                    Description = description,
                    Packs = packs,
                };
        }

        internal static Workload Create()
            => new()
            {
                Elskom = ElskomWorkload.Create(
                    ".NET SDK Workload for building Els_kom, and it's plugins.",
                    [
                        Constants.SdkPackName,
                        Constants.RefPackName,
                        Constants.RuntimePackName,
                        Constants.TemplatePackName,
                    ]),
            };
    }

    public class WorkloadPacks
    {
        [JsonPropertyName(Constants.SdkPackName)]
        public WorkloadPack ElskomSdk { get; set; } = null!;

        [JsonPropertyName(Constants.RefPackName)]
        public WorkloadPack ElskomSdkAppRef { get; set; } = null!;

        [JsonPropertyName(Constants.RuntimePackName)]
        public WorkloadPack ElskomSdkApp { get; set; } = null!;

        [JsonPropertyName(Constants.TemplatePackName)]
        public WorkloadPack ElskomSdkTemplates { get; set; } = null!;

        [JsonIgnore]
        public Dictionary<string, WorkloadPack> Packs { get; set; } = null!;

        internal static WorkloadPacks Create(IReadOnlyDictionary<string, string> packVersions)
            => new()
            {
                ElskomSdk = WorkloadPack.Create(Constants.Sdk, Constants.SdkPackName, packVersions),
                ElskomSdkAppRef = WorkloadPack.Create(Constants.Framework, Constants.RefPackName, packVersions),
                ElskomSdkApp = WorkloadPack.Create(Constants.Framework, Constants.RuntimePackName, packVersions),
                ElskomSdkTemplates = WorkloadPack.Create(Constants.Template, Constants.TemplatePackName, packVersions),
            };

        internal void SetPacksDictionary()
            => this.Packs = new Dictionary<string, WorkloadPack>()
            {
                { Constants.SdkPackName, this.ElskomSdk },
                { Constants.RefPackName, this.ElskomSdkAppRef },
                { Constants.RuntimePackName, this.ElskomSdkApp },
                { Constants.TemplatePackName, this.ElskomSdkTemplates },
            };

        public class WorkloadPack
        {
            [JsonIgnore]
            public string Name { get; set; } = null!;

            [JsonPropertyName("kind")]
            public string Kind { get; set; } = null!;

            [JsonPropertyName("version")]
            public string Version { get; set; } = null!;

            internal static WorkloadPack Create(
                string kind,
                string packName,
                IReadOnlyDictionary<string, string> packVersions)
                => new()
                {
                    Kind = kind,
                    Version = packVersions.GetValueOrDefault(packName)!,
                    Name = packName,
                };

            internal void UpdateVersion(string version)
            {
                this.Version = version;
            }
        }
    }
}
