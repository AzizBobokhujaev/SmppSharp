using SmppSharp.Codec;

namespace SmppSharp.Tests;

public class Gsm7EncoderTests
{
    [Theory]
    [InlineData("Hello World")]
    [InlineData("Test 123!")]
    [InlineData("@£$¥")]
    [InlineData("äöñüà")]
    public void CanEncode_LatinAndGsmChars_ReturnsTrue(string text)
    {
        Assert.True(Gsm7Encoder.CanEncode(text));
    }

    [Theory]
    [InlineData("Привет")]          // Cyrillic
    [InlineData("مرحبا")]           // Arabic
    [InlineData("你好")]             // Chinese
    [InlineData("Test 🙂")]         // emoji
    public void CanEncode_NonGsmChars_ReturnsFalse(string text)
    {
        Assert.False(Gsm7Encoder.CanEncode(text));
    }

    [Fact]
    public void EncodeDecode_RoundTrip_ReturnsOriginal()
    {
        var original = "Hello World! Test 123 @#$";
        var encoded  = Gsm7Encoder.Encode(original);
        var decoded  = Gsm7Encoder.Decode(encoded);
        Assert.Equal(original, decoded);
    }

    [Fact]
    public void Encode_ExtensionChars_UsesEscapeSequence()
    {
        var encoded = Gsm7Encoder.Encode("[{€}]");
        // Each extension char = 2 bytes (ESC + code), 5 chars = 10 bytes
        Assert.Equal(10, encoded.Length);
    }

    [Fact]
    public void Decode_ExtensionChars_ReturnsCorrectChars()
    {
        var original = "Price: 100€";
        var encoded  = Gsm7Encoder.Encode(original);
        var decoded  = Gsm7Encoder.Decode(encoded);
        Assert.Equal(original, decoded);
    }
}
