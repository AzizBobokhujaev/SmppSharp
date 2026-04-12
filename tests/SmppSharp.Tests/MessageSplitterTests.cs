using SmppSharp.Codec;
using SmppSharp.Protocol;

namespace SmppSharp.Tests;

public class MessageSplitterTests
{
    [Fact]
    public void Encode_ShortLatinMessage_SingleSegmentGsm7()
    {
        var result = MessageSplitter.Encode("Hello!");

        Assert.Equal(DataCoding.Gsm7, result.DataCoding);
        Assert.False(result.IsMultipart);
        Assert.Single(result.Segments);
    }

    [Fact]
    public void Encode_ShortCyrillicMessage_SingleSegmentUcs2()
    {
        var result = MessageSplitter.Encode("Привет мир!");

        Assert.Equal(DataCoding.Ucs2, result.DataCoding);
        Assert.False(result.IsMultipart);
        Assert.Single(result.Segments);
    }

    [Fact]
    public void Encode_160CharLatin_SingleSegment()
    {
        var text   = new string('A', 160);
        var result = MessageSplitter.Encode(text);

        Assert.False(result.IsMultipart);
        Assert.Single(result.Segments);
        Assert.Equal(160, result.AllBytes.Length);
    }

    [Fact]
    public void Encode_161CharLatin_TwoSegments()
    {
        var text   = new string('A', 161);
        var result = MessageSplitter.Encode(text);

        Assert.True(result.IsMultipart);
        Assert.Equal(2, result.Segments.Count);
        Assert.Equal(153, result.Segments[0].Length);
        Assert.Equal(8,   result.Segments[1].Length);
    }

    [Fact]
    public void Encode_70CharCyrillic_SingleSegment()
    {
        var text   = new string('А', 70); // Cyrillic А
        var result = MessageSplitter.Encode(text);

        Assert.Equal(DataCoding.Ucs2, result.DataCoding);
        Assert.False(result.IsMultipart);
    }

    [Fact]
    public void Encode_71CharCyrillic_TwoSegments()
    {
        var text   = new string('А', 71);
        var result = MessageSplitter.Encode(text);

        Assert.True(result.IsMultipart);
        Assert.Equal(2, result.Segments.Count);
    }

    [Fact]
    public void Encode_ForceUcs2_UsesUcs2EvenForLatin()
    {
        var result = MessageSplitter.Encode("Hello", forceUcs2: true);

        Assert.Equal(DataCoding.Ucs2, result.DataCoding);
    }
}
