using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace PasswordVault.Services;

public static class PasswordGenerator
{
    private const string Lower = "abcdefghijklmnopqrstuvwxyz";
    private const string Upper = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
    private const string Digits = "0123456789";
    private const string Symbols = "!@#$%^&*()-_=+[]{};:,.<>/?";

    public static string Generate(int length = 16, bool useUpper = true, bool useDigits = true, bool useSymbols = true)
    {
        if (length < 8) length = 8;
        var pool = Lower + (useUpper ? Upper : string.Empty) + (useDigits ? Digits : string.Empty) + (useSymbols ? Symbols : string.Empty);
        if (string.IsNullOrEmpty(pool)) pool = Lower;

        var bytes = RandomNumberGenerator.GetBytes(length);
        var sb = new StringBuilder(length);
        for (int i = 0; i < length; i++)
        {
            sb.Append(pool[bytes[i] % pool.Length]);
        }
        return sb.ToString();
    }
}
