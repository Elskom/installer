// set the console title.
Console.Title = "Elskom workload cross-platform installer";

// Need to register the code pages provider for code that parses
// and later needs ISO-8859-2
Encoding.RegisterProvider(
    CodePagesEncodingProvider.Instance);

// Test that it loads
_ = Encoding.GetEncoding("ISO-8859-2");
var app = new CommandApp<InstallCommand>();
app.Configure(config =>
{
    _ = config.AddCommand<InstallCommand>("install").WithDescription("Installs the Workload.");
    _ = config.AddCommand<UninstallCommand>("uninstall").WithDescription("Uninstalls the Workload.");
    _ = config.AddCommand<UpdateCommand>("update").WithDescription("Updates the Workload.");
});

using (NuGetHelper.HttpClient = new HttpClient())
{
    var result = await app.RunAsync(args).ConfigureAwait(false);
    Console.Title = "";
    return result;
}
