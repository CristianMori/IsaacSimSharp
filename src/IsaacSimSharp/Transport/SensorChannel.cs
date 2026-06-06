using System.Runtime.CompilerServices;
using System.Threading.Channels;
using Google.Protobuf;
using IsaacSimSharp.Protocol;
using NetMQ;
using NetMQ.Sockets;

namespace IsaacSimSharp.Transport;

/// <summary>
/// Owns the SUB socket that receives pushed <see cref="SensorFrame"/> messages. A single poller
/// thread parses frames and fans them out to any active <see cref="Stream"/> consumers, optionally
/// filtered by sensor handle.
/// </summary>
internal sealed class SensorChannel : IDisposable
{
    private readonly SubscriberSocket _socket;
    private readonly NetMQPoller _poller;
    private readonly object _gate = new();
    private readonly List<Subscriber> _subscribers = new();

    private sealed record Subscriber(string? Handle, ChannelWriter<SensorFrame> Writer);

    public SensorChannel(string endpoint)
    {
        _socket = new SubscriberSocket();
        _socket.Connect(endpoint);
        _socket.SubscribeToAnyTopic(); // receive all handles; filter client-side per consumer
        _socket.ReceiveReady += OnReceiveReady;
        _poller = new NetMQPoller { _socket };
        _poller.RunAsync();
    }

    /// <summary>Yields frames as they arrive, optionally filtered to a single sensor handle.</summary>
    public async IAsyncEnumerable<SensorFrame> Stream(string? handle, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var channel = Channel.CreateUnbounded<SensorFrame>(new UnboundedChannelOptions { SingleReader = true });
        var subscriber = new Subscriber(handle, channel.Writer);
        lock (_gate)
            _subscribers.Add(subscriber);
        try
        {
            await foreach (var frame in channel.Reader.ReadAllAsync(cancellationToken).ConfigureAwait(false))
                yield return frame;
        }
        finally
        {
            lock (_gate)
                _subscribers.Remove(subscriber);
            channel.Writer.TryComplete();
        }
    }

    private void OnReceiveReady(object? sender, NetMQSocketEventArgs e)
    {
        var message = e.Socket.ReceiveMultipartMessage();
        if (message.FrameCount < 2)
            return;

        SensorFrame frame;
        try
        {
            var payload = message[1];
            frame = SensorFrame.Parser.ParseFrom(new ReadOnlySpan<byte>(payload.Buffer, 0, payload.MessageSize));
        }
        catch (InvalidProtocolBufferException)
        {
            return;
        }

        lock (_gate)
        {
            foreach (var subscriber in _subscribers)
                if (subscriber.Handle is null || subscriber.Handle == frame.Handle)
                    subscriber.Writer.TryWrite(frame);
        }
    }

    public void Dispose()
    {
        try
        {
            _poller.Stop();
        }
        catch
        {
            // best-effort
        }

        _poller.Dispose();
        _socket.Dispose();

        lock (_gate)
        {
            foreach (var subscriber in _subscribers)
                subscriber.Writer.TryComplete();
            _subscribers.Clear();
        }
    }
}
