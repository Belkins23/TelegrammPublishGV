using System.Net;

namespace TelegrammPublishGV.Services;

/// <summary>
/// The publishing API is an internal service. Nginx connects over loopback and appends
/// the real caller to X-Forwarded-For, so the last address must also be private.
/// </summary>
public static class InternalRequestGuard
{
    public static bool IsAllowed(HttpContext context)
    {
        var direct = context.Connection.RemoteIpAddress;
        if (direct is null || !IsPrivate(direct))
            return false;

        var forwarded = context.Request.Headers["X-Forwarded-For"].ToString();
        if (string.IsNullOrWhiteSpace(forwarded))
            return true;

        var last = forwarded.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .LastOrDefault();
        return IPAddress.TryParse(last, out var caller) && IsPrivate(caller);
    }

    private static bool IsPrivate(IPAddress address)
    {
        if (IPAddress.IsLoopback(address)) return true;

        if (address.IsIPv4MappedToIPv6)
            address = address.MapToIPv4();

        var bytes = address.GetAddressBytes();
        if (bytes.Length == 4)
            return bytes[0] == 10
                || bytes[0] == 127
                || bytes[0] == 192 && bytes[1] == 168
                || bytes[0] == 172 && bytes[1] is >= 16 and <= 31;

        return address.IsIPv6LinkLocal || (bytes[0] & 0xfe) == 0xfc;
    }
}
