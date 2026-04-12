# SmppSharp

Lightweight SMPP 3.4 client for .NET 8+.

## Features

- Bind transceiver / transmitter / receiver
- Send SMS — auto-split long messages into multipart segments
- Auto-detect encoding: GSM7 for Latin, UCS2 for Cyrillic/Arabic/CJK
- Receive MO messages and delivery receipts
- Automatic enquire_link keepalive
- Auto-reconnect with configurable delay and retry limit
- Bulk send with concurrency control
- ASP.NET Core DI integration (`AddSmpp`)
- Zero dependencies except `Microsoft.Extensions.Logging.Abstractions`

## Installation

```bash
dotnet add package SmppSharp
```

## Quick start

```csharp
var client = new SmppClient(new SmppOptions
{
    Host     = "smpp.provider.com",
    Port     = 2775,
    SystemId = "login",
    Password = "password",
});

client.OnDeliveryReceived += async receipt =>
    Console.WriteLine($"Delivered: {receipt.MessageId} — {receipt.Status}");

client.OnMessageReceived += async msg =>
    Console.WriteLine($"MO from {msg.SourceAddress}: {msg.Message}");

await client.ConnectAsync();

var result = await client.SubmitAsync(new SubmitRequest
{
    SourceAddress      = "MYAPP",
    DestinationAddress = "+998901234567",
    Message            = "Ваш код: 1234",
});

Console.WriteLine($"Sent! MessageId={result.MessageId}, Segments={result.SegmentCount}");
```

## ASP.NET Core DI

```csharp
// Program.cs
builder.Services.AddSmpp(opt =>
{
    opt.Host     = "smpp.provider.com";
    opt.Port     = 2775;
    opt.SystemId = "login";
    opt.Password = "password";
});

// Inject ISmppClient wherever needed
```

## License

MIT
