using System.Collections.Concurrent;
using System.Net.Sockets;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using SmppSharp.Codec;
using SmppSharp.Internal;
using SmppSharp.Models;
using SmppSharp.Protocol;

namespace SmppSharp;

/// <summary>
/// SMPP 3.4 client. Thread-safe, auto-reconnecting, supports multipart messages.
/// </summary>
public sealed class SmppClient : ISmppClient
{
    private readonly SmppOptions _options;
    private readonly ILogger<SmppClient> _logger;

    private TcpClient? _tcp;
    private NetworkStream? _stream;
    private uint _sequence;
    private readonly SemaphoreSlim _writeLock = new(1, 1);
    private readonly ConcurrentDictionary<uint, TaskCompletionSource<Pdu>> _pending = new();
    private CancellationTokenSource? _cts;
    private Task? _readLoop;
    private Task? _keepAliveLoop;
    private Task? _reconnectLoop;

    public bool IsConnected { get; private set; }

    public event Func<DeliverMessage, Task>?  OnMessageReceived;
    public event Func<DeliveryReceipt, Task>? OnDeliveryReceived;
    public event Action<Exception?>?          OnDisconnected;
    public event Action?                      OnReconnected;

    public SmppClient(SmppOptions options, ILogger<SmppClient>? logger = null)
    {
        _options = options;
        _logger  = logger ?? NullLogger<SmppClient>.Instance;
    }

    // ── Connection ───────────────────────────────────────────────

    public async Task ConnectAsync(CancellationToken ct = default)
    {
        _cts = CancellationTokenSource.CreateLinkedTokenSource(ct);

        await ConnectInternalAsync(_cts.Token);

        if (_options.AutoReconnect)
            _reconnectLoop = Task.Run(() => ReconnectLoopAsync(_cts.Token), CancellationToken.None);
    }

    private async Task ConnectInternalAsync(CancellationToken ct)
    {
        _tcp = new TcpClient { NoDelay = true };
        await _tcp.ConnectAsync(_options.Host, _options.Port, ct);
        _stream = _tcp.GetStream();

        _readLoop      = Task.Run(() => ReadLoopAsync(ct), CancellationToken.None);
        _keepAliveLoop = Task.Run(() => KeepAliveLoopAsync(ct), CancellationToken.None);

        await BindAsync(ct);

        IsConnected = true;
        _logger.LogInformation("SMPP connected and bound to {Host}:{Port}", _options.Host, _options.Port);
    }

    private async Task BindAsync(CancellationToken ct)
    {
        var (commandId, _) = _options.BindMode switch
        {
            BindMode.Transmitter => (CommandId.BindTransmitter, CommandId.BindTransmitterResp),
            BindMode.Receiver    => (CommandId.BindReceiver,    CommandId.BindReceiverResp),
            _                    => (CommandId.BindTransceiver, CommandId.BindTransceiverResp),
        };

        var body = new List<byte>();
        PduWriter.WriteCString(body, _options.SystemId);
        PduWriter.WriteCString(body, _options.Password);
        PduWriter.WriteCString(body, _options.SystemType);
        PduWriter.WriteByte(body, 0x34);   // interface_version = SMPP 3.4
        PduWriter.WriteByte(body, 0x00);   // addr_ton
        PduWriter.WriteByte(body, 0x00);   // addr_npi
        PduWriter.WriteCString(body, "");  // address_range

        var resp = await SendAndWaitAsync(commandId, [.. body], ct);

        if (!resp.IsOk)
            throw new SmppException($"Bind failed: {CommandStatus.Describe(resp.CommandStatus)}",
                resp.CommandStatus);
    }

    public async Task DisconnectAsync(CancellationToken ct = default)
    {
        if (!IsConnected) return;

        try
        {
            await SendAndWaitAsync(CommandId.Unbind, [], ct);
        }
        catch { /* best effort */ }

        await CleanupAsync();
    }

    private async Task CleanupAsync()
    {
        IsConnected = false;
        _cts?.Cancel();

        foreach (var tcs in _pending.Values)
            tcs.TrySetException(new SmppException("Connection closed"));
        _pending.Clear();

        _stream?.Dispose();
        _tcp?.Dispose();

        if (_readLoop != null)      try { await _readLoop; }      catch { }
        if (_keepAliveLoop != null) try { await _keepAliveLoop; } catch { }

        _stream = null;
        _tcp    = null;
    }

    // ── Sending ──────────────────────────────────────────────────

    public async Task<SubmitResult> SubmitAsync(SubmitRequest request, CancellationToken ct = default)
    {
        if (!IsConnected)
            throw new InvalidOperationException("SMPP client is not connected.");

        var encoded = MessageSplitter.Encode(request.Message, request.ForceUcs2);

        var messageId = encoded.IsMultipart
            ? await SubmitMultipartAsync(request, encoded, ct)
            : await SubmitSingleAsync(request, encoded.AllBytes, encoded.DataCoding, ct);

        return new SubmitResult
        {
            MessageId      = messageId,
            SegmentCount   = encoded.Segments.Count,
            DataCoding     = encoded.DataCoding,
            CorrelationId  = request.CorrelationId,
        };
    }

    public async Task<IReadOnlyList<SubmitResult>> SubmitBulkAsync(
        IEnumerable<SubmitRequest> requests,
        int maxConcurrency = 10,
        CancellationToken ct = default)
    {
        var semaphore = new SemaphoreSlim(maxConcurrency);
        var results   = new ConcurrentBag<SubmitResult>();

        var tasks = requests.Select(async req =>
        {
            await semaphore.WaitAsync(ct);
            try
            {
                var result = await SubmitAsync(req, ct);
                results.Add(result);
            }
            finally
            {
                semaphore.Release();
            }
        });

        await Task.WhenAll(tasks);
        return [.. results];
    }

    private async Task<string> SubmitSingleAsync(
        SubmitRequest request, byte[] msgBytes, byte dataCoding, CancellationToken ct)
    {
        var body = BuildSubmitSmBody(
            request.SourceAddress, request.DestinationAddress,
            msgBytes, dataCoding,
            request.RegisteredDelivery, request.ValidityPeriod);

        var resp = await SendAndWaitAsync(CommandId.SubmitSm, body, ct);

        if (!resp.IsOk)
            throw new SmppException(
                $"submit_sm failed: {CommandStatus.Describe(resp.CommandStatus)}", resp.CommandStatus);

        return new PduReader(resp.Body).ReadCString();
    }

    private async Task<string> SubmitMultipartAsync(
        SubmitRequest request, EncodedMessage encoded, CancellationToken ct)
    {
        var refNum   = (ushort)Random.Shared.Next(1, 65536);
        var total    = (byte)encoded.Segments.Count;
        var lastId   = string.Empty;

        for (byte i = 0; i < total; i++)
        {
            var body = BuildSubmitSmBody(
                request.SourceAddress, request.DestinationAddress,
                encoded.Segments[i], encoded.DataCoding,
                request.RegisteredDelivery, request.ValidityPeriod,
                sarRef: refNum, sarTotal: total, sarSeq: (byte)(i + 1));

            var resp = await SendAndWaitAsync(CommandId.SubmitSm, body, ct);

            if (!resp.IsOk)
                throw new SmppException(
                    $"submit_sm part {i + 1}/{total} failed: {CommandStatus.Describe(resp.CommandStatus)}",
                    resp.CommandStatus);

            lastId = new PduReader(resp.Body).ReadCString();

            _logger.LogDebug("SMPP multipart segment {Seq}/{Total} sent, msgId={MsgId}", i + 1, total, lastId);
        }

        return lastId;
    }

    private static byte[] BuildSubmitSmBody(
        string sourceAddr, string destAddr,
        byte[] msgBytes, byte dataCoding,
        bool registeredDelivery, TimeSpan? validityPeriod,
        ushort sarRef = 0, byte sarTotal = 0, byte sarSeq = 0)
    {
        var srcTon = sourceAddr.Any(char.IsLetter) ? (byte)0x05 : (byte)0x01; // 5=alphanumeric, 1=international
        var srcNpi = srcTon == 0x05 ? (byte)0x00 : (byte)0x01;

        var validity = validityPeriod.HasValue
            ? DateTime.UtcNow.Add(validityPeriod.Value).ToString("yyMMddHHmmss000+")
            : "";

        var body = new List<byte>();
        PduWriter.WriteCString(body, "");               // service_type
        PduWriter.WriteByte(body, srcTon);              // source_addr_ton
        PduWriter.WriteByte(body, srcNpi);              // source_addr_npi
        PduWriter.WriteCString(body, sourceAddr);       // source_addr
        PduWriter.WriteByte(body, 0x01);                // dest_addr_ton (international)
        PduWriter.WriteByte(body, 0x01);                // dest_addr_npi (E.164)
        PduWriter.WriteCString(body, destAddr);         // destination_addr
        PduWriter.WriteByte(body, 0x00);                // esm_class
        PduWriter.WriteByte(body, 0x00);                // protocol_id
        PduWriter.WriteByte(body, 0x00);                // priority_flag
        PduWriter.WriteCString(body, "");               // schedule_delivery_time
        PduWriter.WriteCString(body, validity);         // validity_period
        PduWriter.WriteByte(body, registeredDelivery ? (byte)0x01 : (byte)0x00);
        PduWriter.WriteByte(body, 0x00);                // replace_if_present_flag
        PduWriter.WriteByte(body, dataCoding);          // data_coding
        PduWriter.WriteByte(body, 0x00);                // sm_default_msg_id
        PduWriter.WriteByte(body, (byte)msgBytes.Length);
        PduWriter.WriteOctets(body, msgBytes);          // short_message

        // SAR TLV fields for multipart
        if (sarTotal > 1)
        {
            PduWriter.WriteTlvUInt16(body, TlvTag.SarMsgRefNum,      sarRef);
            PduWriter.WriteTlvByte(body,   TlvTag.SarTotalSegments,  sarTotal);
            PduWriter.WriteTlvByte(body,   TlvTag.SarSegmentSeqnum,  sarSeq);
        }

        return [.. body];
    }

    // ── Read loop ────────────────────────────────────────────────

    private async Task ReadLoopAsync(CancellationToken ct)
    {
        var header = new byte[16];

        while (!ct.IsCancellationRequested)
        {
            try
            {
                await ReadExactAsync(header, ct);
                var r      = new PduReader(header);
                var length = r.ReadUInt32();
                var cmdId  = r.ReadUInt32();
                var status = r.ReadUInt32();
                var seq    = r.ReadUInt32();

                var bodyLen = (int)length - 16;
                var body    = bodyLen > 0 ? new byte[bodyLen] : [];
                if (bodyLen > 0) await ReadExactAsync(body, ct);

                await DispatchAsync(new Pdu
                {
                    CommandLength  = length,
                    CommandId      = cmdId,
                    CommandStatus  = status,
                    SequenceNumber = seq,
                    Body           = body,
                }, ct);
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                _logger.LogError(ex, "SMPP read loop error");
                HandleDisconnect(ex);
                break;
            }
        }
    }

    private async Task DispatchAsync(Pdu pdu, CancellationToken ct)
    {
        switch (pdu.CommandId)
        {
            // Responses — complete the pending request
            case CommandId.BindTransceiverResp:
            case CommandId.BindTransmitterResp:
            case CommandId.BindReceiverResp:
            case CommandId.SubmitSmResp:
            case CommandId.UnbindResp:
            case CommandId.QuerySmResp:
            case CommandId.GenericNack:
            case CommandId.EnquireLinkResp:
                if (_pending.TryGetValue(pdu.SequenceNumber, out var tcs))
                    tcs.TrySetResult(pdu);
                break;

            // Incoming deliver_sm (MO or delivery receipt)
            case CommandId.DeliverSm:
                await HandleDeliverSmAsync(pdu, ct);
                break;

            // Server-initiated keepalive — respond immediately
            case CommandId.EnquireLink:
                var resp = PduWriter.Build(CommandId.EnquireLinkResp, 0, pdu.SequenceNumber, []);
                await SendRawAsync(resp, ct);
                break;
        }
    }

    private async Task HandleDeliverSmAsync(Pdu pdu, CancellationToken ct)
    {
        // ACK first — always
        var ack = PduWriter.Build(CommandId.DeliverSmResp, 0, pdu.SequenceNumber, [0x00]);
        await SendRawAsync(ack, ct);

        try
        {
            var r       = new PduReader(pdu.Body);
            r.ReadCString();                       // service_type
            r.ReadByte();                          // source_addr_ton
            r.ReadByte();                          // source_addr_npi
            var srcAddr  = r.ReadCString();
            r.ReadByte();                          // dest_addr_ton
            r.ReadByte();                          // dest_addr_npi
            var dstAddr  = r.ReadCString();
            var esmClass = r.ReadByte();
            r.ReadByte();                          // protocol_id
            r.ReadByte();                          // priority_flag
            r.ReadCString();                       // schedule_delivery_time
            r.ReadCString();                       // validity_period
            r.ReadByte();                          // registered_delivery
            r.ReadByte();                          // replace_if_present
            var dataCoding = r.ReadByte();
            r.ReadByte();                          // sm_default_msg_id
            var smLen    = r.ReadByte();
            var msgBytes = r.ReadBytes(smLen);

            var text = dataCoding == DataCoding.Ucs2
                ? Encoding.BigEndianUnicode.GetString(msgBytes)
                : Gsm7Encoder.Decode(msgBytes);

            var isReceipt = (esmClass & 0x04) != 0;

            if (isReceipt)
            {
                var receipt = DeliveryReceiptParser.TryParse(text);
                if (receipt != null && OnDeliveryReceived != null)
                    await OnDeliveryReceived(receipt);
            }
            else
            {
                if (OnMessageReceived != null)
                    await OnMessageReceived(new DeliverMessage
                    {
                        SourceAddress      = srcAddr,
                        DestinationAddress = dstAddr,
                        Message            = text,
                        DataCoding         = dataCoding,
                    });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process deliver_sm");
        }
    }

    // ── Keepalive ────────────────────────────────────────────────

    private async Task KeepAliveLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(_options.EnquireLinkInterval, ct);

                if (!IsConnected) continue;

                await SendAndWaitAsync(CommandId.EnquireLink, [], ct);
                _logger.LogTrace("SMPP enquire_link OK");
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "SMPP enquire_link failed");
            }
        }
    }

    // ── Auto-reconnect ───────────────────────────────────────────

    private async Task ReconnectLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            // Wait until disconnected
            while (IsConnected && !ct.IsCancellationRequested)
                await Task.Delay(500, ct);

            if (ct.IsCancellationRequested) break;

            var attempt = 0;

            while (!IsConnected && !ct.IsCancellationRequested)
            {
                attempt++;
                if (_options.MaxReconnectAttempts > 0 && attempt > _options.MaxReconnectAttempts)
                {
                    _logger.LogError("SMPP max reconnect attempts ({Max}) reached — giving up",
                        _options.MaxReconnectAttempts);
                    return;
                }

                _logger.LogInformation("SMPP reconnect attempt {Attempt}…", attempt);

                try
                {
                    await Task.Delay(_options.ReconnectDelay, ct);
                    await ConnectInternalAsync(ct);

                    _logger.LogInformation("SMPP reconnected successfully");
                    OnReconnected?.Invoke();
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "SMPP reconnect attempt {Attempt} failed", attempt);
                }
            }
        }
    }

    private void HandleDisconnect(Exception? ex)
    {
        if (!IsConnected) return;
        IsConnected = false;

        foreach (var tcs in _pending.Values)
            tcs.TrySetException(new SmppException("Connection lost", ex));
        _pending.Clear();

        OnDisconnected?.Invoke(ex);
    }

    // ── Low-level ────────────────────────────────────────────────

    private async Task<Pdu> SendAndWaitAsync(uint commandId, byte[] body, CancellationToken ct)
    {
        var seq = Interlocked.Increment(ref _sequence);
        var raw = PduWriter.Build(commandId, 0, seq, body);
        var tcs = new TaskCompletionSource<Pdu>(TaskCreationOptions.RunContinuationsAsynchronously);

        _pending[seq] = tcs;

        try
        {
            await SendRawAsync(raw, ct);

            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(_options.ResponseTimeout);

            return await tcs.Task.WaitAsync(timeout.Token);
        }
        finally
        {
            _pending.TryRemove(seq, out _);
        }
    }

    private async Task SendRawAsync(byte[] pdu, CancellationToken ct)
    {
        await _writeLock.WaitAsync(ct);
        try { await _stream!.WriteAsync(pdu, ct); }
        finally { _writeLock.Release(); }
    }

    private async Task ReadExactAsync(byte[] buffer, CancellationToken ct)
    {
        var total = 0;
        while (total < buffer.Length)
        {
            var read = await _stream!.ReadAsync(buffer.AsMemory(total), ct);
            if (read == 0) throw new EndOfStreamException("SMPP connection closed by remote host");
            total += read;
        }
    }

    // ── IAsyncDisposable ─────────────────────────────────────────

    public async ValueTask DisposeAsync()
    {
        await DisconnectAsync();
        _cts?.Dispose();
        _writeLock.Dispose();
    }
}
