using System.Text;

namespace SmppSharp.Codec;

/// <summary>
/// Determines encoding and splits messages into SMS segments.
/// </summary>
internal static class MessageSplitter
{
    // Single SMS limits
    private const int Gsm7SingleLimit  = 160;
    private const int Ucs2SingleLimit  = 140; // bytes (70 chars × 2)

    // Multipart SMS limits (6 bytes used for UDH)
    private const int Gsm7SegmentLimit = 153;
    private const int Ucs2SegmentLimit = 134; // bytes (67 chars × 2), must be even

    public static EncodedMessage Encode(string message, bool forceUcs2 = false, bool isFlash = false)
    {
        if (!forceUcs2 && Gsm7Encoder.CanEncode(message))
        {
            var bytes      = Gsm7Encoder.Encode(message);
            var dataCoding = isFlash ? Protocol.DataCoding.Gsm7Flash : Protocol.DataCoding.Gsm7;
            var segments   = Split(bytes, Gsm7SingleLimit, Gsm7SegmentLimit);
            return new EncodedMessage(dataCoding, bytes, segments);
        }
        else
        {
            var bytes      = Encoding.BigEndianUnicode.GetBytes(message);
            var dataCoding = isFlash ? Protocol.DataCoding.Ucs2Flash : Protocol.DataCoding.Ucs2;
            var segments   = Split(bytes, Ucs2SingleLimit, Ucs2SegmentLimit);
            return new EncodedMessage(dataCoding, bytes, segments);
        }
    }

    /// <summary>Encodes a raw binary payload (data_coding = 0x04). Max 140 bytes/SMS.</summary>
    public static EncodedMessage EncodeBinary(byte[] payload)
    {
        // Binary SMS: single = 140 bytes, multipart segment = 134 bytes
        var segments = Split(payload, 140, 134);
        return new EncodedMessage(Protocol.DataCoding.Binary, payload, segments);
    }

    private static List<byte[]> Split(byte[] data, int singleLimit, int segmentLimit)
    {
        if (data.Length <= singleLimit)
            return [data];

        var segments = new List<byte[]>();
        for (var i = 0; i < data.Length; i += segmentLimit)
        {
            var len = Math.Min(segmentLimit, data.Length - i);
            var seg = data[i..(i + len)];
            segments.Add(seg);
        }
        return segments;
    }
}

internal sealed record EncodedMessage(
    byte DataCoding,
    byte[] AllBytes,
    List<byte[]> Segments)
{
    public bool IsMultipart => Segments.Count > 1;
}
