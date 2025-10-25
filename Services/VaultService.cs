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
        File.WriteAllBytes(_vaultPath, raw);
    }
}
