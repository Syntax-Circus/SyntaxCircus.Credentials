namespace SyntaxCircus.Credentials;

/// <summary>
/// Runs an external process and captures its output. Injectable so the macOS/Linux credential
/// stores (which shell out to <c>security</c>/<c>secret-tool</c>) can be tested without spawning
/// real processes.
/// </summary>
public interface IProcessRunner
{
    /// <summary>Executes an external process, capturing stdout and stderr.</summary>
    /// <param name="executable">Path or name of the executable.</param>
    /// <param name="arguments">Command-line arguments.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <param name="timeout">Optional timeout. Default is 30 seconds.</param>
    /// <param name="stdinData">Optional data to pipe to the process's standard input.</param>
    Task<ProcessResult> RunAsync(string executable, string arguments, CancellationToken ct, TimeSpan? timeout = null, string? stdinData = null);
}
