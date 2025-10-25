using PasswordVault.Models;

namespace PasswordVault.Services;

public sealed class SecurityPolicy
{
    public KdfSpec RecommendedKdf { get; init; } = new()
    {
        Type = "pbkdf2",
        Pbkdf2 = new Pbkdf2Params { Iterations = 600_000, Hash = "sha256" }
    };

    public int MinPasswordScore { get; init; } = 2; // simple estimator 0..4
    public int MinPasswordLength { get; init; } = 12;
    public int ClipboardClearSeconds { get; init; } = 30;
    public int IdleAutolockMinutes { get; init; } = 5;
    public bool UsePepper { get; init; } = false; // not active yet

    public static SecurityPolicy Current { get; } = new SecurityPolicy();
}
