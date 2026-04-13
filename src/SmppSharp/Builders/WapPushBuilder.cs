using SmppSharp.Models;
using System.Text;

namespace SmppSharp.Builders;

/// <summary>
/// Builds WAP Push Service Indication (SI) messages.
/// Generates the correct binary payload (UDH + WSP + WBXML) for sending via SMPP.
/// </summary>
/// <example>
/// <code>
/// var request = WapPushBuilder.CreateSi(
///     sourceAddress:      "PAYOMAK",
///     destinationAddress: "+998901234567",
///     url:                "https://example.com/promo",
///     message:            "Нажмите для просмотра акции",
///     signal:             WapPushSignal.Medium);
///
/// await client.SubmitAsync(request);
/// </code>
/// </example>
public static class WapPushBuilder
{
    /// <summary>
    /// Creates a WAP Push Service Indication request ready to be submitted via SMPP.
    /// </summary>
    public static SubmitRequest CreateSi(
        string sourceAddress,
        string destinationAddress,
        string url,
        string? message = null,
        WapPushSignal signal = WapPushSignal.Medium)
    {
        var payload = BuildSiPayload(url, message ?? "", signal);

        return new SubmitRequest
        {
            SourceAddress      = sourceAddress,
            DestinationAddress = destinationAddress,
            Message            = "",
            Payload            = payload,
            EsmClass           = 0x40,  // UDH indicator
            RegisteredDelivery = false,
        };
    }

    // ── Binary construction ───────────────────────────────────────

    private static byte[] BuildSiPayload(string url, string text, WapPushSignal signal)
    {
        var wbxml = BuildSiWbxml(url, text, signal);
        var wsp   = BuildWspPush(wbxml);

        // UDH: Application Port Addressing (16-bit)
        // [0] UDH Length = 6 (bytes that follow)
        // [1] IE ID      = 0x04 (app port 16-bit)
        // [2] IE Length  = 0x04
        // [3-4] Dest port: 2948 (0x0B84) — WAP Push port
        // [5-6] Src  port: 9200 (0x23F0)
        var udh = new byte[] { 0x06, 0x04, 0x04, 0x0B, 0x84, 0x23, 0xF0 };

        var result = new byte[udh.Length + wsp.Length];
        udh.CopyTo(result, 0);
        wsp.CopyTo(result, udh.Length);
        return result;
    }

    private static byte[] BuildWspPush(byte[] wbxml)
    {
        // WSP Push PDU:
        //   TID      (1 byte) = 0x00
        //   PDU Type (1 byte) = 0x06 (Push)
        //   Headers Length (varint)
        //   Content-Type: 0xAE = application/vnd.wap.sic (well-known compact form)
        //   Body: WBXML
        var buf = new List<byte>();
        buf.Add(0x00);  // TID
        buf.Add(0x06);  // Push
        buf.Add(0x01);  // Headers length = 1
        buf.Add(0xAE);  // Content-Type: application/vnd.wap.sic
        buf.AddRange(wbxml);
        return [.. buf];
    }

    private static byte[] BuildSiWbxml(string url, string text, WapPushSignal signal)
    {
        var urlBytes  = Encoding.UTF8.GetBytes(url);
        var textBytes = Encoding.UTF8.GetBytes(text);

        var buf = new List<byte>();

        // WBXML header
        buf.Add(0x02);  // WBXML version 1.2
        buf.Add(0x05);  // Public ID: WAP SI 1.0
        buf.Add(0x6A);  // Charset: UTF-8
        buf.Add(0x00);  // String table length: 0

        // <si> — tag 0x05, has content → 0x05 | 0x40 = 0x45
        buf.Add(0x45);

        // <indication> — tag 0x06, has attrs + content → 0x06 | 0xC0 = 0xC6
        buf.Add(0xC6);

        // Attribute: href (inline string)
        buf.Add(0x06);          // href attribute token
        buf.Add(0x03);          // STR_I (inline string)
        buf.AddRange(urlBytes);
        buf.Add(0x00);          // null terminator

        // Attribute: action="signal-x" (complete token includes value)
        buf.Add(signal switch
        {
            WapPushSignal.None   => 0x08,   // action="signal-none"
            WapPushSignal.Low    => 0x09,   // action="signal-low"
            WapPushSignal.Medium => 0x0A,   // action="signal-medium"
            WapPushSignal.High   => 0x0B,   // action="signal-high"
            WapPushSignal.Delete => 0x0C,   // action="delete"
            _                    => 0x0A,
        });

        buf.Add(0x01);  // END attributes

        // Text content (inline string)
        if (textBytes.Length > 0)
        {
            buf.Add(0x03);          // STR_I
            buf.AddRange(textBytes);
            buf.Add(0x00);
        }

        buf.Add(0x01);  // END </indication>
        buf.Add(0x01);  // END </si>

        return [.. buf];
    }
}

/// <summary>Signal priority for WAP Push Service Indication.</summary>
public enum WapPushSignal
{
    /// <summary>No signal — silently delivered.</summary>
    None,

    /// <summary>Low priority signal.</summary>
    Low,

    /// <summary>Medium priority signal (recommended default).</summary>
    Medium,

    /// <summary>High priority — phone may vibrate/ring.</summary>
    High,

    /// <summary>Delete a previously sent SI with the same href.</summary>
    Delete,
}
