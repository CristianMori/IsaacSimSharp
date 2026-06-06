namespace IsaacSimSharp;

/// <summary>
/// Raised when the Isaac Sim bridge returns an error reply (<c>Reply.ok == false</c>).
/// </summary>
public sealed class IsaacSimException : Exception
{
    public IsaacSimException(string message) : base(message) { }
}
