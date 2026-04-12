namespace SmppSharp.Protocol;

/// <summary>SMPP data_coding values.</summary>
public static class DataCoding
{
    /// <summary>GSM 7-bit default alphabet (160 chars / SMS).</summary>
    public const byte Gsm7 = 0x00;

    /// <summary>Latin-1 / ISO-8859-1.</summary>
    public const byte Latin1 = 0x03;

    /// <summary>UCS2 / UTF-16 Big Endian (70 chars / SMS). Required for Cyrillic, Arabic, CJK, etc.</summary>
    public const byte Ucs2 = 0x08;
}
