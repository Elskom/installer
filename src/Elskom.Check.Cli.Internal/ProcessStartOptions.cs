namespace Elskom.Check;

internal sealed class ProcessStartOptions
{
    internal ProcessStartInfo? StartInfo { get; private set; }

    public bool WaitForProcessExit { get; set; }

    internal ProcessStartOptions WithStartInformation(string fileName, string arguments, bool redirectStandardOutput, bool redirectStandardError, bool useShellExecute, bool createNoWindow, ProcessWindowStyle windowStyle, string workingDirectory)
    {
        this.StartInfo = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            RedirectStandardOutput = redirectStandardOutput,
            RedirectStandardError = redirectStandardError,
            UseShellExecute = useShellExecute,
            CreateNoWindow = createNoWindow,
            WindowStyle = windowStyle,
            WorkingDirectory = workingDirectory,
        };
        return this;
    }

    internal string Start()
    {
        if (this.StartInfo is null)
        {
            throw new InvalidOperationException("StartInfo must not be null.");
        }

        if (!File.Exists(this.StartInfo.FileName))
        {
            throw new FileNotFoundException("File to execute does not exist.");
        }

        StringBuilder? stdout = null;
        StringBuilder? stderr = null;
        using var proc = Process.Start(this.StartInfo);
        proc!.OutputDataReceived += (_, e) =>
        {
            stdout ??= new StringBuilder();
            _ = stdout.AppendLine(e.Data);
        };
        proc.ErrorDataReceived += (_, e) =>
        {
            stderr ??= new StringBuilder();
            _ = stderr.AppendLine(e.Data);
        };
        if (this.StartInfo.RedirectStandardOutput)
        {
            proc.BeginOutputReadLine();
        }

        if (this.StartInfo.RedirectStandardError)
        {
            proc.BeginErrorReadLine();
        }

        if (this.WaitForProcessExit)
        {
            proc.WaitForExit();
        }

        return (stdout is not null, stderr is not null) switch
        {
            (true, false) => $"{stdout}",
            (true, true) => $@"{stdout}
{stderr}",
            (false, false) => string.Empty,
            (false, true) => $"{stderr}",
        };
    }
}
