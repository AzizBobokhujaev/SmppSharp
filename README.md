# SmppSharp

Lightweight SMPP 3.4 client for .NET 8+. No external dependencies.

## Features

- **Text SMS** — auto-detect GSM7 (Latin) or UCS2 (Cyrillic, Arabic, Chinese, Hebrew, Greek, emoji)
- **Long messages** — automatic multipart splitting via UDH (default), SAR TLV, or `message_payload` TLV
- **Flash SMS** — Class 0 messages displayed immediately without storing
- **Binary SMS** — raw byte payload (OTA configs, ringtones, etc.)
- **WAP Push** — Service Indication (SI) with URL and message text
- **Fluent builder** — intuitive `SubmitRequestBuilder` API
- **MO & delivery receipts** — receive incoming messages and status reports
- **Bulk sending** — `SubmitBulkAsync` with concurrency control
- **Pipeline mode** — `SubmitPipelineAsync` for high-throughput campaigns (> 1000 msg/sec)
- **SSL/TLS** — optional encrypted transport
- **Auto-reconnect** — configurable delay and retry limit
- **Keepalive** — automatic `enquire_link` heartbeat
- **Diagnostic Metrics** — `System.Diagnostics.Metrics` integration (OpenTelemetry, Prometheus)
- **ASP.NET Core DI** — `AddSmpp()` extension with `IHostedService`

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

client.OnMessageReceived  += async msg     => Console.WriteLine($"MO: {msg.SourceAddress} → {msg.Message}");
client.OnDeliveryReceived += async receipt => Console.WriteLine($"DR: {receipt.MessageId} = {receipt.Status}");
client.OnDisconnected     += ex            => Console.WriteLine($"Disconnected: {ex?.Message}");
client.OnReconnected      +=               () => Console.WriteLine("Reconnected!");

await client.ConnectAsync();

var result = await client.SubmitAsync(new SubmitRequest
{
    SourceAddress      = "MYAPP",
    DestinationAddress = "+998901234567",
    Message            = "Ваш код: 1234",
});

Console.WriteLine($"Sent! MessageId={result.MessageId}, Segments={result.SegmentCount}");
```

## Fluent builder

```csharp
var request = new SubmitRequestBuilder()
    .From("PAYOMAK")
    .To("+998901234567")
    .Text("Ваш код: 4521")
    .WithDeliveryReport()
    .WithCorrelationId("order-99")
    .ValidFor(TimeSpan.FromHours(24))
    .Build();

await client.SubmitAsync(request);
```

## Flash SMS

Displayed immediately on the handset — not saved to inbox.

```csharp
var request = new SubmitRequestBuilder()
    .From("ALERT")
    .To("+998901234567")
    .Text("Внимание! Ваш счёт заблокирован.")
    .Flash()
    .Build();

await client.SubmitAsync(request);
```

## Binary SMS

```csharp
var request = new SubmitRequestBuilder()
    .From("SVC")
    .To("+998901234567")
    .Binary(new byte[] { 0x01, 0x02, 0x03, 0xAB })
    .Build();

await client.SubmitAsync(request);
```

## WAP Push

Sends a Service Indication (SI) — opens a URL on the user's handset.

```csharp
using SmppSharp.Builders;

var request = WapPushBuilder.CreateSi(
    sourceAddress:      "PAYOMAK",
    destinationAddress: "+998901234567",
    url:                "https://payomak.uz/promo",
    message:            "Нажмите для просмотра акции",
    signal:             WapPushSignal.Medium);

await client.SubmitAsync(request);
```

Available signals: `None`, `Low`, `Medium`, `High`, `Delete`.

## Bulk sending

```csharp
var requests = phones.Select(phone => new SubmitRequestBuilder()
    .From("PAYOMAK")
    .To(phone)
    .Text("Акция! Скидка 20% до конца месяца.")
    .Build());

var results = await client.SubmitBulkAsync(requests, maxConcurrency: 20);
Console.WriteLine($"Sent: {results.Count}");
```

## High-throughput pipeline (> 1000 msg/sec)

Streams requests and yields results as they complete — no need to buffer everything in memory.

```csharp
var requests = GetRequestsAsync(); // IAsyncEnumerable<SubmitRequest>

await foreach (var result in client.SubmitPipelineAsync(requests, concurrency: 100))
{
    Console.WriteLine($"Sent: {result.MessageId} ({result.SegmentCount} segments)");
}
```

## SSL/TLS

```csharp
var client = new SmppClient(new SmppOptions
{
    Host     = "smpp.provider.com",
    Port     = 3550,
    SystemId = "login",
    Password = "password",
    UseSsl   = true,
    // SslTargetHost            = "smpp.provider.com", // optional, defaults to Host
    // AllowUntrustedCertificate = true,               // dev only
});
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
public class SmsService(ISmppClient smpp)
{
    public Task SendAsync(string phone, string text) =>
        smpp.SubmitAsync(new SubmitRequest
        {
            SourceAddress      = "MYAPP",
            DestinationAddress = phone,
            Message            = text,
        });
}
```

## Observability (OpenTelemetry / Prometheus)

```csharp
// Register metrics listener
builder.Services.AddOpenTelemetry()
    .WithMetrics(m => m.AddMeter(SmppMetrics.MeterName));
```

Available instruments:

| Metric | Type | Description |
|---|---|---|
| `smpp.messages.sent` | Counter | SMS segments successfully submitted |
| `smpp.messages.failed` | Counter | Failed submit operations |
| `smpp.messages.received` | Counter | MO messages received |
| `smpp.receipts.received` | Counter | Delivery receipts received |
| `smpp.reconnects.total` | Counter | Reconnect attempts |
| `smpp.submit.duration` | Histogram (ms) | Round-trip time for submit_sm |
| `smpp.connections.active` | UpDownCounter | Currently bound connections |

## Configuration reference

```csharp
new SmppOptions
{
    Host                     = "localhost",
    Port                     = 2775,
    SystemId                 = "",
    Password                 = "",
    SystemType               = "",
    BindMode                 = BindMode.Transceiver, // Transmitter | Receiver | Transceiver
    EnquireLinkInterval      = TimeSpan.FromSeconds(30),
    ResponseTimeout          = TimeSpan.FromSeconds(30),
    ReconnectDelay           = TimeSpan.FromSeconds(5),
    AutoReconnect            = true,
    MaxReconnectAttempts     = 0,   // 0 = infinite
    UseSsl                    = false,
    SslTargetHost             = null,
    AllowUntrustedCertificate = false,
    UseUdh                    = true,   // multipart via UDH; false = SAR TLV
    UseMessagePayload         = false,  // send long message as single PDU via message_payload TLV
}
```

## License

MIT
