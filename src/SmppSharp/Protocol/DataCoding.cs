namespace SmppSharp.Protocol;

/// <summary>SMPP data_coding values (GSM 03.38).</summary>
public static class DataCoding
{
    /// <summary>GSM 7-bit default alphabet (160 chars / SMS).</summary>
    public const byte Gsm7 = 0x00;

    /// <summary>Latin-1 / ISO-8859-1.</summary>
    public const byte Latin1 = 0x03;

    /// <summary>8-bit binary data.</summary>
    public const byte Binary = 0x04;

    /// <summary>UCS2 / UTF-16 Big Endian (70 chars / SMS). Required for Cyrillic, Arabic, CJK, etc.</summary>
    public const byte Ucs2 = 0x08;

    /// <summary>GSM7 Flash SMS — Class 0 message, displayed immediately without storing.</summary>
    public const byte Gsm7Flash = 0x10;

    /// <summary>UCS2 Flash SMS — Class 0 message in Unicode.</summary>
    public const byte Ucs2Flash = 0x18;
}
