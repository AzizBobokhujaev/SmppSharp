namespace SmppSharp.Models;

/// <summary>Result of a successful submit_sm operation.</summary>
public sealed class SubmitResult
{
    /// <summary>
    /// Message ID assigned by the SMSC (from submit_sm_resp).
    /// For multipart messages — ID of the last segment.
    /// </summary>
    public required string MessageId { get; init; }

    /// <summary>Number of SMS segments sent (1 for short messages).</summary>
    public int SegmentCount { get; init; } = 1;

    /// <summary>Encoding used: 0x00 = GSM7, 0x08 = UCS2.</summary>
    public byte DataCoding { get; init; }

    /// <summary>Correlation ID passed in the original <see cref="SubmitRequest"/>.</summary>
    public string? CorrelationId { get; init; }

    public bool IsMultipart => SegmentCount > 1;
}
