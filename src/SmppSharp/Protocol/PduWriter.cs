using System.Text;

namespace SmppSharp.Protocol;

/// <summary>Helpers for building SMPP PDU byte arrays.</summary>
internal static class PduWriter
{
    /// <summary>Builds a complete PDU byte array including the 16-byte header.</summary>
    public static byte[] Build(uint commandId, uint status, uint sequence, byte[] body)
    {
        var length = (uint)(16 + body.Length);
        var pdu    = new byte[length];

        WriteUInt32(pdu, 0,  length);
        WriteUInt32(pdu, 4,  commandId);
        WriteUInt32(pdu, 8,  status);
        WriteUInt32(pdu, 12, sequence);

        body.CopyTo(pdu, 16);
        return pdu;
    }

    public static void WriteByte(List<byte> buf, byte value)
        => buf.Add(value);

    public static void WriteUInt16(List<byte> buf, ushort value)
    {
        buf.Add((byte)(value >> 8));
        buf.Add((byte)value);
    }

    public static void WriteUInt32(List<byte> buf, uint value)
    {
        buf.Add((byte)(value >> 24));
        buf.Add((byte)(value >> 16));
        buf.Add((byte)(value >> 8));
        buf.Add((byte)value);
    }

    /// <summary>Writes a null-terminated ASCII C-string.</summary>
    public static void WriteCString(List<byte> buf, string value)
    {
        buf.AddRange(Encoding.ASCII.GetBytes(value));
        buf.Add(0x00);
    }

    public static void WriteOctets(List<byte> buf, byte[] data)
        => buf.AddRange(data);

    /// <summary>Writes a TLV (tag, length, value) optional parameter.</summary>
    public static void WriteTlv(List<byte> buf, ushort tag, byte[] value)
    {
        WriteUInt16(buf, tag);
        WriteUInt16(buf, (ushort)value.Length);
        WriteOctets(buf, value);
    }

    public static void WriteTlvByte(List<byte> buf, ushort tag, byte value)
        => WriteTlv(buf, tag, [value]);

    public static void WriteTlvUInt16(List<byte> buf, ushort tag, ushort value)
        => WriteTlv(buf, tag, [(byte)(value >> 8), (byte)value]);

    // ── Helpers ─────────────────────────────────────────────────

    private static void WriteUInt32(byte[] buf, int offset, uint value)
    {
        buf[offset]     = (byte)(value >> 24);
        buf[offset + 1] = (byte)(value >> 16);
        buf[offset + 2] = (byte)(value >> 8);
        buf[offset + 3] = (byte)value;
    }
}
