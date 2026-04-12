namespace SmppSharp.Protocol;

public enum BindMode
{
    /// <summary>Send only.</summary>
    Transmitter,

    /// <summary>Receive only (MO + delivery receipts).</summary>
    Receiver,

    /// <summary>Send and receive. Most common mode.</summary>
    Transceiver
}
