using System.Security.Cryptography;
using System.Text;

namespace CentralAuthNotificationPlatform.Security;

public static class TokenUtility
{
    public static string GenerateApiKey()
    {
        Span<byte> bytes = stackalloc byte[32];
        RandomNumberGenerator.Fill(bytes);
        return $"idp_{ToBase64Url(bytes)}";
    }

    public static string GenerateClientId()
    {
        Span<byte> bytes = stackalloc byte[18];
        RandomNumberGenerator.Fill(bytes);
        return $"client_{ToBase64Url(bytes)}";
    }

    public static string GenerateClientSecret()
    {
        Span<byte> bytes = stackalloc byte[32];
        RandomNumberGenerator.Fill(bytes);
        return $"secret_{ToBase64Url(bytes)}";
    }

    public static string GenerateSecureToken()
    {
        Span<byte> bytes = stackalloc byte[32];
        RandomNumberGenerator.Fill(bytes);
        return ToBase64Url(bytes);
    }

    public static string GenerateAuthorizationCode()
    {
        Span<byte> bytes = stackalloc byte[32];
        RandomNumberGenerator.Fill(bytes);
        return $"code_{ToBase64Url(bytes)}";
    }

    public static string GenerateSalt()
    {
        Span<byte> bytes = stackalloc byte[16];
        RandomNumberGenerator.Fill(bytes);
        return Convert.ToBase64String(bytes);
    }

    public static string Sha256(string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(bytes);
    }

    public static string HashToken(string token, string salt)
    {
        return Sha256($"{salt}:{token}");
    }

    public static bool FixedTimeEquals(string firstHex, string secondHex)
    {
        if (firstHex.Length != secondHex.Length)
        {
            return false;
        }

        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(firstHex),
            Encoding.UTF8.GetBytes(secondHex));
    }

    public static string ToBase64Url(ReadOnlySpan<byte> bytes)
    {
        return Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }
}
