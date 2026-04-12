using System.Text;

namespace SmppSharp.Codec;

/// <summary>
/// GSM 7-bit default alphabet encoder/decoder.
/// Handles the full GSM 03.38 charset including the extension table (@, £, $, etc.)
/// </summary>
public static class Gsm7Encoder
{
    // GSM 7-bit basic character set (position = GSM code point)
    private static readonly char[] BasicCharset =
    [
        '@',  '£',  '$',  '¥',  'è',  'é',  'ù',  'ì',  'ò',  'Ç',  '\n', 'Ø',  'ø',  '\r', 'Å',  'å',
        'Δ',  '_',  'Φ',  'Γ',  'Λ',  'Ω',  'Π',  'Ψ',  'Σ',  'Θ',  'Ξ',  '\x1B','Æ', 'æ',  'ß',  'É',
        ' ',  '!',  '"',  '#',  '¤',  '%',  '&',  '\'', '(',  ')',  '*',  '+',  ',',  '-',  '.',  '/',
        '0',  '1',  '2',  '3',  '4',  '5',  '6',  '7',  '8',  '9',  ':',  ';',  '<',  '=',  '>',  '?',
        '¡',  'A',  'B',  'C',  'D',  'E',  'F',  'G',  'H',  'I',  'J',  'K',  'L',  'M',  'N',  'O',
        'P',  'Q',  'R',  'S',  'T',  'U',  'V',  'W',  'X',  'Y',  'Z',  'Ä',  'Ö',  'Ñ',  'Ü',  '§',
        '¿',  'a',  'b',  'c',  'd',  'e',  'f',  'g',  'h',  'i',  'j',  'k',  'l',  'm',  'n',  'o',
        'p',  'q',  'r',  's',  't',  'u',  'v',  'w',  'x',  'y',  'z',  'ä',  'ö',  'ñ',  'ü',  'à',
    ];

    // Extension table (ESC + code point)
    private static readonly Dictionary<char, byte> ExtensionTable = new()
    {
        { '\f', 0x0A }, // form feed
        { '^',  0x14 },
        { '{',  0x28 },
        { '}',  0x29 },
        { '\\', 0x2F },
        { '[',  0x3C },
        { '~',  0x3D },
        { ']',  0x3E },
        { '|',  0x40 },
        { '€',  0x65 },
    };

    private static readonly Dictionary<char, byte> BasicLookup;

    static Gsm7Encoder()
    {
        BasicLookup = new Dictionary<char, byte>(128);
        for (byte i = 0; i < BasicCharset.Length; i++)
            BasicLookup[BasicCharset[i]] = i;
    }

    /// <summary>Returns true if all characters in the string fit in GSM7.</summary>
    public static bool CanEncode(string text)
    {
        foreach (var c in text)
        {
            if (!BasicLookup.ContainsKey(c) && !ExtensionTable.ContainsKey(c))
                return false;
        }
        return true;
    }

    /// <summary>
    /// Encodes string to GSM7 byte array (unpacked — one byte per character, 2 for extension chars).
    /// This is what SMPP submit_sm uses (not the packed 7-bit radio format).
    /// </summary>
    public static byte[] Encode(string text)
    {
        var result = new List<byte>(text.Length);
        foreach (var c in text)
        {
            if (BasicLookup.TryGetValue(c, out var code))
            {
                result.Add(code);
            }
            else if (ExtensionTable.TryGetValue(c, out var ext))
            {
                result.Add(0x1B); // ESC
                result.Add(ext);
            }
            else
            {
                result.Add(0x3F); // '?' — fallback for unknown chars
            }
        }
        return [.. result];
    }

    /// <summary>Decodes unpacked GSM7 bytes back to a string.</summary>
    public static string Decode(byte[] data)
    {
        var sb = new StringBuilder(data.Length);
        var i = 0;
        while (i < data.Length)
        {
            var b = data[i++];
            if (b == 0x1B && i < data.Length)
            {
                var ext = data[i++];
                var found = ExtensionTable.FirstOrDefault(kv => kv.Value == ext);
                sb.Append(found.Key != '\0' ? found.Key : ' ');
            }
            else if (b < BasicCharset.Length)
            {
                sb.Append(BasicCharset[b]);
            }
        }
        return sb.ToString();
    }
}
