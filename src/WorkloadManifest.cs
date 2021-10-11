namespace Elskom.Check;

public class WorkloadManifest
{
    [JsonPropertyName("version")]
    public string Version { get; set; } = null!;

    [JsonPropertyName("workloads")]
    public Workload Workloads { get; set; } = null!;

    [JsonPropertyName("packs")]
    public WorkloadPacks Packs { get; set; } = null!;

    internal static WorkloadManifest Create(IReadOnlyDictionary<string, string> packVersions)
    {
        return new WorkloadManifest
        {
            Version = packVersions.GetValueOrDefault(Constants.SdkPackName)!,
            Workloads = Workload.Create(),
            Packs = WorkloadPacks.Create(packVersions),
        };
    }

    internal void WriteJsonFile(string sdkVersion)
    {
        var workloadFolder = DotNetSdkHelper.GetDotNetSdkWorkloadFolder(
            Constants.WorkloadName,
            sdkVersion);
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

    internal void WriteTargetsFile(string sdkVersion)
    {
        var workloadFolder = DotNetSdkHelper.GetDotNetSdkWorkloadFolder(
            Constants.WorkloadName,
            sdkVersion);
        File.WriteAllText(
            $"{workloadFolder}{Path.DirectorySeparatorChar}WorkloadManifest.targets",
            @$"<Project>

  <PropertyGroup Condition=""'$(DisableImplicitFrameworkReferences)' == ''"">
    <ElskomSdkFrameworkVersion Condition=""'$(ElskomSdkFrameworkVersion)' == ''"">{this.Packs.ElskomSdkApp.Version}</ElskomSdkFrameworkVersion>
  </PropertyGroup>

  <Import Project=""Sdk.props"" Sdk=""{Constants.SdkPackName}"" Condition=""'$(UseElskomSdk)' == 'true'"" />
  <Import Project=""Sdk.targets"" Sdk=""{Constants.SdkPackName}"" Condition=""'$(UseElskomSdk)' == 'true'"" />

</Project>
");
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
                    new List<string>
                    {
                        Constants.SdkPackName,
                        Constants.RefPackName,
                        Constants.RuntimePackName,
                        Constants.TemplatePackName,
                    }),
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

        internal static WorkloadPacks Create(IReadOnlyDictionary<string, string> packVersions)
            => new()
            {
                ElskomSdk = WorkloadPack.Create(Constants.Sdk, Constants.SdkPackName, packVersions),
                ElskomSdkAppRef = WorkloadPack.Create(Constants.Framework, Constants.RefPackName, packVersions),
                ElskomSdkApp = WorkloadPack.Create(Constants.Framework, Constants.RefPackName, packVersions),
                ElskomSdkTemplates = WorkloadPack.Create(Constants.Template, Constants.TemplatePackName, packVersions)
            };

        public class WorkloadPack
        {
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
                };

            internal void UpdateVersion(string version)
            {
                this.Version = version;
            }
        }
    }
}
