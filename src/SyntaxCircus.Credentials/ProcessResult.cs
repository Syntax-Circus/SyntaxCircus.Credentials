namespace SyntaxCircus.Credentials;

/// <summary>Result of executing an external process.</summary>
public sealed record ProcessResult(int ExitCode, string StdOut, string StdErr, bool TimedOut);
