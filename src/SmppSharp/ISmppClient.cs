using SmppSharp.Models;

namespace SmppSharp;

/// <summary>
/// SMPP 3.4 client. Manages a persistent TCP session with automatic keepalive and reconnect.
/// </summary>
public interface ISmppClient : IAsyncDisposable
{
    /// <summary>True when the session is bound and ready to send/receive.</summary>
    bool IsConnected { get; }

    // ── Events ───────────────────────────────────────────────────

    /// <summary>Fired when an incoming MO message is received.</summary>
    event Func<DeliverMessage, Task>  OnMessageReceived;

    /// <summary>Fired when a delivery receipt arrives from the SMSC.</summary>
    event Func<DeliveryReceipt, Task> OnDeliveryReceived;

    /// <summary>Fired when the connection is lost (before any reconnect attempt).</summary>
    event Action<Exception?> OnDisconnected;

    /// <summary>Fired after a successful reconnect.</summary>
    event Action OnReconnected;

    // ── Connection ───────────────────────────────────────────────

    /// <summary>Connects to the SMSC and performs the bind operation.</summary>
    Task ConnectAsync(CancellationToken ct = default);

    /// <summary>Sends unbind and closes the TCP connection.</summary>
    Task DisconnectAsync(CancellationToken ct = default);

    // ── Sending ──────────────────────────────────────────────────

    /// <summary>
    /// Submits an SMS. Automatically splits into multipart segments if needed.
    /// Encoding is auto-detected: GSM7 for Latin, UCS2 for Cyrillic/Unicode.
    /// </summary>
    Task<SubmitResult> SubmitAsync(SubmitRequest request, CancellationToken ct = default);

    /// <summary>
    /// Submits multiple SMS messages with controlled concurrency.
    /// </summary>
    Task<IReadOnlyList<SubmitResult>> SubmitBulkAsync(
        IEnumerable<SubmitRequest> requests,
        int maxConcurrency = 10,
        CancellationToken ct = default);
}
