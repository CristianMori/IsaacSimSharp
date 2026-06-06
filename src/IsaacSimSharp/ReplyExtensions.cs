using IsaacSimSharp.Protocol;

namespace IsaacSimSharp;

internal static class ReplyExtensions
{
    /// <summary>Throws <see cref="IsaacSimException"/> if the reply indicates failure.</summary>
    public static Reply EnsureOk(this Reply reply)
    {
        if (!reply.Ok)
            throw new IsaacSimException(string.IsNullOrEmpty(reply.Error) ? "Bridge returned an error." : reply.Error);
        return reply;
    }
}
