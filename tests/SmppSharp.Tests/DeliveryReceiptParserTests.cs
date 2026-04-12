using SmppSharp.Internal;
using SmppSharp.Models;

namespace SmppSharp.Tests;

public class DeliveryReceiptParserTests
{
    [Fact]
    public void Parse_StandardFormat_ReturnsReceipt()
    {
        var text = "id:ABC123 sub:001 dlvrd:001 submit date:2404121030 done date:2404121031 stat:DELIVRD err:000 text:Hello";

        var receipt = DeliveryReceiptParser.TryParse(text);

        Assert.NotNull(receipt);
        Assert.Equal("ABC123",          receipt.MessageId);
        Assert.Equal(DeliveryStatus.Delivered, receipt.Status);
        Assert.Equal("000",             receipt.ErrorCode);
        Assert.Equal(1,                 receipt.Submitted);
        Assert.Equal(1,                 receipt.Delivered);
    }

    [Fact]
    public void Parse_FailedStatus_ReturnsFailedStatus()
    {
        var text = "id:XYZ999 sub:001 dlvrd:000 submit date:2404121030 done date:2404121035 stat:UNDELIV err:099 text:";

        var receipt = DeliveryReceiptParser.TryParse(text);

        Assert.NotNull(receipt);
        Assert.Equal(DeliveryStatus.Undeliverable, receipt.Status);
        Assert.Equal("099", receipt.ErrorCode);
    }

    [Fact]
    public void Parse_InvalidText_ReturnsNull()
    {
        var receipt = DeliveryReceiptParser.TryParse("this is a regular MO message");
        Assert.Null(receipt);
    }

    [Theory]
    [InlineData("DELIVRD",  DeliveryStatus.Delivered)]
    [InlineData("UNDELIV",  DeliveryStatus.Undeliverable)]
    [InlineData("REJECTD",  DeliveryStatus.Rejected)]
    [InlineData("EXPIRED",  DeliveryStatus.Expired)]
    [InlineData("FAILED",   DeliveryStatus.Failed)]
    [InlineData("UNKNOWN",  DeliveryStatus.Unknown)]
    public void Parse_AllStatuses(string stat, DeliveryStatus expected)
    {
        var text    = $"id:123 sub:001 dlvrd:001 submit date:2404121030 done date:2404121031 stat:{stat} err:000 text:";
        var receipt = DeliveryReceiptParser.TryParse(text);

        Assert.NotNull(receipt);
        Assert.Equal(expected, receipt.Status);
    }
}
