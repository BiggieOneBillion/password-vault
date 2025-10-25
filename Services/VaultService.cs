using System;
using System.IO;
using System.Text;
using System.Security.Cryptography;
using Newtonsoft.Json;
using PasswordVault.Models;

namespace PasswordVault.Services;

public class VaultService
{
    private readonly string _vaultPath;

    public VaultService(string vaultPath)
    {
        _vaultPath = vaultPath;
    }

    public bool VaultExists() => File.Exists(_vaultPath);

    public void CreateNewVault(string masterPassword)
    {
        var payload = new VaultPayload();
        SaveVault(masterPassword, payload);
    }

    public VaultPayload LoadVault(string masterPassword)
    {
        var raw = File.ReadAllBytes(_vaultPath);

        // Try v2 first
        try
        {
            var v2 = JsonConvert.DeserializeObject<VaultFileV2>(Encoding.UTF8.GetString(raw));
            if (v2 is not null && v2.FormatVersion == 2)
            {
                return LoadVaultV2(masterPassword, v2);
            }
        }
        catch { /* fall back to v1 */ }

        // v1 path
        var vf = JsonConvert.DeserializeObject<VaultFile>(Encoding.UTF8.GetString(raw))
                 ?? throw new InvalidOperationException("Invalid vault file.");

        var key = CryptoService.DeriveKey(masterPassword, vf.Salt, vf.Iterations);
        var plaintext = CryptoService.Decrypt(key, vf.Nonce, vf.Ciphertext, vf.Tag);
        var json = Encoding.UTF8.GetString(plaintext);
        var payload = JsonConvert.DeserializeObject<VaultPayload>(json)
                      ?? throw new InvalidOperationException("Invalid vault payload.");
        return payload;
    }

    public void SaveVault(string masterPassword, VaultPayload payload)
    {
        // Keep v1 save for now (migration PR will switch to v2). Use atomic write and perms.
        var json = JsonConvert.SerializeObject(payload, Formatting.Indented);
        var plaintext = Encoding.UTF8.GetBytes(json);

        var salt = RandomNumberGenerator.GetBytes(16);
        int iterations = 210_000;
        var key = CryptoService.DeriveKey(masterPassword, salt, iterations);
        var (cipher, nonce, tag) = CryptoService.Encrypt(key, plaintext);

        var vf = new VaultFile
        {
            Salt = salt,
            Iterations = iterations,
            Nonce = nonce,
            Ciphertext = cipher,
            Tag = tag,
        };

        var raw = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(vf));
        // Console.WriteLine($"raw: {string.Join(", ", raw)}, vaultpath: {_vaultPath}");
        FileSecurityHelper.AtomicWriteAllBytes(_vaultPath, raw);
    }

    public int GetVaultFormatVersion()
    {
        var raw = File.ReadAllBytes(_vaultPath);
        try
        {
            var v2 = JsonConvert.DeserializeObject<VaultFileV2>(Encoding.UTF8.GetString(raw));
            if (v2 is not null && v2.FormatVersion == 2) return 2;
        }
        catch { }
        return 1;
    }

    // === V2 helpers (not wired as default yet) ===
    private static KdfSpec DefaultPbkdf2Spec() => new()
    {
        Type = "pbkdf2",
        Pbkdf2 = new Pbkdf2Params { Iterations = 600_000, Hash = "sha256" }
    };

    public void SaveVaultV2(string masterPassword, VaultPayload payload, KdfSpec? kdf = null)
    {
        kdf ??= DefaultPbkdf2Spec();
        var json = JsonConvert.SerializeObject(payload, Formatting.None);
        var plaintext = Encoding.UTF8.GetBytes(json);

        var salt = RandomNumberGenerator.GetBytes(16);
        var created = DateTimeOffset.UtcNow;
        var headerForAad = new
        {
            FormatVersion = 2,
            CipherSpec = new CipherSpec { Alg = "aes-256-gcm", TagBytes = 16 },
            Kdf = kdf,
            Salt = salt,
            Nonce = Array.Empty<byte>(),
            CreatedAt = created
        };
        var aad = CanonicalJson.SerializeStable(headerForAad);
        var key = CryptoService.DeriveKey(kdf, masterPassword, salt, pepper: null, keyBytes: 32);
        var (cipher, nonce, tag) = CryptoService.EncryptAead(key, plaintext, aad, 12, 16);

        var finalHeaderForAad = new
        {
            FormatVersion = 2,
            CipherSpec = new CipherSpec { Alg = "aes-256-gcm", TagBytes = 16 },
            Kdf = kdf,
            Salt = salt,
            Nonce = nonce,
            CreatedAt = created
        };
        var finalAad = CanonicalJson.SerializeStable(finalHeaderForAad);
        var aadHash = SHA256.HashData(finalAad);

        var v2 = new VaultFileV2
        {
            FormatVersion = 2,
            CipherSpec = new CipherSpec { Alg = "aes-256-gcm", TagBytes = 16 },
            Kdf = kdf,
            Salt = salt,
            Nonce = nonce,
            Ciphertext = cipher,
            Tag = tag,
            CreatedAt = created,
            HeaderAadHash = aadHash
        };
        var raw = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(v2));
        FileSecurityHelper.AtomicWriteAllBytes(_vaultPath, raw);
    }

    public VaultPayload LoadVaultV2(string masterPassword, VaultFileV2 v2)
    {
        var headerForAad = new
        {
            v2.FormatVersion,
            v2.CipherSpec,
            v2.Kdf,
            v2.Salt,
            v2.Nonce,
            v2.CreatedAt
        };
        var aad = CanonicalJson.SerializeStable(headerForAad);
        var diagHash = SHA256.HashData(aad);
        if (v2.HeaderAadHash is { Length: > 0 } && !diagHash.AsSpan().SequenceEqual(v2.HeaderAadHash))
            throw new InvalidOperationException("Vault header AAD hash mismatch.");

        var key = CryptoService.DeriveKey(v2.Kdf, masterPassword, v2.Salt, pepper: null, keyBytes: 32);
        var plaintext = CryptoService.DecryptAead(key, v2.Nonce, v2.Ciphertext, v2.Tag, aad, v2.CipherSpec.TagBytes);
        var json = Encoding.UTF8.GetString(plaintext);
        var payload = JsonConvert.DeserializeObject<VaultPayload>(json)
                      ?? throw new InvalidOperationException("Invalid vault payload.");
        return payload;
    }
}
