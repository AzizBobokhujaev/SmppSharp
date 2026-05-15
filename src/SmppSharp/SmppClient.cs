using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net.Security;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using SmppSharp.Codec;
using SmppSharp.Internal;
using SmppSharp.Models;
using SmppSharp.Protocol;

namespace SmppSharp;

/// <summary>
/// SMPP 3.4 client. Thread-safe, auto-reconnecting, supports text/binary/flash/WAP Push.
/// Integrates with System.Diagnostics.Metrics for observability.
/// </summary>
public sealed class SmppClient : ISmppClient
{
    private readonly SmppOptions _options;
    private readonly ILogger<SmppClient> _logger;

    private TcpClient? _tcp;
    private Stream? _stream;                    // NetworkStream or SslStream
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

    private void LogVerbose(string message, params object?[] args)
    {
        if (_options.VerboseLogging)
            _logger.LogInformation("[SMPP-VERBOSE] " + message, args);
    }

    private static string CommandName(uint id) => id switch
    {
        CommandId.BindTransceiver     => "bind_transceiver",
        CommandId.BindTransceiverResp => "bind_transceiver_resp",
        CommandId.BindTransmitter     => "bind_transmitter",
        CommandId.BindTransmitterResp => "bind_transmitter_resp",
        CommandId.BindReceiver        => "bind_receiver",
        CommandId.BindReceiverResp    => "bind_receiver_resp",
        CommandId.SubmitSm            => "submit_sm",
        CommandId.SubmitSmResp        => "submit_sm_resp",
        CommandId.DeliverSm           => "deliver_sm",
        CommandId.DeliverSmResp       => "deliver_sm_resp",
        CommandId.EnquireLink         => "enquire_link",
        CommandId.EnquireLinkResp     => "enquire_link_resp",
        CommandId.Unbind              => "unbind",
        CommandId.UnbindResp          => "unbind_resp",
        CommandId.GenericNack         => "generic_nack",
        CommandId.QuerySm             => "query_sm",
        CommandId.QuerySmResp         => "query_sm_resp",
        _                             => $"0x{id:X8}",
    };

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
        LogVerbose("ConnectInternal starting to {Host}:{Port}", _options.Host, _options.Port);

        // Clean up old loops/streams before creating new ones (reconnect scenario)
        _stream?.Dispose();
        _tcp?.Dispose();

        _tcp = new TcpClient { NoDelay = true };

        // TCP KeepAlive to detect dead connections at OS level
        if (_options.TcpKeepAlive)
        {
            _tcp.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);
            _tcp.Client.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.TcpKeepAliveTime, _options.TcpKeepAliveInterval);
            _tcp.Client.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.TcpKeepAliveInterval, _options.TcpKeepAliveInterval);
            _tcp.Client.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.TcpKeepAliveRetryCount, 3);
            LogVerbose("TCP KeepAlive enabled: interval={Interval}s, retries=3", _options.TcpKeepAliveInterval);
        }

        // Connect with timeout to avoid waiting minutes for OS default
        LogVerbose("TCP connecting to {Host}:{Port} (timeout={Timeout}s)…",
            _options.Host, _options.Port, _options.ConnectTimeout.TotalSeconds);
        var sw = Stopwatch.StartNew();

        using var connectCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        connectCts.CancelAfter(_options.ConnectTimeout);
        try
        {
            await _tcp.ConnectAsync(_options.Host, _options.Port, connectCts.Token);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            throw new SmppException($"TCP connect to {_options.Host}:{_options.Port} timed out after {_options.ConnectTimeout.TotalSeconds}s");
        }

        sw.Stop();
        LogVerbose("TCP connected in {Elapsed}ms", sw.ElapsedMilliseconds);

        Stream baseStream = _tcp.GetStream();

        if (_options.UseSsl)
        {
            var ssl = new SslStream(
                baseStream,
                leaveInnerStreamOpen: false,
                userCertificateValidationCallback: _options.AllowUntrustedCertificate
                    ? (_, _, _, _) => true
                    : null);

            await ssl.AuthenticateAsClientAsync(_options.SslTargetHost ?? _options.Host);

            _stream = ssl;
            _logger.LogDebug("SMPP TLS handshake OK ({Protocol})", ssl.SslProtocol);
        }
        else
        {
            _stream = baseStream;
        }

        LogVerbose("Starting read loop and keepalive loop");
        _readLoop      = Task.Run(() => ReadLoopAsync(ct), CancellationToken.None);
        _keepAliveLoop = Task.Run(() => KeepAliveLoopAsync(ct), CancellationToken.None);

        LogVerbose("Sending bind (mode={Mode}, systemId={SystemId})", _options.BindMode, _options.SystemId);
        await BindAsync(ct);

        IsConnected = true;
        SmppMetrics.ActiveConnections.Add(1);
        _logger.LogInformation("SMPP connected and bound to {Host}:{Port} (ssl={Ssl})",
            _options.Host, _options.Port, _options.UseSsl);
    }

    private async Task BindAsync(CancellationToken ct)
    {
        var commandId = _options.BindMode switch
        {
            BindMode.Transmitter => CommandId.BindTransmitter,
            BindMode.Receiver    => CommandId.BindReceiver,
            _                    => CommandId.BindTransceiver,
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
            throw new SmppException(
                $"Bind failed: {CommandStatus.Describe(resp.CommandStatus)}", resp.CommandStatus);
    }

    public async Task DisconnectAsync(CancellationToken ct = default)
    {
        if (IsConnected)
            try { await SendAndWaitAsync(CommandId.Unbind, [], ct); } catch { }
        await CleanupAsync();
    }

    private async Task CleanupAsync()
    {
        if (IsConnected) SmppMetrics.ActiveConnections.Add(-1);
        IsConnected = false;
        _cts?.Cancel();

        foreach (var tcs in _pending.Values)
            tcs.TrySetException(new SmppException("Connection closed"));
        _pending.Clear();

        _stream?.Dispose();
        _tcp?.Dispose();

        if (_readLoop      != null) try { await _readLoop; }      catch { }
        if (_keepAliveLoop != null) try { await _keepAliveLoop; } catch { }
        if (_reconnectLoop != null) try { await _reconnectLoop; } catch { }

        _stream = null;
        _tcp    = null;
    }

    // ── Sending ──────────────────────────────────────────────────

    public async Task<SubmitResult> SubmitAsync(SubmitRequest request, CancellationToken ct = default)
    {
        if (!IsConnected)
            throw new InvalidOperationException("SMPP client is not connected.");

        var encoded = request.Payload != null
            ? MessageSplitter.EncodeBinary(request.Payload)
            : MessageSplitter.Encode(request.Message, request.ForceUcs2, request.IsFlash);

        var sw = Stopwatch.StartNew();
        try
        {
            var messageId = encoded.IsMultipart && _options.UseMessagePayload
                ? await SubmitWithPayloadAsync(request, encoded, ct)
                : encoded.IsMultipart
                    ? await SubmitMultipartAsync(request, encoded, ct)
                    : await SubmitSingleAsync(request, encoded.AllBytes, encoded.DataCoding, ct);

            sw.Stop();
            SmppMetrics.MessagesSent.Add(encoded.Segments.Count);
            SmppMetrics.SubmitDuration.Record(sw.Elapsed.TotalMilliseconds);

            return new SubmitResult
            {
                MessageId     = messageId,
                SegmentCount  = encoded.Segments.Count,
                DataCoding    = encoded.DataCoding,
                CorrelationId = request.CorrelationId,
            };
        }
        catch
        {
            SmppMetrics.MessagesFailed.Add(1);
            throw;
        }
    }

    public async Task<IReadOnlyList<SubmitResult>> SubmitBulkAsync(
        IEnumerable<SubmitRequest> requests,
        int maxConcurrency = 10,
        CancellationToken ct = default)
    {
        var sem     = new SemaphoreSlim(maxConcurrency);
        var results = new ConcurrentBag<SubmitResult>();

        var tasks = requests.Select(async req =>
        {
            await sem.WaitAsync(ct);
            try   { results.Add(await SubmitAsync(req, ct)); }
            finally { sem.Release(); }
        });

        await Task.WhenAll(tasks);
        return [.. results];
    }

    public async IAsyncEnumerable<SubmitResult> SubmitPipelineAsync(
        IAsyncEnumerable<SubmitRequest> requests,
        int concurrency = 100,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var channel = Channel.CreateBounded<SubmitResult>(
            new BoundedChannelOptions(concurrency * 2)
            {
                SingleReader          = true,
                SingleWriter          = false,
                FullMode              = BoundedChannelFullMode.Wait,
                AllowSynchronousContinuations = false,
            });

        var producer = Task.Run(async () =>
        {
            var sem   = new SemaphoreSlim(concurrency);
            var tasks = new List<Task>();

            await foreach (var request in requests.WithCancellation(ct))
            {
                await sem.WaitAsync(ct);
                tasks.Add(Task.Run(async () =>
                {
                    try
                    {
                        var result = await SubmitAsync(request, ct);
                        await channel.Writer.WriteAsync(result, ct);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Pipeline: failed to submit to {Dest}",
                            request.DestinationAddress);
                    }
                    finally { sem.Release(); }
                }, ct));
            }

            await Task.WhenAll(tasks);
            channel.Writer.Complete();
        }, CancellationToken.None);

        await foreach (var result in channel.Reader.ReadAllAsync(ct))
            yield return result;

        await producer;
    }

    // ── Internal submit helpers ──────────────────────────────────

    private async Task<string> SubmitSingleAsync(
        SubmitRequest request, byte[] msgBytes, byte dataCoding, CancellationToken ct)
    {
        var body = BuildSubmitSmBody(
            request.SourceAddress, request.DestinationAddress,
            msgBytes, dataCoding,
            request.RegisteredDelivery, request.ValidityPeriod,
            esmClass: request.EsmClass);

        var resp = await SendAndWaitAsync(CommandId.SubmitSm, body, ct);

        if (!resp.IsOk)
            throw new SmppException(
                $"submit_sm failed: {CommandStatus.Describe(resp.CommandStatus)}", resp.CommandStatus);

        return new PduReader(resp.Body).ReadCString();
    }

    private async Task<string> SubmitMultipartAsync(
        SubmitRequest request, EncodedMessage encoded, CancellationToken ct)
    {
        var refNum  = (byte)Random.Shared.Next(1, 256);
        var total   = (byte)encoded.Segments.Count;
        var lastId  = string.Empty;
        var useUdh  = _options.UseUdh;

        for (byte i = 0; i < total; i++)
        {
            var segBytes  = encoded.Segments[i];
            byte esmClass = request.EsmClass;
            ushort sarRef = 0;

            byte[] msgBytes;
            if (useUdh)
            {
                // Prepend 6-byte UDH: 05 00 03 <ref> <total> <seq>
                // esmClass |= 0x40 (UDHI bit)
                msgBytes = new byte[6 + segBytes.Length];
                msgBytes[0] = 0x05;           // UDH length
                msgBytes[1] = 0x00;           // IE ID: concat 8-bit ref
                msgBytes[2] = 0x03;           // IE data length
                msgBytes[3] = refNum;         // reference number
                msgBytes[4] = total;          // total segments
                msgBytes[5] = (byte)(i + 1);  // segment seq (1-based)
                segBytes.CopyTo(msgBytes, 6);
                esmClass |= 0x40;
            }
            else
            {
                msgBytes = segBytes;
                sarRef   = (ushort)refNum;
            }

            var body = BuildSubmitSmBody(
                request.SourceAddress, request.DestinationAddress,
                msgBytes, encoded.DataCoding,
                request.RegisteredDelivery, request.ValidityPeriod,
                esmClass: esmClass,
                sarRef: sarRef, sarTotal: useUdh ? (byte)0 : total, sarSeq: useUdh ? (byte)0 : (byte)(i + 1));

            var resp = await SendAndWaitAsync(CommandId.SubmitSm, body, ct);

            if (!resp.IsOk)
                throw new SmppException(
                    $"submit_sm part {i + 1}/{total} failed: {CommandStatus.Describe(resp.CommandStatus)}",
                    resp.CommandStatus);

            lastId = new PduReader(resp.Body).ReadCString();
            _logger.LogDebug("SMPP multipart {Mode} {Seq}/{Total} → msgId={Id}",
                useUdh ? "UDH" : "SAR", i + 1, total, lastId);
        }

        return lastId;
    }

    /// <summary>
    /// Sends a long message as a single submit_sm using message_payload TLV (0x0424).
    /// sm_length=0, short_message empty. SMSC handles splitting and concatenation.
    /// </summary>
    private async Task<string> SubmitWithPayloadAsync(
        SubmitRequest request, EncodedMessage encoded, CancellationToken ct)
    {
        var body = BuildSubmitSmBody(
            request.SourceAddress, request.DestinationAddress,
            [], encoded.DataCoding,
            request.RegisteredDelivery, request.ValidityPeriod,
            esmClass: request.EsmClass,
            messagePayload: encoded.AllBytes);

        var resp = await SendAndWaitAsync(CommandId.SubmitSm, body, ct);

        if (!resp.IsOk)
            throw new SmppException(
                $"submit_sm (message_payload) failed: {CommandStatus.Describe(resp.CommandStatus)}", resp.CommandStatus);

        var messageId = new PduReader(resp.Body).ReadCString();
        _logger.LogDebug("SMPP message_payload len={Len} → msgId={Id}", encoded.AllBytes.Length, messageId);
        return messageId;
    }

    private static byte[] BuildSubmitSmBody(
        string sourceAddr, string destAddr,
        byte[] msgBytes, byte dataCoding,
        bool registeredDelivery, TimeSpan? validityPeriod,
        byte esmClass = 0x00,
        ushort sarRef = 0, byte sarTotal = 0, byte sarSeq = 0,
        byte[]? messagePayload = null)
    {
        var srcTon = sourceAddr.Any(char.IsLetter) ? (byte)0x05 : (byte)0x01;
        var srcNpi = srcTon == 0x05 ? (byte)0x00 : (byte)0x01;

        var validity = validityPeriod.HasValue
            ? DateTime.UtcNow.Add(validityPeriod.Value).ToString("yyMMddHHmmss000+")
            : "";

        var body = new List<byte>();
        PduWriter.WriteCString(body, "");               // service_type
        PduWriter.WriteByte(body, srcTon);
        PduWriter.WriteByte(body, srcNpi);
        PduWriter.WriteCString(body, sourceAddr);
        PduWriter.WriteByte(body, 0x01);                // dest_addr_ton: international
        PduWriter.WriteByte(body, 0x01);                // dest_addr_npi: E.164
        PduWriter.WriteCString(body, destAddr);
        PduWriter.WriteByte(body, esmClass);
        PduWriter.WriteByte(body, 0x00);                // protocol_id
        PduWriter.WriteByte(body, 0x00);                // priority_flag
        PduWriter.WriteCString(body, "");               // schedule_delivery_time
        PduWriter.WriteCString(body, validity);
        PduWriter.WriteByte(body, registeredDelivery ? (byte)0x01 : (byte)0x00);
        PduWriter.WriteByte(body, 0x00);                // replace_if_present
        PduWriter.WriteByte(body, dataCoding);
        PduWriter.WriteByte(body, 0x00);                // sm_default_msg_id

        if (messagePayload != null)
        {
            // message_payload TLV: sm_length=0, short_message empty, full text in TLV 0x0424
            PduWriter.WriteByte(body, 0x00);
            PduWriter.WriteTlvOctets(body, TlvTag.MessagePayload, messagePayload);
        }
        else
        {
            PduWriter.WriteByte(body, (byte)msgBytes.Length);
            PduWriter.WriteOctets(body, msgBytes);

            if (sarTotal > 1)
            {
                PduWriter.WriteTlvUInt16(body, TlvTag.SarMsgRefNum,     sarRef);
                PduWriter.WriteTlvByte(body,   TlvTag.SarTotalSegments, sarTotal);
                PduWriter.WriteTlvByte(body,   TlvTag.SarSegmentSeqnum, sarSeq);
            }
        }

        return [.. body];
    }

    // ── Read loop ────────────────────────────────────────────────

    private async Task ReadLoopAsync(CancellationToken ct)
    {
        LogVerbose("Read loop started");
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

                LogVerbose("PDU recv: cmd={Cmd} status=0x{Status:X8} seq={Seq} len={Len}",
                    CommandName(cmdId), status, seq, length);

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
            catch (OperationCanceledException)
            {
                LogVerbose("Read loop cancelled");
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "SMPP read loop error: {Message}", ex.Message);
                LogVerbose("Read loop exiting due to error: {Type}: {Message}", ex.GetType().Name, ex.Message);
                HandleDisconnect(ex);
                break;
            }
        }
        LogVerbose("Read loop ended");
    }

    private async Task DispatchAsync(Pdu pdu, CancellationToken ct)
    {
        switch (pdu.CommandId)
        {
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

            case CommandId.DeliverSm:
                await HandleDeliverSmAsync(pdu, ct);
                break;

            case CommandId.EnquireLink:
                var resp = PduWriter.Build(CommandId.EnquireLinkResp, 0, pdu.SequenceNumber, []);
                await SendRawAsync(resp, ct);
                break;
        }
    }

    private async Task HandleDeliverSmAsync(Pdu pdu, CancellationToken ct)
    {
        var ack = PduWriter.Build(CommandId.DeliverSmResp, 0, pdu.SequenceNumber, [0x00]);
        await SendRawAsync(ack, ct);

        try
        {
            var r       = new PduReader(pdu.Body);
            r.ReadCString();
            r.ReadByte();
            r.ReadByte();
            var srcAddr  = r.ReadCString();
            r.ReadByte();
            r.ReadByte();
            var dstAddr  = r.ReadCString();
            var esmClass = r.ReadByte();
            r.ReadByte();
            r.ReadByte();
            r.ReadCString();
            r.ReadCString();
            r.ReadByte();
            r.ReadByte();
            var dataCoding = r.ReadByte();
            r.ReadByte();
            var smLen    = r.ReadByte();
            var msgBytes = r.ReadBytes(smLen);

            var text = dataCoding == DataCoding.Ucs2 || dataCoding == DataCoding.Ucs2Flash
                ? Encoding.BigEndianUnicode.GetString(msgBytes)
                : Gsm7Encoder.Decode(msgBytes);

            var isReceipt = (esmClass & 0x04) != 0;

            if (isReceipt)
            {
                SmppMetrics.ReceiptsReceived.Add(1);
                var receipt = DeliveryReceiptParser.TryParse(text);
                if (receipt != null && OnDeliveryReceived != null)
                    await OnDeliveryReceived(receipt);
            }
            else
            {
                SmppMetrics.MessagesReceived.Add(1);
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

    private int _enquireLinkFailCount;

    private async Task KeepAliveLoopAsync(CancellationToken ct)
    {
        LogVerbose("KeepAlive loop started (interval={Interval}s)", _options.EnquireLinkInterval.TotalSeconds);
        _enquireLinkFailCount = 0;
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(_options.EnquireLinkInterval, ct);
                if (!IsConnected)
                {
                    LogVerbose("KeepAlive: IsConnected=false, exiting loop");
                    break;
                }
                var sw = Stopwatch.StartNew();
                await SendAndWaitAsync(CommandId.EnquireLink, [], ct);
                sw.Stop();
                Interlocked.Exchange(ref _enquireLinkFailCount, 0);
                LogVerbose("enquire_link OK (rtt={Rtt}ms)", sw.ElapsedMilliseconds);
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                var fails = Interlocked.Increment(ref _enquireLinkFailCount);
                _logger.LogWarning(ex, "SMPP enquire_link failed ({Fails} consecutive): {Message}", fails, ex.Message);
                if (fails >= 2)
                {
                    _logger.LogError("SMPP enquire_link failed {Fails} times — forcing disconnect", fails);
                    HandleDisconnect(ex);
                    break;
                }
            }
        }
        LogVerbose("KeepAlive loop ended");
    }

    // ── Auto-reconnect ───────────────────────────────────────────

    private async Task ReconnectLoopAsync(CancellationToken ct)
    {
        LogVerbose("Reconnect loop started (autoReconnect={Auto}, delay={Delay}s)",
            _options.AutoReconnect, _options.ReconnectDelay.TotalSeconds);

        while (!ct.IsCancellationRequested)
        {
            while (IsConnected && !ct.IsCancellationRequested)
                await Task.Delay(500, ct);

            if (ct.IsCancellationRequested) break;

            LogVerbose("Reconnect loop: connection lost, waiting for old loops to finish…");
            if (_readLoop != null) try { await _readLoop; } catch { }
            if (_keepAliveLoop != null) try { await _keepAliveLoop; } catch { }
            LogVerbose("Reconnect loop: old loops finished");

            var attempt = 0;
            while (!IsConnected && !ct.IsCancellationRequested)
            {
                attempt++;
                if (_options.MaxReconnectAttempts > 0 && attempt > _options.MaxReconnectAttempts)
                {
                    _logger.LogError("SMPP max reconnect attempts ({Max}) reached", _options.MaxReconnectAttempts);
                    return;
                }

                _logger.LogInformation("SMPP reconnect attempt {Attempt} to {Host}:{Port}…",
                    attempt, _options.Host, _options.Port);
                SmppMetrics.Reconnects.Add(1);

                try
                {
                    await Task.Delay(_options.ReconnectDelay, ct);
                    await ConnectInternalAsync(ct);
                    _logger.LogInformation("SMPP reconnected successfully after {Attempts} attempt(s)", attempt);
                    OnReconnected?.Invoke();
                }
                catch (Exception ex)
                {
                    _logger.LogWarning("SMPP reconnect attempt {Attempt} failed: {Error}", attempt, ex.Message);
                }
            }
        }
    }

    private void HandleDisconnect(Exception? ex)
    {
        var pendingCount = _pending.Count;
        LogVerbose("HandleDisconnect called: IsConnected={Connected}, pending={Pending}, reason={Reason}",
            IsConnected, pendingCount, ex?.Message ?? "null");

        // Always fail pending requests (e.g. bind during initial connect) regardless of IsConnected.
        foreach (var tcs in _pending.Values)
            tcs.TrySetException(new SmppException("Connection lost", ex));
        _pending.Clear();

        if (!IsConnected) return;
        SmppMetrics.ActiveConnections.Add(-1);
        IsConnected = false;

        _logger.LogWarning("SMPP disconnected from {Host}:{Port}: {Reason}",
            _options.Host, _options.Port, ex?.Message ?? "unknown");
        OnDisconnected?.Invoke(ex);
    }

    // ── Low-level ────────────────────────────────────────────────

    private async Task<Pdu> SendAndWaitAsync(uint commandId, byte[] body, CancellationToken ct)
    {
        var seq = Interlocked.Increment(ref _sequence);
        var raw = PduWriter.Build(commandId, 0, seq, body);
        var tcs = new TaskCompletionSource<Pdu>(TaskCreationOptions.RunContinuationsAsynchronously);

        LogVerbose("PDU send: cmd={Cmd} seq={Seq} bodyLen={Len}", CommandName(commandId), seq, body.Length);

        _pending[seq] = tcs;
        try
        {
            await SendRawAsync(raw, ct);
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(_options.ResponseTimeout);
            var sw = Stopwatch.StartNew();
            var resp = await tcs.Task.WaitAsync(timeout.Token);
            sw.Stop();
            LogVerbose("PDU resp: cmd={Cmd} seq={Seq} status=0x{Status:X8} rtt={Rtt}ms",
                CommandName(resp.CommandId), seq, resp.CommandStatus, sw.ElapsedMilliseconds);
            return resp;
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            _logger.LogError("SMPP response timeout: cmd={Cmd} seq={Seq} after {Timeout}s (pending={Pending})",
                CommandName(commandId), seq, _options.ResponseTimeout.TotalSeconds, _pending.Count);
            throw;
        }
        finally { _pending.TryRemove(seq, out _); }
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
        _cts?.Cancel();
        await DisconnectAsync();
        _cts?.Dispose();
        _writeLock.Dispose();
    }
}
