using SmppSharp.Models;
using System.Globalization;
using System.Text.RegularExpressions;

namespace SmppSharp.Internal;

/// <summary>
/// Parses SMSC delivery receipt text in the standard format:
/// id:XXXX sub:001 dlvrd:001 submit date:YYMMDDHHMM done date:YYMMDDHHMM stat:DELIVRD err:000 text:...
/// </summary>
internal static partial class DeliveryReceiptParser
{
    [GeneratedRegex(@"id:(\S+)", RegexOptions.IgnoreCase)]
    private static partial Regex IdPattern();

    [GeneratedRegex(@"sub:(\d+)", RegexOptions.IgnoreCase)]
    private static partial Regex SubPattern();

    [GeneratedRegex(@"dlvrd:(\d+)", RegexOptions.IgnoreCase)]
    private static partial Regex DlvrdPattern();

    [GeneratedRegex(@"submit date:(\d{10})", RegexOptions.IgnoreCase)]
    private static partial Regex SubmitDatePattern();

    [GeneratedRegex(@"done date:(\d{10})", RegexOptions.IgnoreCase)]
    private static partial Regex DoneDatePattern();

    [GeneratedRegex(@"stat:(\w+)", RegexOptions.IgnoreCase)]
    private static partial Regex StatPattern();

    [GeneratedRegex(@"err:(\S+)", RegexOptions.IgnoreCase)]
    private static partial Regex ErrPattern();

    public static DeliveryReceipt? TryParse(string text)
    {
        var idMatch = IdPattern().Match(text);
        if (!idMatch.Success) return null;

        var statMatch = StatPattern().Match(text);
        if (!statMatch.Success) return null;

        return new DeliveryReceipt
        {
            MessageId   = idMatch.Groups[1].Value,
            Status      = ParseStatus(statMatch.Groups[1].Value),
            ErrorCode   = ErrPattern().Match(text) is { Success: true } e ? e.Groups[1].Value : null,
            Submitted   = int.TryParse(SubPattern().Match(text).Groups[1].Value, out var sub) ? sub : 0,
            Delivered   = int.TryParse(DlvrdPattern().Match(text).Groups[1].Value, out var dlv) ? dlv : 0,
            SubmitDate  = ParseDate(SubmitDatePattern().Match(text).Groups[1].Value),
            DoneDate    = ParseDate(DoneDatePattern().Match(text).Groups[1].Value),
        };
    }

    private static DeliveryStatus ParseStatus(string stat) => stat.ToUpperInvariant() switch
    {
        "DELIVRD" or "DELIVERED" => DeliveryStatus.Delivered,
        "UNDELIV"                => DeliveryStatus.Undeliverable,
        "REJECTD"                => DeliveryStatus.Rejected,
        "EXPIRED" or "ENROUTE"   => DeliveryStatus.Expired,
        "FAILED"                 => DeliveryStatus.Failed,
        _                        => DeliveryStatus.Unknown
    };

    private static DateTime? ParseDate(string value)
    {
        if (string.IsNullOrEmpty(value)) return null;
        return DateTime.TryParseExact(value, "yyMMddHHmm",
            CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var dt)
            ? dt.ToUniversalTime()
            : null;
    }
}
