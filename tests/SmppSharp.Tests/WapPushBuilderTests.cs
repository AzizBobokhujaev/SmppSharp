using SmppSharp.Builders;
using SmppSharp.Protocol;

namespace SmppSharp.Tests;

public class WapPushBuilderTests
{
    [Fact]
    public void CreateSi_SetsCorrectEsmClass()
    {
        var request = WapPushBuilder.CreateSi("APP", "+998901234567", "https://example.com");
        Assert.Equal(0x40, request.EsmClass);
    }

    [Fact]
    public void CreateSi_HasBinaryPayload()
    {
        var request = WapPushBuilder.CreateSi("APP", "+998901234567", "https://example.com");
        Assert.NotNull(request.Payload);
        Assert.True(request.Payload!.Length > 0);
    }

    [Fact]
    public void CreateSi_PayloadStartsWithUdh()
    {
        var request = WapPushBuilder.CreateSi("APP", "+998901234567", "https://example.com");
        var payload = request.Payload!;

        // UDH: len=6, IE_ID=0x04 (app port 16-bit), IE_len=0x04, dest=2948, src=9200
        Assert.Equal(0x06, payload[0]); // UDH length = 6
        Assert.Equal(0x04, payload[1]); // IE ID
        Assert.Equal(0x04, payload[2]); // IE length

        // Dest port = 2948 (0x0B84)
        Assert.Equal(0x0B, payload[3]);
        Assert.Equal(0x84, payload[4]);
    }

    [Fact]
    public void CreateSi_PayloadContainsUrl()
    {
        const string url = "https://payomak.uz/promo";
        var request  = WapPushBuilder.CreateSi("APP", "+998901234567", url);
        var payload  = request.Payload!;

        // URL bytes should be present in payload
        var urlBytes = System.Text.Encoding.UTF8.GetBytes(url);
        var found    = ContainsSequence(payload, urlBytes);
        Assert.True(found, "URL bytes not found in WAP Push payload");
    }

    [Fact]
    public void CreateSi_DeliveryReportDisabledByDefault()
    {
        var request = WapPushBuilder.CreateSi("APP", "+998901234567", "https://example.com");
        Assert.False(request.RegisteredDelivery);
    }

    private static bool ContainsSequence(byte[] haystack, byte[] needle)
    {
        for (var i = 0; i <= haystack.Length - needle.Length; i++)
        {
            if (haystack.AsSpan(i, needle.Length).SequenceEqual(needle))
                return true;
        }
        return false;
    }
}
