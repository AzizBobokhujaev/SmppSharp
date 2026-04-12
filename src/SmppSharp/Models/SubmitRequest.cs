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

    /// <summary>Optional arbitrary reference attached to this request (not sent to SMSC).</summary>
    public string? CorrelationId { get; init; }
}
