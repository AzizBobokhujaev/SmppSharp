namespace SmppSharp.Models;

/// <summary>Incoming MO (Mobile Originated) message from a subscriber.</summary>
public sealed class DeliverMessage
{
    public required string SourceAddress      { get; init; }
    public required string DestinationAddress { get; init; }
    public required string Message            { get; init; }
    public byte DataCoding                    { get; init; }
    public DateTime ReceivedAt                { get; init; } = DateTime.UtcNow;
}
