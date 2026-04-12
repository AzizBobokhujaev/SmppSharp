namespace SmppSharp.Protocol;

/// <summary>SMPP 3.4 command IDs.</summary>
public static class CommandId
{
    public const uint GenericNack         = 0x80000000;
    public const uint BindReceiver        = 0x00000001;
    public const uint BindReceiverResp    = 0x80000001;
    public const uint BindTransmitter     = 0x00000002;
    public const uint BindTransmitterResp = 0x80000002;
    public const uint BindTransceiver     = 0x00000009;
    public const uint BindTransceiverResp = 0x80000009;
    public const uint Unbind              = 0x00000006;
    public const uint UnbindResp          = 0x80000006;
    public const uint SubmitSm            = 0x00000004;
    public const uint SubmitSmResp        = 0x80000004;
    public const uint DeliverSm           = 0x00000005;
    public const uint DeliverSmResp       = 0x80000005;
    public const uint QuerySm             = 0x00000003;
    public const uint QuerySmResp         = 0x80000003;
    public const uint EnquireLink         = 0x00000015;
    public const uint EnquireLinkResp     = 0x80000015;

    public static bool IsResponse(uint commandId) => (commandId & 0x80000000) != 0;
}
