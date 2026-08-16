using System.Diagnostics;
using System.Text;

namespace SyntaxCircus.Credentials;

/// <summary>Default <see cref="IProcessRunner"/> implementation using <see cref="Process"/>.</summary>
public sealed class ProcessRunner : IProcessRunner
{
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(30);

    public async Task<ProcessResult> RunAsync(string executable, string arguments, CancellationToken ct, TimeSpan? timeout = null, string? stdinData = null)
    {
        var effectiveTimeout = timeout ?? DefaultTimeout;

        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = executable,
                Arguments = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                RedirectStandardInput = stdinData is not null,
                UseShellExecute = false,
                CreateNoWindow = true,
            },
        };

        var stdout = new StringBuilder();
        var stderr = new StringBuilder();

        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data is not null)
            {
                stdout.AppendLine(e.Data);
            }
        };

        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data is not null)
            {
                stderr.AppendLine(e.Data);
            }
        };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        if (stdinData is not null)
        {
            await process.StandardInput.WriteAsync(stdinData).ConfigureAwait(false);
            process.StandardInput.Close();
        }

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(effectiveTimeout);

        try
        {
            await process.WaitForExitAsync(timeoutCts.Token).ConfigureAwait(false);
            return new ProcessResult(process.ExitCode, stdout.ToString().TrimEnd(), stderr.ToString().TrimEnd(), TimedOut: false);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            try
            {
                process.Kill(entireProcessTree: true);
            }
            catch (InvalidOperationException)
            {
                // Process already exited between the timeout firing and Kill — nothing to do.
            }

            return new ProcessResult(ExitCode: -1, stdout.ToString().TrimEnd(), stderr.ToString().TrimEnd(), TimedOut: true);
        }
    }
}
