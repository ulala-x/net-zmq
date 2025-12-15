using System.Runtime.InteropServices;
using Net.Zmq.Core.Native;

namespace Net.Zmq;

/// <summary>
/// Z85 encoding/decoding utilities.
/// </summary>
public static class Z85
{
    /// <summary>
    /// Encodes binary data to Z85 string.
    /// Input size must be divisible by 4.
    /// </summary>
    public static string Encode(ReadOnlySpan<byte> data)
    {
        if (data.Length % 4 != 0)
            throw new ArgumentException("Data length must be divisible by 4", nameof(data));

        if (data.Length == 0)
            return string.Empty;

        var destSize = data.Length * 5 / 4 + 1;
        Span<byte> dest = stackalloc byte[destSize];

        unsafe
        {
            fixed (byte* dataPtr = data)
            fixed (byte* destPtr = dest)
            {
                var result = LibZmq.Z85Encode((nint)destPtr, (nint)dataPtr, (nuint)data.Length);
                if (result == IntPtr.Zero)
                    throw new ZmqException();

                return Marshal.PtrToStringAnsi(result) ?? string.Empty;
            }
        }
    }

    /// <summary>
    /// Decodes Z85 string to binary data.
    /// Input length must be divisible by 5.
    /// </summary>
    public static byte[] Decode(string encoded)
    {
        ArgumentNullException.ThrowIfNull(encoded);

        if (encoded.Length == 0)
            return Array.Empty<byte>();

        if (encoded.Length % 5 != 0)
            throw new ArgumentException("Encoded string length must be divisible by 5", nameof(encoded));

        var destSize = encoded.Length * 4 / 5;
        var dest = new byte[destSize];

        unsafe
        {
            fixed (byte* destPtr = dest)
            {
                var strPtr = Marshal.StringToHGlobalAnsi(encoded);
                try
                {
                    var result = LibZmq.Z85Decode((nint)destPtr, strPtr);
                    if (result == IntPtr.Zero)
                        throw new ZmqException();
                    return dest;
                }
                finally
                {
                    Marshal.FreeHGlobal(strPtr);
                }
            }
        }
    }
}
