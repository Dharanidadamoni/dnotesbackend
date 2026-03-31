using System.Security.Cryptography;
using System.Text;

namespace dnotes_backend.Services;

/// <summary>
/// Server-side AES-256-GCM encryption.
/// Used ONLY for metadata the server needs to process
/// (e.g. specific delivery dates for the trigger service).
///
/// Message BODIES are encrypted on the browser using
/// Web Crypto API — this service never touches them.
/// </summary>
public interface IEncryptionService
{
    string Encrypt(string plainText);
    string Decrypt(string cipherText);
    bool IsEncrypted(string value);
}

public class EncryptionService : IEncryptionService
{
    private const string Prefix = "ENC_V1:";
    private readonly byte[] _key;

    public EncryptionService(IConfiguration config)
    {
        var secret = config["JwtSettings:SecretKey"]
            ?? throw new InvalidOperationException("SecretKey not configured.");

        // Derive a stable 256-bit key from the secret
        using var sha = SHA256.Create();
        _key = sha.ComputeHash(Encoding.UTF8.GetBytes(secret));
    }

    public string Encrypt(string plainText)
    {
        if (string.IsNullOrEmpty(plainText)) return plainText;

        // Generate a random 12-byte nonce for AES-GCM
        var nonce = new byte[12];
        var tag = new byte[16];
        var plainBytes = Encoding.UTF8.GetBytes(plainText);
        var cipher = new byte[plainBytes.Length];

        RandomNumberGenerator.Fill(nonce);

        using var aesGcm = new AesGcm(_key, 16);
        aesGcm.Encrypt(nonce, plainBytes, cipher, tag);

        // Combine: nonce (12) + tag (16) + ciphertext → base64
        var combined = new byte[nonce.Length + tag.Length + cipher.Length];
        Buffer.BlockCopy(nonce, 0, combined, 0, nonce.Length);
        Buffer.BlockCopy(tag, 0, combined, nonce.Length, tag.Length);
        Buffer.BlockCopy(cipher, 0, combined, nonce.Length + tag.Length, cipher.Length);

        return Prefix + Convert.ToBase64String(combined);
    }

    public string Decrypt(string cipherText)
    {
        if (string.IsNullOrEmpty(cipherText)) return cipherText;
        if (!cipherText.StartsWith(Prefix)) return cipherText;

        var combined = Convert.FromBase64String(cipherText[Prefix.Length..]);

        var nonce = combined[..12];
        var tag = combined[12..28];
        var cipher = combined[28..];
        var plain = new byte[cipher.Length];

        using var aesGcm = new AesGcm(_key, 16);
        aesGcm.Decrypt(nonce, cipher, tag, plain);

        return Encoding.UTF8.GetString(plain);
    }

    public bool IsEncrypted(string value) =>
        !string.IsNullOrEmpty(value) && value.StartsWith(Prefix);

}