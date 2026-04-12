namespace SmppSharp.Models;

/// <summary>Delivery receipt received from SMSC via deliver_sm.</summary>
public sealed class DeliveryReceipt
{
    /// <summary>Original message_id from submit_sm_resp.</summary>
    public required string MessageId { get; init; }

    public required DeliveryStatus Status { get; init; }

    /// <summary>Error code string from the receipt (e.g. "000", "099").</summary>
    public string? ErrorCode { get; init; }

    public int Submitted  { get; init; }
    public int Delivered  { get; init; }

    public DateTime? SubmitDate { get; init; }
    public DateTime? DoneDate   { get; init; }

    public DateTime ReceivedAt { get; init; } = DateTime.UtcNow;
}

public enum DeliveryStatus
{
    Delivered,
    Failed,
    Rejected,
    Expired,
    Undeliverable,
    Unknown
}
