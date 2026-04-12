namespace SmppSharp.Protocol;

/// <summary>SMPP 3.4 command status codes.</summary>
public static class CommandStatus
{
    public const uint Ok                = 0x00000000;
    public const uint InvalidMsgLen     = 0x00000001;
    public const uint InvalidCmdLen     = 0x00000002;
    public const uint InvalidCmdId      = 0x00000003;
    public const uint BindFailed        = 0x00000004; // incorrect bind status
    public const uint AlreadyBound      = 0x00000005;
    public const uint InvalidPriority   = 0x00000006;
    public const uint InvalidMsgId      = 0x00000007;
    public const uint SysErr            = 0x00000008;
    public const uint InvalidSrcAddr    = 0x0000000A;
    public const uint InvalidDstAddr    = 0x0000000B;
    public const uint InvalidPassword   = 0x0000000E;
    public const uint InvalidSystemId   = 0x0000000F;
    public const uint ThrottledError    = 0x00000058;
    public const uint MsgQueueFull      = 0x00000061;
    public const uint InvalidScheduled  = 0x00000061;

    public static string Describe(uint status) => status switch
    {
        Ok               => "OK",
        InvalidMsgLen    => "Invalid message length",
        InvalidCmdId     => "Invalid command ID",
        AlreadyBound     => "Already bound",
        SysErr           => "System error",
        InvalidPassword  => "Invalid password",
        InvalidSystemId  => "Invalid system ID",
        ThrottledError   => "Throttled — too many messages",
        _                => $"Unknown (0x{status:X8})"
    };
}
