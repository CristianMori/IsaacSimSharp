using System.Numerics;
using System.Runtime.CompilerServices;
using IsaacSimSharp.Protocol;
using IsaacSimSharp.Transport;

namespace IsaacSimSharp.Sensors;

/// <summary>
/// Sensor operations. Create a sensor, then either pull single frames on demand
/// (<see cref="GetFrameAsync"/>) or subscribe to a continuous push stream
/// (<see cref="StreamAsync"/>). Obtained from <see cref="IsaacSimClient.Sensors"/>.
/// </summary>
public sealed class SensorsApi
{
    private readonly CommandChannel _commands;
    private readonly SensorChannel _stream;

    internal SensorsApi(CommandChannel commands, SensorChannel stream)
    {
        _commands = commands;
        _stream = stream;
    }

    /// <summary>Creates an RTX camera; returns its handle (used for pull/subscribe).</summary>
    public async Task<string> CreateCameraAsync(
        string primPath,
        int width = 640,
        int height = 480,
        Vector3 position = default,
        Quaternion? orientation = null,
        bool depth = true,
        bool segmentation = false,
        CancellationToken cancellationToken = default)
    {
        var request = new CreateCameraRequest
        {
            PrimPath = primPath,
            Width = (uint)width,
            Height = (uint)height,
            Depth = depth,
            Segmentation = segmentation,
            Position = ToVec3(position),
        };
        if (orientation is { } q)
            request.Orientation = ToQuat(q);
        var reply = (await _commands.SendAsync(new Command { CreateCamera = request }, cancellationToken).ConfigureAwait(false)).EnsureOk();
        return reply.Sensor.Handle;
    }

    /// <summary>Creates an IMU sensor; returns its handle.</summary>
    public async Task<string> CreateImuAsync(string primPath, Vector3 position = default, CancellationToken cancellationToken = default)
    {
        var reply = (await _commands
            .SendAsync(new Command { CreateImu = new CreateImuRequest { PrimPath = primPath, Position = ToVec3(position) } }, cancellationToken)
            .ConfigureAwait(false)).EnsureOk();
        return reply.Sensor.Handle;
    }

    /// <summary>Creates a contact sensor on a body; returns its handle.</summary>
    public async Task<string> CreateContactAsync(
        string primPath,
        Vector3 position = default,
        double radius = 0.1,
        double minThreshold = 0.0,
        double maxThreshold = 1e7,
        CancellationToken cancellationToken = default)
    {
        var request = new CreateContactRequest
        {
            PrimPath = primPath,
            Position = ToVec3(position),
            Radius = radius,
            MinThreshold = minThreshold,
            MaxThreshold = maxThreshold,
        };
        var reply = (await _commands.SendAsync(new Command { CreateContact = request }, cancellationToken).ConfigureAwait(false)).EnsureOk();
        return reply.Sensor.Handle;
    }

    /// <summary>Creates an RTX lidar from a named config (e.g. "Example_Rotary"); returns its handle.</summary>
    public async Task<string> CreateLidarAsync(
        string primPath,
        string config = "Example_Rotary",
        Vector3 position = default,
        CancellationToken cancellationToken = default)
    {
        var request = new CreateLidarRequest { PrimPath = primPath, Config = config, Position = ToVec3(position) };
        var reply = (await _commands.SendAsync(new Command { CreateLidar = request }, cancellationToken).ConfigureAwait(false)).EnsureOk();
        return reply.Sensor.Handle;
    }

    /// <summary>
    /// Creates an RTX radar from a named config (e.g. "IWRL6432AOP"); returns its handle.
    /// Requires the bridge to be launched with <c>--motion-bvh</c>, otherwise the call faults
    /// with a clear error.
    /// </summary>
    public async Task<string> CreateRadarAsync(
        string primPath,
        string config = "IWRL6432AOP",
        Vector3 position = default,
        CancellationToken cancellationToken = default)
    {
        var request = new CreateRadarRequest { PrimPath = primPath, Config = config, Position = ToVec3(position) };
        var reply = (await _commands.SendAsync(new Command { CreateRadar = request }, cancellationToken).ConfigureAwait(false)).EnsureOk();
        return reply.Sensor.Handle;
    }

    /// <summary>Pulls a single, current frame for the sensor on demand (request/reply).</summary>
    public async Task<SensorFrame> GetFrameAsync(string handle, CancellationToken cancellationToken = default)
    {
        var reply = (await _commands
            .SendAsync(new Command { GetSensorFrame = new GetSensorFrameRequest { Handle = handle } }, cancellationToken)
            .ConfigureAwait(false)).EnsureOk();
        return reply.SensorFrame;
    }

    /// <summary>
    /// Subscribes to a sensor and yields frames as they are pushed, until the enumeration is
    /// cancelled or disposed (which unsubscribes automatically).
    /// </summary>
    public async IAsyncEnumerable<SensorFrame> StreamAsync(string handle, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        (await _commands.SendAsync(new Command { Subscribe = new SubscribeRequest { Handle = handle } }, cancellationToken).ConfigureAwait(false)).EnsureOk();
        try
        {
            await foreach (var frame in _stream.Stream(handle, cancellationToken).ConfigureAwait(false))
                yield return frame;
        }
        finally
        {
            try
            {
                await _commands
                    .SendAsync(new Command { Unsubscribe = new UnsubscribeRequest { Handle = handle } }, CancellationToken.None)
                    .ConfigureAwait(false);
            }
            catch
            {
                // best-effort unsubscribe on teardown
            }
        }
    }

    private static Vec3 ToVec3(Vector3 v) => new() { X = v.X, Y = v.Y, Z = v.Z };

    private static Quat ToQuat(Quaternion q) => new() { X = q.X, Y = q.Y, Z = q.Z, W = q.W };
}
