using System;
using System.Text.RegularExpressions;

namespace PasswordVault.Services;

public static class PasswordStrength
{
    // naive 0..4 scoring based on length and charset variety
    public static int EstimateScore(string password)
    {
        if (string.IsNullOrEmpty(password)) return 0;
        int score = 0;
        if (password.Length >= 12) score++;
        if (password.Length >= 16) score++;
        if (Regex.IsMatch(password, "[a-z]")) score++;
        if (Regex.IsMatch(password, "[A-Z]")) score++;
        if (Regex.IsMatch(password, "[0-9]")) score++;
        if (Regex.IsMatch(password, "[^A-Za-z0-9]")) score++;
        // normalize to 0..4
        return Math.Min(4, Math.Max(0, score - 2));
    }
}
