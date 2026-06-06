using System.Diagnostics;

namespace IsaacSimSharp.Tests;

/// <summary>
/// Launches <c>mock/mock_bridge.py</c> on a dedicated endpoint for the duration of a test
/// class, and tears it down afterwards. Requires the Python 3.12 launcher (<c>py -3.12</c>)
/// with <c>pyzmq</c> + <c>protobuf</c> installed.
/// </summary>
public sealed class MockBridgeFixture : IAsyncLifetime
{
    public string Endpoint { get; } = "tcp://127.0.0.1:5611";

    private Process? _process;

    public async Task InitializeAsync()
    {
        var repoRoot = FindRepoRoot();
        var script = Path.Combine(repoRoot, "mock", "mock_bridge.py");

        var psi = new ProcessStartInfo("py", $"-3.12 \"{script}\" --command-endpoint {Endpoint}")
        {
            WorkingDirectory = repoRoot,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        _process = Process.Start(psi)
            ?? throw new InvalidOperationException("Failed to start mock_bridge.py (is 'py -3.12' on PATH?).");

        await WaitForReadyAsync();
    }

    private async Task WaitForReadyAsync()
    {
        using var client = IsaacSimClient.Connect(Endpoint);
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(20);
        while (DateTime.UtcNow < deadline)
        {
            if (_process is { HasExited: true })
                throw new InvalidOperationException($"mock_bridge.py exited early:\n{await ReadProcessOutputAsync()}");

            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(500));
                if (await client.PingAsync("ready", cts.Token) == "ready")
                    return;
            }
            catch (Exception ex) when (ex is TimeoutException or OperationCanceledException)
            {
                await Task.Delay(150);
            }
        }

        if (_process is { HasExited: false })
            _process.Kill(entireProcessTree: true);
        throw new InvalidOperationException(
            $"mock_bridge.py did not become ready within 20s.\n{await ReadProcessOutputAsync()}");
    }

    private async Task<string> ReadProcessOutputAsync()
    {
        if (_process is null)
            return "(no process)";
        var stdout = await _process.StandardOutput.ReadToEndAsync();
        var stderr = await _process.StandardError.ReadToEndAsync();
        return $"stdout:\n{stdout}\nstderr:\n{stderr}";
    }

    public Task DisposeAsync()
    {
        if (_process is { HasExited: false })
        {
            _process.Kill(entireProcessTree: true);
            _process.WaitForExit(2000);
        }
        _process?.Dispose();
        return Task.CompletedTask;
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "IsaacSimSharp.slnx")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("Could not locate repo root (IsaacSimSharp.slnx).");
    }
}
