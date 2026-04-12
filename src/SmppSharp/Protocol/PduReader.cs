using System.Text;

namespace SmppSharp.Protocol;

/// <summary>Sequential binary reader for SMPP PDU bodies.</summary>
internal sealed class PduReader
{
    private readonly byte[] _data;
    private int _pos;

    public PduReader(byte[] data, int offset = 0)
    {
        _data = data;
        _pos  = offset;
    }

    public int Position => _pos;
    public int Remaining => _data.Length - _pos;
    public bool HasData => _pos < _data.Length;

    public byte ReadByte() => _data[_pos++];

    public ushort ReadUInt16()
    {
        var v = (ushort)((_data[_pos] << 8) | _data[_pos + 1]);
        _pos += 2;
        return v;
    }

    public uint ReadUInt32()
    {
        var v = ((uint)_data[_pos] << 24) | ((uint)_data[_pos + 1] << 16)
              | ((uint)_data[_pos + 2] << 8) | _data[_pos + 3];
        _pos += 4;
        return v;
    }

    /// <summary>Reads a null-terminated ASCII C-string.</summary>
    public string ReadCString()
    {
        var start = _pos;
        while (_pos < _data.Length && _data[_pos] != 0x00) _pos++;
        var s = Encoding.ASCII.GetString(_data, start, _pos - start);
        if (_pos < _data.Length) _pos++; // skip null terminator
        return s;
    }

    public byte[] ReadBytes(int count)
    {
        var result = _data[_pos..(_pos + count)];
        _pos += count;
        return result;
    }

    public byte[] ReadToEnd() => _data[_pos..];

    public void Skip(int count) => _pos += count;
}
