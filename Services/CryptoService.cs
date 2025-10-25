using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using PasswordVault.Models;

namespace PasswordVault.Services;

public static class CryptoService
{
    // Legacy PBKDF2 helper (v1)
    public static byte[] DeriveKey(string password, byte[] salt, int iterations, int keyBytes = 32)
    {
        using var pbkdf2 = new Rfc2898DeriveBytes(password, salt, iterations, HashAlgorithmName.SHA256);
        return pbkdf2.GetBytes(keyBytes);
    }

    // v2: KDF selection with optional pepper + HKDF finalization
    public static byte[] DeriveKey(KdfSpec spec, string masterPassword, byte[] salt, byte[]? pepper = null, int keyBytes = 32)
    {
        byte[] baseKey;
        if (string.Equals(spec.Type, "argon2id", StringComparison.OrdinalIgnoreCase))
        {
            // Argon2id package not yet included; will be added in a later PR.
            throw new NotSupportedException("Argon2id KDF not available in this build.");
        }
        else // pbkdf2
        {
            var p = spec.Pbkdf2 ?? new Pbkdf2Params();
            var algo = HashAlgorithmName.SHA256; // only sha256 supported for now
            using var pbkdf2 = new Rfc2898DeriveBytes(masterPassword, salt, Math.Max(100_000, p.Iterations), algo);
            baseKey = pbkdf2.GetBytes(keyBytes);
        }

        if (pepper is not null && pepper.Length > 0)
        {
            var prk = HkdfExtract(pepper, baseKey);
            return HkdfExpand(prk, Encoding.UTF8.GetBytes("vault-enc-key"), keyBytes);
        }
        return baseKey;
    }

    // AES-GCM (legacy convenience, keeps compatibility with v1 callers)
    public static (byte[] ciphertext, byte[] nonce, byte[] tag) Encrypt(byte[] key, byte[] plaintext, byte[]? associatedData = null)
    {
        byte[] nonce = RandomNumberGenerator.GetBytes(12); // 96-bit nonce
        byte[] ciphertext = new byte[plaintext.Length];
        byte[] tag = new byte[16];

        using var aesGcm = new AesGcm(key, 16);
        aesGcm.Encrypt(nonce, plaintext, ciphertext, tag, associatedData);
        return (ciphertext, nonce, tag);
    }

    public static byte[] Decrypt(byte[] key, byte[] nonce, byte[] ciphertext, byte[] tag, byte[]? associatedData = null)
    {
        byte[] plaintext = new byte[ciphertext.Length];
        using var aesGcm = new AesGcm(key, 16);
        aesGcm.Decrypt(nonce, ciphertext, tag, plaintext, associatedData);
        return plaintext;
    }

    // v2 AEAD with explicit AAD and sizes
    public static (byte[] ciphertext, byte[] nonce, byte[] tag) EncryptAead(byte[] key, byte[] plaintext, byte[] aad, int nonceBytes = 12, int tagBytes = 16)
    {
        byte[] nonce = RandomNumberGenerator.GetBytes(nonceBytes);
        byte[] ciphertext = new byte[plaintext.Length];
        byte[] tag = new byte[tagBytes];
        using var aesGcm = new AesGcm(key, tagBytes);
        aesGcm.Encrypt(nonce, plaintext, ciphertext, tag, aad);
        return (ciphertext, nonce, tag);
    }

    public static byte[] DecryptAead(byte[] key, byte[] nonce, byte[] ciphertext, byte[] tag, byte[] aad, int tagBytes = 16)
    {
        byte[] plaintext = new byte[ciphertext.Length];
        using var aesGcm = new AesGcm(key, tagBytes);
        aesGcm.Decrypt(nonce, ciphertext, tag, plaintext, aad);
        return plaintext;
    }

    // HKDF (HMAC-SHA256)
    public static byte[] HkdfExtract(byte[] saltOrPepper, byte[] ikm)
    {
        using var hmac = new HMACSHA256(saltOrPepper);
        return hmac.ComputeHash(ikm);
    }

    public static byte[] HkdfExpand(byte[] prk, byte[] info, int length)
    {
        using var hmac = new HMACSHA256(prk);
        int hashLen = hmac.HashSize / 8;
        int n = (int)Math.Ceiling((double)length / hashLen);
        byte[] okm = new byte[length];
        byte[] t = Array.Empty<byte>();
        int offset = 0;
        for (int i = 1; i <= n; i++)
        {
            hmac.Initialize();
            hmac.TransformBlock(t, 0, t.Length, null, 0);
            hmac.TransformBlock(info, 0, info.Length, null, 0);
            var c = new[] { (byte)i };
            hmac.TransformFinalBlock(c, 0, 1);
            t = hmac.Hash!;
            Array.Copy(t, 0, okm, offset, Math.Min(hashLen, length - offset));
            offset += hashLen;
        }
        return okm;
    }
}
