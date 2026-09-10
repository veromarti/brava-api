using System.Security.Cryptography;

namespace Brava.Api.Modules.Wishlists;

/// <summary>
/// Short share codes for wishlist links (/lista-de-deseos/{code}). Crockford
/// base32 without the ambiguous glyphs (no 0/O, 1/I/L) so a code read off a
/// screen or dictated over the phone doesn't get mistyped. 8 chars over a
/// 31-symbol alphabet is ~39 bits — collisions are still handled by a retry
/// loop at the call site.
/// </summary>
public static class WishlistCode
{
    private const string Alphabet = "23456789ABCDEFGHJKMNPQRSTUVWXYZ";
    private const int Length = 8;

    public static string Generate()
    {
        Span<char> chars = stackalloc char[Length];
        for (var i = 0; i < Length; i++)
        {
            chars[i] = Alphabet[RandomNumberGenerator.GetInt32(Alphabet.Length)];
        }
        return new string(chars);
    }
}
