using System;

namespace PasswordVault.Models;

public sealed class VaultFileV2
{
    public int FormatVersion { get; set; } = 2;
    public CipherSpec CipherSpec { get; set; } = new() { Alg = "aes-256-gcm", TagBytes = 16 };
    public KdfSpec Kdf { get; set; } = new();
    public byte[] Salt { get; set; } = Array.Empty<byte>();
    public byte[] Nonce { get; set; } = Array.Empty<byte>();
    public byte[] Ciphertext { get; set; } = Array.Empty<byte>();
    public byte[] Tag { get; set; } = Array.Empty<byte>();
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public byte[] HeaderAadHash { get; set; } = Array.Empty<byte>();
}

public sealed class CipherSpec
{
    public string Alg { get; set; } = "aes-256-gcm";
    public int TagBytes { get; set; } = 16;
}

public sealed class KdfSpec
{
    public string Type { get; set; } = "argon2id"; // or "pbkdf2"
    public Argon2Params? Argon2 { get; set; } = new();
    public Pbkdf2Params? Pbkdf2 { get; set; }
}

public sealed class Argon2Params
{
    public int MemoryMB { get; set; } = 128;
    public int Iterations { get; set; } = 3;
    public int Parallelism { get; set; } = Math.Max(2, Environment.ProcessorCount / 2);
}

public sealed class Pbkdf2Params
{
    public int Iterations { get; set; } = 600_000;
    public string Hash { get; set; } = "sha256";
}
