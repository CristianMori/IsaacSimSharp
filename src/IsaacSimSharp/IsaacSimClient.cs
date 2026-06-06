using IsaacSimSharp.Protocol;
using IsaacSimSharp.Robots;
using IsaacSimSharp.Scene;
using IsaacSimSharp.Sensors;
using IsaacSimSharp.Transport;

namespace IsaacSimSharp;

/// <summary>
/// Client for an Isaac Sim instance running the <c>isaacsim_bridge</c> over ZeroMQ + Protobuf.
/// Construct via <see cref="Connect(string?)"/> and call the async methods to drive the sim.
/// </summary>
public sealed class IsaacSimClient : IDisposable
{
    private readonly CommandChannel _commands;
    private readonly SensorChannel _sensorStream;

    public IsaacSimClient(IsaacSimClientOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _commands = new CommandChannel(options.CommandEndpoint, options.RequestTimeout);
        _sensorStream = new SensorChannel(options.SensorEndpoint);
        Scene = new SceneApi(_commands);
        Robots = new RobotsApi(_commands);
        Sensors = new SensorsApi(_commands, _sensorStream);
    }

    /// <summary>Scene-configuration operations (ground plane, lights, primitives, references).</summary>
    public SceneApi Scene { get; }

    /// <summary>Robot operations (register articulations, read/drive joints).</summary>
    public RobotsApi Robots { get; }

    /// <summary>Sensor operations (create cameras/IMUs, pull frames, subscribe to streams).</summary>
    public SensorsApi Sensors { get; }

    /// <summary>Connects to a bridge at the given command endpoint (defaults to localhost).</summary>
    public static IsaacSimClient Connect(string? commandEndpoint = null)
        => new(new IsaacSimClientOptions
        {
            CommandEndpoint = commandEndpoint ?? IsaacSimClientOptions.DefaultCommandEndpoint,
        });

    /// <summary>Round-trips a message through the bridge to verify connectivity.</summary>
    public async Task<string> PingAsync(string message = "ping", CancellationToken cancellationToken = default)
    {
        var reply = await _commands
            .SendAsync(new Command { Ping = new PingRequest { Message = message } }, cancellationToken)
            .ConfigureAwait(false);
        ThrowIfError(reply);
        return reply.Ping.Message;
    }

    /// <summary>Returns the Isaac Sim, bridge, and protocol versions reported by the server.</summary>
    public async Task<GetVersionReply> GetVersionAsync(CancellationToken cancellationToken = default)
    {
        var reply = await _commands
            .SendAsync(new Command { GetVersion = new GetVersionRequest() }, cancellationToken)
            .ConfigureAwait(false);
        ThrowIfError(reply);
        return reply.GetVersion;
    }

    // ----------------------------------------------------------------- lifecycle

    /// <summary>Creates a fresh, empty USD stage (discards the current one).</summary>
    public Task NewStageAsync(CancellationToken cancellationToken = default)
        => AckAsync(new Command { NewStage = new NewStageRequest() }, cancellationToken);

    /// <summary>Opens an existing USD stage from disk.</summary>
    public Task OpenStageAsync(string path, CancellationToken cancellationToken = default)
        => AckAsync(new Command { OpenStage = new OpenStageRequest { Path = path } }, cancellationToken);

    /// <summary>Starts (plays) the simulation timeline.</summary>
    public Task PlayAsync(CancellationToken cancellationToken = default)
        => AckAsync(new Command { Play = new PlayRequest() }, cancellationToken);

    /// <summary>Pauses the simulation timeline.</summary>
    public Task PauseAsync(CancellationToken cancellationToken = default)
        => AckAsync(new Command { Pause = new PauseRequest() }, cancellationToken);

    /// <summary>Stops the timeline, rewinding to the start.</summary>
    public Task StopAsync(CancellationToken cancellationToken = default)
        => AckAsync(new Command { Stop = new StopRequest() }, cancellationToken);

    /// <summary>Restores prims to their default initial state.</summary>
    public Task ResetAsync(CancellationToken cancellationToken = default)
        => AckAsync(new Command { Reset = new ResetRequest() }, cancellationToken);

    /// <summary>Sets the physics timestep in seconds (e.g. 1.0/60.0).</summary>
    public Task SetPhysicsDtAsync(double dt, CancellationToken cancellationToken = default)
        => AckAsync(new Command { SetPhysicsDt = new SetPhysicsDtRequest { Dt = dt } }, cancellationToken);

    /// <summary>Advances the simulation by <paramref name="count"/> frames; returns the new frame/time.</summary>
    public async Task<StepReply> StepAsync(uint count = 1, CancellationToken cancellationToken = default)
    {
        var reply = await _commands
            .SendAsync(new Command { Step = new StepRequest { Count = count } }, cancellationToken)
            .ConfigureAwait(false);
        ThrowIfError(reply);
        return reply.Step;
    }

    /// <summary>Exports the current stage to disk; returns the resolved absolute path written.</summary>
    public async Task<string> ExportUsdAsync(string path, CancellationToken cancellationToken = default)
    {
        var reply = await _commands
            .SendAsync(new Command { ExportUsd = new ExportUsdRequest { Path = path } }, cancellationToken)
            .ConfigureAwait(false);
        ThrowIfError(reply);
        return reply.ExportUsd.Path;
    }

    /// <summary>Asks the bridge to shut down Isaac Sim and exit.</summary>
    public Task ShutdownAsync(CancellationToken cancellationToken = default)
        => AckAsync(new Command { Shutdown = new ShutdownRequest() }, cancellationToken);

    /// <summary>Resolves the Isaac Sim assets root (for building asset URLs like robots).</summary>
    public async Task<string> GetAssetsRootAsync(CancellationToken cancellationToken = default)
    {
        var reply = await _commands
            .SendAsync(new Command { GetAssetsRoot = new GetAssetsRootRequest() }, cancellationToken)
            .ConfigureAwait(false);
        reply.EnsureOk();
        return reply.GetAssetsRoot.Path;
    }

    private async Task AckAsync(Command command, CancellationToken cancellationToken)
    {
        var reply = await _commands.SendAsync(command, cancellationToken).ConfigureAwait(false);
        reply.EnsureOk();
    }

    private static void ThrowIfError(Reply reply) => reply.EnsureOk();

    public void Dispose()
    {
        _commands.Dispose();
        _sensorStream.Dispose();
    }
}
