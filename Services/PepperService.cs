using System;

namespace PasswordVault.Services;

public static class PepperService
{
    // Placeholder: returns null (no pepper). Will integrate with OS keystore in a future PR.
    public static byte[]? TryGetPepperOrNull()
    {
        return null;
    }
}
