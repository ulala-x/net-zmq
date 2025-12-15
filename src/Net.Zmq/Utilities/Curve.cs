using System.Runtime.InteropServices;
using Net.Zmq.Core.Native;

namespace Net.Zmq;

/// <summary>
/// CURVE encryption key utilities.
/// </summary>
public static class Curve
{
    public const int KeySize = 32;
    public const int Z85KeySize = 41;

    /// <summary>
    /// Generates a new CURVE keypair.
    /// </summary>
    public static (string PublicKey, string SecretKey) GenerateKeypair()
    {
        if (!Context.Has("curve"))
            throw new NotSupportedException("CURVE security is not available");

        Span<byte> publicKey = stackalloc byte[Z85KeySize];
        Span<byte> secretKey = stackalloc byte[Z85KeySize];

        unsafe
        {
            fixed (byte* pubPtr = publicKey)
            fixed (byte* secPtr = secretKey)
            {
                var result = LibZmq.CurveKeypair((nint)pubPtr, (nint)secPtr);
                ZmqException.ThrowIfError(result);

                return (
                    Marshal.PtrToStringAnsi((nint)pubPtr) ?? string.Empty,
                    Marshal.PtrToStringAnsi((nint)secPtr) ?? string.Empty
                );
            }
        }
    }

    /// <summary>
    /// Derives public key from secret key.
    /// </summary>
    public static string DerivePublicKey(string secretKey)
    {
        ArgumentNullException.ThrowIfNull(secretKey);
        if (!Context.Has("curve"))
            throw new NotSupportedException("CURVE security is not available");

        Span<byte> publicKey = stackalloc byte[Z85KeySize];
        var secPtr = Marshal.StringToHGlobalAnsi(secretKey);

        try
        {
            unsafe
            {
                fixed (byte* pubPtr = publicKey)
                {
                    var result = LibZmq.CurvePublic((nint)pubPtr, secPtr);
                    ZmqException.ThrowIfError(result);
                    return Marshal.PtrToStringAnsi((nint)pubPtr) ?? string.Empty;
                }
            }
        }
        finally
        {
            Marshal.FreeHGlobal(secPtr);
        }
    }
}
