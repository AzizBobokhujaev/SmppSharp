namespace SmppSharp.Protocol;

/// <summary>Parsed SMPP PDU.</summary>
internal sealed class Pdu
{
    public uint CommandLength  { get; init; }
    public uint CommandId      { get; init; }
    public uint CommandStatus  { get; init; }
    public uint SequenceNumber { get; init; }
    public byte[] Body         { get; init; } = [];

    public bool IsOk => CommandStatus == Protocol.CommandStatus.Ok;
}
