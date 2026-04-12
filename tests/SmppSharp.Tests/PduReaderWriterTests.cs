using SmppSharp.Protocol;

namespace SmppSharp.Tests;

public class PduReaderWriterTests
{
    [Fact]
    public void Build_CorrectHeaderLayout()
    {
        var body = new byte[] { 0x01, 0x02, 0x03 };
        var pdu  = PduWriter.Build(CommandId.SubmitSm, 0, 1, body);

        var r = new PduReader(pdu);
        Assert.Equal(19u,                r.ReadUInt32()); // length = 16 + 3
        Assert.Equal(CommandId.SubmitSm, r.ReadUInt32()); // command_id
        Assert.Equal(0u,                 r.ReadUInt32()); // status
        Assert.Equal(1u,                 r.ReadUInt32()); // sequence
        Assert.Equal(body,               r.ReadBytes(3)); // body
    }

    [Fact]
    public void ReadCString_NullTerminated_ReturnsCorrectString()
    {
        var data = new byte[] { (byte)'H', (byte)'i', 0x00, (byte)'X' };
        var r    = new PduReader(data);

        Assert.Equal("Hi", r.ReadCString());
        Assert.Equal(3, r.Position); // past null terminator
    }

    [Fact]
    public void WriteCString_AppendsCString()
    {
        var buf = new List<byte>();
        PduWriter.WriteCString(buf, "SMPP");

        Assert.Equal(5, buf.Count);
        Assert.Equal(0x00, buf[4]);
    }

    [Fact]
    public void WriteTlv_CorrectTagLengthValue()
    {
        var buf = new List<byte>();
        PduWriter.WriteTlvByte(buf, TlvTag.SarTotalSegments, 3);

        // tag (2) + length (2) + value (1) = 5 bytes
        Assert.Equal(5, buf.Count);
        var r = new PduReader([.. buf]);
        Assert.Equal(TlvTag.SarTotalSegments, r.ReadUInt16());
        Assert.Equal(1,   r.ReadUInt16()); // length
        Assert.Equal(3,   r.ReadByte());   // value
    }
}
