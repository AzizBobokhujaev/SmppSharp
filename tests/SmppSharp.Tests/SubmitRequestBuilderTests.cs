using SmppSharp.Builders;
using SmppSharp.Models;

namespace SmppSharp.Tests;

public class SubmitRequestBuilderTests
{
    [Fact]
    public void Build_TextMessage_SetsCorrectProperties()
    {
        var request = new SubmitRequestBuilder()
            .From("PAYOMAK")
            .To("+998901234567")
            .Text("Hello!")
            .Build();

        Assert.Equal("PAYOMAK",        request.SourceAddress);
        Assert.Equal("+998901234567",  request.DestinationAddress);
        Assert.Equal("Hello!",         request.Message);
        Assert.Null(request.Payload);
        Assert.False(request.IsFlash);
        Assert.True(request.RegisteredDelivery);
    }

    [Fact]
    public void Build_FlashMessage_SetsIsFlash()
    {
        var request = new SubmitRequestBuilder()
            .From("ALERT")
            .To("+998901234567")
            .Text("Emergency!")
            .Flash()
            .Build();

        Assert.True(request.IsFlash);
    }

    [Fact]
    public void Build_BinaryMessage_SetsPayload()
    {
        var data    = new byte[] { 0x01, 0x02, 0x03 };
        var request = new SubmitRequestBuilder()
            .From("SVC")
            .To("+998901234567")
            .Binary(data)
            .Build();

        Assert.Equal(data, request.Payload);
        Assert.Equal("", request.Message);
    }

    [Fact]
    public void Build_WithCorrelationId_Attached()
    {
        var request = new SubmitRequestBuilder()
            .From("APP")
            .To("+998901234567")
            .Text("Test")
            .WithCorrelationId("order-42")
            .Build();

        Assert.Equal("order-42", request.CorrelationId);
    }

    [Fact]
    public void Build_WithValidityPeriod_Set()
    {
        var validity = TimeSpan.FromHours(24);
        var request  = new SubmitRequestBuilder()
            .From("APP")
            .To("+998901234567")
            .Text("Test")
            .ValidFor(validity)
            .Build();

        Assert.Equal(validity, request.ValidityPeriod);
    }

    [Fact]
    public void Build_MissingFrom_Throws()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            new SubmitRequestBuilder().To("+998901234567").Text("Hi").Build());

        Assert.Contains("Source address", ex.Message);
    }

    [Fact]
    public void Build_MissingContent_Throws()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            new SubmitRequestBuilder().From("APP").To("+998901234567").Build());

        Assert.Contains("content", ex.Message);
    }
}
