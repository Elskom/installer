namespace Elskom.Check;

internal sealed class ProcessStartOptions
{
    private bool Executing { get; set; }

    private bool Running { get; set; }

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

        this.Executing = true;
        StringBuilder? stdout = null;
        StringBuilder? stderr = null;
        using var proc = Process.Start(this.StartInfo);
        proc!.OutputDataReceived += (_, e) =>
        {
            if (stdout is null)
            {
                stdout = new StringBuilder();
                stdout.Append(e.Data);
                stdout.AppendLine();
            }
            else
            {
                stdout.AppendLine(e.Data);
            }
        };
        proc.ErrorDataReceived += (_, e) =>
        {
            if (stderr is null)
            {
                stderr = new StringBuilder();
                stderr.Append(e.Data);
                stderr.AppendLine();
            }
            else
            {
                stderr.AppendLine(e.Data);
            }
        };
        this.Running = true;
        this.Executing = false;
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

        this.Running = false;
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
