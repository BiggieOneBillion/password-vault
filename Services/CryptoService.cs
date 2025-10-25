using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace PasswordVault.Services;

public static class CryptoService
{
    public static byte[] DeriveKey(string password, byte[] salt, int iterations, int keyBytes = 32)
    {
        using var pbkdf2 = new Rfc2898DeriveBytes(password, salt, iterations, HashAlgorithmName.SHA256);
        return pbkdf2.GetBytes(keyBytes);
    }

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
}
