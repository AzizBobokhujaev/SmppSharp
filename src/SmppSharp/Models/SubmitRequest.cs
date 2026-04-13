namespace SmppSharp.Models;

/// <summary>Represents an outgoing SMS to be submitted via SMPP.</summary>
public sealed class SubmitRequest
{
    /// <summary>
    /// Sender ID (alphanumeric max 11 chars, or numeric up to 20).
    /// Examples: "PAYOMAK", "+998712345678"
    /// </summary>
    public required string SourceAddress { get; init; }

    /// <summary>
    /// Destination phone number. International format recommended: +998901234567
    /// </summary>
    public required string DestinationAddress { get; init; }

    /// <summary>Message text. Cyrillic/non-Latin detected automatically → UCS2.</summary>
    public required string Message { get; init; }

    /// <summary>Request delivery receipt from SMSC. Default: true.</summary>
    public bool RegisteredDelivery { get; init; } = true;

    /// <summary>Force UCS2 encoding even for ASCII-only messages.</summary>
    public bool ForceUcs2 { get; init; }

    /// <summary>Message validity period. null = SMSC default.</summary>
    public TimeSpan? ValidityPeriod { get; init; }

    /// <summary>
    /// Raw binary payload. When set, overrides <see cref="Message"/> and sends a binary SMS
    /// (data_coding = 0x04). Use for WAP Push, ringtones, OTA configs, etc.
    /// </summary>
    public byte[]? Payload { get; init; }

    /// <summary>
    /// Flash SMS (Class 0) — displayed immediately on the handset without being stored.
    /// </summary>
    public bool IsFlash { get; init; }

    /// <summary>
    /// Advanced: override esm_class byte. 0x40 = UDH indicator (set automatically for WAP Push).
    /// </summary>
    public byte EsmClass { get; init; } = 0x00;

    /// <summary>Optional arbitrary reference attached to this request (not sent to SMSC).</summary>
    public string? CorrelationId { get; init; }
}
