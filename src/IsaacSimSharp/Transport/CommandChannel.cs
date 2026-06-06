using System.Collections.Concurrent;
using Google.Protobuf;
using IsaacSimSharp.Protocol;
using NetMQ;
using NetMQ.Sockets;

namespace IsaacSimSharp.Transport;

/// <summary>
/// Owns the DEALER socket used for request/reply commands. All socket access happens
/// on a single NetMQ poller thread; outbound commands are marshaled onto that thread via
/// an <see cref="NetMQQueue{T}"/>, and replies are correlated back to callers by
/// <see cref="Command.Id"/> using <see cref="TaskCompletionSource{TResult}"/>.
/// </summary>
internal sealed class CommandChannel : IDisposable
{
    private readonly DealerSocket _socket;
    private readonly NetMQPoller _poller;
    private readonly NetMQQueue<Command> _outbound = new();
    private readonly ConcurrentDictionary<ulong, TaskCompletionSource<Reply>> _pending = new();
    private readonly TimeSpan _timeout;
    private long _nextId;

    public CommandChannel(string endpoint, TimeSpan timeout)
    {
        _timeout = timeout;
        _socket = new DealerSocket();
        _socket.Connect(endpoint);

        _outbound.ReceiveReady += OnOutboundReady;
        _socket.ReceiveReady += OnInboundReady;

        _poller = new NetMQPoller { _socket, _outbound };
        _poller.RunAsync();
    }

    /// <summary>Sends a command and awaits its correlated reply (or times out).</summary>
    public async Task<Reply> SendAsync(Command command, CancellationToken cancellationToken = default)
    {
        var id = (ulong)Interlocked.Increment(ref _nextId);
        command.Id = id;

        var tcs = new TaskCompletionSource<Reply>(TaskCreationOptions.RunContinuationsAsynchronously);
        _pending[id] = tcs;
        _outbound.Enqueue(command);

        try
        {
            return await tcs.Task.WaitAsync(_timeout, cancellationToken).ConfigureAwait(false);
        }
        catch (TimeoutException)
        {
            _pending.TryRemove(id, out _);
            throw new TimeoutException($"Isaac Sim command {id} timed out after {_timeout}.");
        }
        catch (OperationCanceledException)
        {
            _pending.TryRemove(id, out _);
            throw;
        }
    }

    private void OnOutboundReady(object? sender, NetMQQueueEventArgs<Command> e)
    {
        while (e.Queue.TryDequeue(out var command, TimeSpan.Zero))
            _socket.SendFrame(command.ToByteArray());
    }

    private void OnInboundReady(object? sender, NetMQSocketEventArgs e)
    {
        var bytes = e.Socket.ReceiveFrameBytes();
        Reply reply;
        try
        {
            reply = Reply.Parser.ParseFrom(bytes);
        }
        catch (InvalidProtocolBufferException)
        {
            return; // ignore malformed frames
        }

        if (_pending.TryRemove(reply.Id, out var pending))
            pending.TrySetResult(reply);
    }

    public void Dispose()
    {
        try
        {
            _poller.Stop();
        }
        catch
        {
            // ignored - best-effort shutdown
        }

        _poller.Dispose();
        _socket.Dispose();
        _outbound.Dispose();

        foreach (var pending in _pending.Values)
            pending.TrySetException(new ObjectDisposedException(nameof(CommandChannel)));
        _pending.Clear();
    }
}
