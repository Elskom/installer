// set the console title.
Console.Title = "Elskom workload cross-platform installer";

// Need to register the code pages provider for code that parses
// and later needs ISO-8859-2
Encoding.RegisterProvider(
    CodePagesEncodingProvider.Instance);

// Test that it loads
_ = Encoding.GetEncoding("ISO-8859-2");
var app = new CommandApp();
app.Configure(config =>
{
    config.AddCommand<InstallCommand>("install").WithDescription("Installs the Workload.");
    config.AddCommand<UninstallCommand>("uninstall").WithDescription("Uninstalls the Workload.");
    config.AddCommand<UpdateCommand>("update").WithDescription("Updates the Workload.");
});

var finalArgs = new List<string>();
var firstArg = args.FirstOrDefault()?.Trim().ToLowerInvariant() ?? string.Empty;
if (firstArg != "install" && firstArg != "uninstall" && firstArg != "update")
{
    finalArgs.Add("install");
}

if (args.Any())
{
    finalArgs.AddRange(args);
}

using (NuGetHelper.HttpClient = new HttpClient())
{
    var result = await app.RunAsync(finalArgs).ConfigureAwait(false);
    Console.Title = "";
    return result;
}
