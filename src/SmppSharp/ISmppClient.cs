using SmppSharp.Models;
using System.Runtime.CompilerServices;

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
    /// Submits an SMS. Automatically:
    /// <list type="bullet">
    ///   <item>Detects encoding (GSM7 for Latin, UCS2 for Cyrillic/Arabic/CJK/emoji)</item>
    ///   <item>Splits into multipart segments if needed</item>
    ///   <item>Handles binary and Flash SMS via <see cref="SubmitRequest"/> flags</item>
    /// </list>
    /// </summary>
    Task<SubmitResult> SubmitAsync(SubmitRequest request, CancellationToken ct = default);

    /// <summary>
    /// Submits multiple messages with controlled concurrency.
    /// Returns when all messages are sent.
    /// </summary>
    Task<IReadOnlyList<SubmitResult>> SubmitBulkAsync(
        IEnumerable<SubmitRequest> requests,
        int maxConcurrency = 10,
        CancellationToken ct = default);

    /// <summary>
    /// High-throughput streaming pipeline. Submits requests as they arrive and yields results
    /// as they complete. Suitable for bulk campaigns > 1000 msg/sec.
    /// </summary>
    IAsyncEnumerable<SubmitResult> SubmitPipelineAsync(
        IAsyncEnumerable<SubmitRequest> requests,
        int concurrency = 100,
        CancellationToken ct = default);
}
