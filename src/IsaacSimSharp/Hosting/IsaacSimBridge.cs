using System.Diagnostics;

namespace IsaacSimSharp.Hosting;

/// <summary>Settings for launching the Isaac Sim bridge process from C#.</summary>
public sealed class BridgeLaunchOptions
{
    /// <summary>Isaac Sim install dir (contains <c>python.bat</c>). Defaults to %ISAACSIM_HOME% or C:\isaacsim.</summary>
    public string IsaacSimHome { get; init; } =
        Environment.GetEnvironmentVariable("ISAACSIM_HOME") ?? @"C:\isaacsim";

    /// <summary>
    /// Folder containing the <c>isaacsim_bridge</c> Python package (the repo's <c>bridge/</c>).
    /// Defaults to %ISAACSIMSHARP_BRIDGE%; required if that variable is unset.
    /// </summary>
    public string? BridgeDirectory { get; init; } =
        Environment.GetEnvironmentVariable("ISAACSIMSHARP_BRIDGE");

    public string CommandEndpoint { get; init; } = IsaacSimClientOptions.DefaultCommandEndpoint;
    public string SensorEndpoint { get; init; } = IsaacSimClientOptions.DefaultSensorEndpoint;

    /// <summary>Show the Isaac Sim window (default headless).</summary>
    public bool Gui { get; init; }

    /// <summary>Enable Motion BVH (required for RTX radar; slower).</summary>
    public bool MotionBvh { get; init; }

    /// <summary>How long to wait for the bridge to become ready (first launch caches shaders).</summary>
    public TimeSpan StartupTimeout { get; init; } = TimeSpan.FromMinutes(3);

    public TimeSpan RequestTimeout { get; init; } = TimeSpan.FromSeconds(30);

    /// <summary>Optional callback receiving the bridge's stdout/stderr lines (for logging).</summary>
    public Action<string>? OnOutput { get; init; }
}

/// <summary>
/// A running bridge process plus a connected <see cref="IsaacSimClient"/>. Disposing it asks the
/// bridge to shut down, then terminates the process.
/// </summary>
public sealed class BridgeSession : IAsyncDisposable
{
    private readonly Process _process;

    internal BridgeSession(IsaacSimClient client, Process process)
    {
        Client = client;
        _process = process;
    }

    /// <summary>The connected client.</summary>
    public IsaacSimClient Client { get; }

    public async ValueTask DisposeAsync()
    {
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            await Client.ShutdownAsync(cts.Token).ConfigureAwait(false);
        }
        catch
        {
            // bridge may already be gone; fall through to kill
        }

        Client.Dispose();

        try
        {
            if (!_process.HasExited)
            {
                _process.Kill(entireProcessTree: true);
                _process.WaitForExit(5000);
            }
        }
        catch
        {
            // best-effort teardown
        }

        _process.Dispose();
    }
}

/// <summary>Launches the Isaac Sim bridge from C#.</summary>
public static class IsaacSimBridge
{
    /// <summary>
    /// Starts Isaac Sim with the bridge, waits until it answers, and returns a session whose
    /// <see cref="BridgeSession.Client"/> is ready to use. Dispose the session to shut it down.
    /// </summary>
    public static async Task<BridgeSession> LaunchAsync(BridgeLaunchOptions options, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);

        var bridgeDir = options.BridgeDirectory
            ?? throw new InvalidOperationException(
                "Set BridgeLaunchOptions.BridgeDirectory (or the ISAACSIMSHARP_BRIDGE env var) to the folder containing the isaacsim_bridge package.");
        var pythonBat = Path.Combine(options.IsaacSimHome, "python.bat");
        if (!File.Exists(pythonBat))
            throw new FileNotFoundException(
                $"Isaac Sim python.bat not found at '{pythonBat}'. Set BridgeLaunchOptions.IsaacSimHome or ISAACSIM_HOME.", pythonBat);

        var inner = $"\"{pythonBat}\" -m isaacsim_bridge"
            + $" --command-endpoint {options.CommandEndpoint}"
            + $" --sensor-endpoint {options.SensorEndpoint}"
            + (options.Gui ? " --gui" : string.Empty)
            + (options.MotionBvh ? " --motion-bvh" : string.Empty);

        var psi = new ProcessStartInfo("cmd.exe", $"/c \"{inner}\"")
        {
            WorkingDirectory = bridgeDir,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };
        var existingPythonPath = Environment.GetEnvironmentVariable("PYTHONPATH");
        psi.Environment["PYTHONPATH"] =
            string.IsNullOrEmpty(existingPythonPath) ? bridgeDir : $"{bridgeDir}{Path.PathSeparator}{existingPythonPath}";

        var process = Process.Start(psi)
            ?? throw new InvalidOperationException("Failed to start the bridge process.");

        // Always drain stdout/stderr, otherwise Isaac Sim's verbose startup logging fills the
        // pipe buffer and the bridge hangs. Forward lines to OnOutput when provided.
        var onOutput = options.OnOutput;
        process.OutputDataReceived += (_, e) => { if (e.Data is not null) onOutput?.Invoke(e.Data); };
        process.ErrorDataReceived += (_, e) => { if (e.Data is not null) onOutput?.Invoke(e.Data); };
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        var client = new IsaacSimClient(new IsaacSimClientOptions
        {
            CommandEndpoint = options.CommandEndpoint,
            SensorEndpoint = options.SensorEndpoint,
            RequestTimeout = options.RequestTimeout,
        });

        try
        {
            await WaitUntilReadyAsync(client, process, options.StartupTimeout, cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            client.Dispose();
            TryKill(process);
            throw;
        }

        return new BridgeSession(client, process);
    }

    private static async Task WaitUntilReadyAsync(IsaacSimClient client, Process process, TimeSpan timeout, CancellationToken cancellationToken)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (process.HasExited)
                throw new InvalidOperationException($"Bridge process exited during startup (exit code {process.ExitCode}).");

            try
            {
                using var attempt = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                attempt.CancelAfter(TimeSpan.FromMilliseconds(750));
                await client.PingAsync("ready", attempt.Token).ConfigureAwait(false);
                return;
            }
            catch (Exception ex) when (ex is TimeoutException or OperationCanceledException && !cancellationToken.IsCancellationRequested)
            {
                await Task.Delay(250, cancellationToken).ConfigureAwait(false);
            }
        }

        TryKill(process);
        throw new TimeoutException($"Bridge did not become ready within {timeout}.");
    }

    private static void TryKill(Process process)
    {
        try
        {
            if (!process.HasExited)
                process.Kill(entireProcessTree: true);
        }
        catch
        {
            // ignored
        }
    }
}
