using System;
using System.Collections.Generic;

namespace PasswordVault.Models;

public class VaultEntry
{
    public string Name { get; set; } = string.Empty; // e.g., "github"
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty; // stored encrypted inside vault payload; plaintext only in-memory
    public string Notes { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
