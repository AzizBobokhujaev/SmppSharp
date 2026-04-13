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

    // ── SSL/TLS ──────────────────────────────────────────────────

    /// <summary>Enable SSL/TLS. Default: false.</summary>
    public bool UseSsl { get; set; } = false;

    /// <summary>TLS target hostname. Defaults to Host if not set.</summary>
    public string? SslTargetHost { get; set; }

    /// <summary>Skip certificate validation (useful for self-signed certs in dev). Default: false.</summary>
    public bool AllowUntrustedCertificate { get; set; } = false;
}
