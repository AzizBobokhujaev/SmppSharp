namespace SmppSharp;

/// <summary>Exception thrown when an SMPP operation fails.</summary>
public sealed class SmppException : Exception
{
    /// <summary>SMPP command_status code, if available.</summary>
    public uint? CommandStatus { get; }

    public SmppException(string message) : base(message) { }

    public SmppException(string message, uint commandStatus)
        : base(message)
    {
        CommandStatus = commandStatus;
    }

    public SmppException(string message, Exception? inner)
        : base(message, inner) { }
}
