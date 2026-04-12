namespace SmppSharp.Protocol;

/// <summary>Well-known SMPP TLV (optional parameter) tags.</summary>
public static class TlvTag
{
    public const ushort DestAddrSubunit       = 0x0005;
    public const ushort SourceAddrSubunit     = 0x000D;
    public const ushort MessagePayload        = 0x0424; // for messages > 254 bytes
    public const ushort ReceiptedMessageId    = 0x001E; // in delivery receipts
    public const ushort MessageState         = 0x0427; // in delivery receipts
    public const ushort NetworkErrorCode     = 0x0423;
    public const ushort SarMsgRefNum         = 0x020C; // multipart reference number
    public const ushort SarTotalSegments     = 0x020E; // total segments
    public const ushort SarSegmentSeqnum     = 0x020F; // segment sequence number
    public const ushort UserMessageReference = 0x0204;
    public const ushort LanguageIndicator    = 0x020D;
    public const ushort CallbackNum          = 0x0381;
    public const ushort PrivacyIndicator     = 0x0201;
}
