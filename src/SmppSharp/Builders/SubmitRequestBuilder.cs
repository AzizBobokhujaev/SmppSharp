using SmppSharp.Models;

namespace SmppSharp.Builders;

/// <summary>
/// Fluent builder for <see cref="SubmitRequest"/>.
/// </summary>
/// <example>
/// <code>
/// var request = new SubmitRequestBuilder()
///     .From("PAYOMAK")
///     .To("+998901234567")
///     .Text("Ваш код: 1234")
///     .Flash()
///     .WithDeliveryReport()
///     .Build();
/// </code>
/// </example>
public sealed class SubmitRequestBuilder
{
    private string   _source      = "";
    private string   _destination = "";
    private string?  _text;
    private byte[]?  _binary;
    private bool     _flash;
    private bool     _deliveryReport = true;
    private bool     _forceUcs2;
    private TimeSpan? _validity;
    private string?  _correlationId;
    private byte     _esmClass = 0x00;

    // ── Source / Destination ─────────────────────────────────────

    public SubmitRequestBuilder From(string address)
    {
        _source = address;
        return this;
    }

    public SubmitRequestBuilder To(string destination)
    {
        _destination = destination;
        return this;
    }

    // ── Content ──────────────────────────────────────────────────

    /// <summary>Set a text message. Auto-detects GSM7 vs UCS2.</summary>
    public SubmitRequestBuilder Text(string message)
    {
        _text   = message;
        _binary = null;
        return this;
    }

    /// <summary>Set a raw binary payload (data_coding=0x04).</summary>
    public SubmitRequestBuilder Binary(byte[] payload)
    {
        _binary = payload;
        _text   = null;
        return this;
    }

    // ── Options ───────────────────────────────────────────────────

    /// <summary>Send as Flash SMS (Class 0) — displayed immediately, not stored.</summary>
    public SubmitRequestBuilder Flash()
    {
        _flash = true;
        return this;
    }

    /// <summary>Request a delivery report. Default: enabled.</summary>
    public SubmitRequestBuilder WithDeliveryReport(bool enable = true)
    {
        _deliveryReport = enable;
        return this;
    }

    /// <summary>Force UCS2 encoding even for ASCII-only text.</summary>
    public SubmitRequestBuilder ForceUcs2()
    {
        _forceUcs2 = true;
        return this;
    }

    /// <summary>Set message validity period.</summary>
    public SubmitRequestBuilder ValidFor(TimeSpan validity)
    {
        _validity = validity;
        return this;
    }

    /// <summary>Attach a correlation ID for tracking (not sent to SMSC).</summary>
    public SubmitRequestBuilder WithCorrelationId(string id)
    {
        _correlationId = id;
        return this;
    }

    /// <summary>Advanced: override the esm_class byte.</summary>
    public SubmitRequestBuilder WithEsmClass(byte esmClass)
    {
        _esmClass = esmClass;
        return this;
    }

    // ── Build ────────────────────────────────────────────────────

    public SubmitRequest Build()
    {
        if (string.IsNullOrWhiteSpace(_source))
            throw new InvalidOperationException("Source address is required. Call .From(...)");

        if (string.IsNullOrWhiteSpace(_destination))
            throw new InvalidOperationException("Destination address is required. Call .To(...)");

        if (_text == null && _binary == null)
            throw new InvalidOperationException("Message content is required. Call .Text(...) or .Binary(...)");

        return new SubmitRequest
        {
            SourceAddress      = _source,
            DestinationAddress = _destination,
            Message            = _text ?? "",
            Payload            = _binary,
            IsFlash            = _flash,
            RegisteredDelivery = _deliveryReport,
            ForceUcs2          = _forceUcs2,
            ValidityPeriod     = _validity,
            CorrelationId      = _correlationId,
            EsmClass           = _esmClass,
        };
    }
}
