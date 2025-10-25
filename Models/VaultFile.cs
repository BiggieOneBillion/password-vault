using System;
using System.Collections.Generic;

namespace PasswordVault.Models;

public class VaultFile
{
    // Parameters for key derivation
    public byte[] Salt { get; set; } = Array.Empty<byte>();
    public int Iterations { get; set; } = 210_000; // OWASP 2024 PBKDF2 baseline

    // AEAD nonce for vault content
    public byte[] Nonce { get; set; } = Array.Empty<byte>();

    // Encrypted JSON payload of VaultPayload
    public byte[] Ciphertext { get; set; } = Array.Empty<byte>();

    // Separate tag for AesGcm
    public byte[] Tag { get; set; } = Array.Empty<byte>();
}

public class VaultPayload
{
    public List<VaultEntry> Entries { get; set; } = new();
}
