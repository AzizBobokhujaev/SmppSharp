using System.Diagnostics.Metrics;

namespace SmppSharp;

/// <summary>
/// Diagnostic metrics for SmppSharp using System.Diagnostics.Metrics.
/// Compatible with OpenTelemetry, Prometheus, and any IMeterFactory consumer.
/// </summary>
public static class SmppMetrics
{
    /// <summary>Meter name. Use this to subscribe with OpenTelemetry: .AddMeter("SmppSharp").</summary>
    public const string MeterName = "SmppSharp";

    private static readonly Meter _meter = new(MeterName, "1.0.0");

    // ── Counters ──────────────────────────────────────────────────

    /// <summary>Total SMS segments successfully submitted (each multipart counts per segment).</summary>
    public static readonly Counter<long> MessagesSent =
        _meter.CreateCounter<long>("smpp.messages.sent", "segments",
            "Total SMS segments successfully submitted");

    /// <summary>Total submit_sm operations that failed.</summary>
    public static readonly Counter<long> MessagesFailed =
        _meter.CreateCounter<long>("smpp.messages.failed", "messages",
            "Total SMS submissions that failed");

    /// <summary>Total MO (mobile-originated) messages received.</summary>
    public static readonly Counter<long> MessagesReceived =
        _meter.CreateCounter<long>("smpp.messages.received", "messages",
            "Total MO messages received from handsets");

    /// <summary>Total delivery receipts received from the SMSC.</summary>
    public static readonly Counter<long> ReceiptsReceived =
        _meter.CreateCounter<long>("smpp.receipts.received", "receipts",
            "Total delivery receipts received");

    /// <summary>Total reconnect attempts (both successful and failed).</summary>
    public static readonly Counter<long> Reconnects =
        _meter.CreateCounter<long>("smpp.reconnects.total", "reconnects",
            "Total SMPP reconnect attempts");

    // ── Histograms ────────────────────────────────────────────────

    /// <summary>Time from sending submit_sm to receiving submit_sm_resp (ms).</summary>
    public static readonly Histogram<double> SubmitDuration =
        _meter.CreateHistogram<double>("smpp.submit.duration", "ms",
            "Round-trip time for submit_sm / submit_sm_resp");

    // ── UpDown counters ───────────────────────────────────────────

    /// <summary>Currently active (bound) SMPP connections.</summary>
    public static readonly UpDownCounter<int> ActiveConnections =
        _meter.CreateUpDownCounter<int>("smpp.connections.active", "connections",
            "Number of currently bound SMPP sessions");
}
