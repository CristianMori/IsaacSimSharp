namespace IsaacSimSharp;

/// <summary>
/// Connection settings for an <see cref="IsaacSimClient"/>.
/// </summary>
public sealed class IsaacSimClientOptions
{
    /// <summary>Default ZeroMQ endpoint for the request/reply command socket.</summary>
    public const string DefaultCommandEndpoint = "tcp://127.0.0.1:5599";

    /// <summary>Default ZeroMQ endpoint for the PUB/SUB sensor stream.</summary>
    public const string DefaultSensorEndpoint = "tcp://127.0.0.1:5600";

    /// <summary>ZeroMQ endpoint the client DEALER socket connects to (bridge ROUTER).</summary>
    public string CommandEndpoint { get; init; } = DefaultCommandEndpoint;

    /// <summary>ZeroMQ endpoint the client SUB socket connects to (bridge PUB).</summary>
    public string SensorEndpoint { get; init; } = DefaultSensorEndpoint;

    /// <summary>How long to wait for a reply before a command faults with <see cref="TimeoutException"/>.</summary>
    public TimeSpan RequestTimeout { get; init; } = TimeSpan.FromSeconds(30);
}
