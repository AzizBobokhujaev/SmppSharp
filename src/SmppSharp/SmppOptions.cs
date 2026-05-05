using SmppSharp.Protocol;

namespace SmppSharp;

/// <summary>Configuration for an SMPP client connection.</summary>
public sealed class SmppOptions
{
    public string Host       { get; set; } = "localhost";
    public int    Port       { get; set; } = 2775;
    public string SystemId   { get; set; } = "";
    public string Password   { get; set; } = "";
    public string SystemType { get; set; } = "";

    public BindMode BindMode { get; set; } = BindMode.Transceiver;

    /// <summary>Interval between enquire_link keepalive PDUs. Default: 30s.</summary>
    public TimeSpan EnquireLinkInterval { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>How long to wait for a PDU response before throwing. Default: 30s.</summary>
    public TimeSpan ResponseTimeout { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>Delay before each reconnect attempt. Default: 5s.</summary>
    public TimeSpan ReconnectDelay { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>Whether to automatically reconnect on disconnect. Default: true.</summary>
    public bool AutoReconnect { get; set; } = true;

    /// <summary>Maximum reconnect attempts. 0 = infinite.</summary>
    public int MaxReconnectAttempts { get; set; } = 0;

    /// <summary>TCP connect timeout. Default: 15s. Prevents waiting minutes for OS timeout.</summary>
    public TimeSpan ConnectTimeout { get; set; } = TimeSpan.FromSeconds(15);

    /// <summary>Enable TCP KeepAlive to detect dead connections faster. Default: true.</summary>
    public bool TcpKeepAlive { get; set; } = true;

    /// <summary>TCP KeepAlive interval in seconds. Default: 15s.</summary>
    public int TcpKeepAliveInterval { get; set; } = 15;

    // ── SSL/TLS ──────────────────────────────────────────────────

    /// <summary>Enable SSL/TLS. Default: false.</summary>
    public bool UseSsl { get; set; } = false;

    /// <summary>TLS target hostname. Defaults to Host if not set.</summary>
    public string? SslTargetHost { get; set; }

    /// <summary>Skip certificate validation (useful for self-signed certs in dev). Default: false.</summary>
    public bool AllowUntrustedCertificate { get; set; } = false;

    /// <summary>
    /// When true, multipart messages use UDH (User Data Header) for concatenation.
    /// When false, uses SAR TLV parameters instead.
    /// UDH is more widely supported by operator SMSCs and handsets. Default: true.
    /// </summary>
    public bool UseUdh { get; set; } = true;

    /// <summary>
    /// When true, long messages are sent as a single submit_sm using the message_payload TLV (0x0424)
    /// instead of being split into multiple submit_sm PDUs.
    /// Use this when the SMSC handles splitting/concatenation on its own side.
    /// Default: false.
    /// </summary>
    public bool UseMessagePayload { get; set; } = false;
}
