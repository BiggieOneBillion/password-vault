using System;
using System.Collections.Generic;
using System.Linq;

namespace PasswordVault.Services;

public static class FuzzySearch
{
    // Returns top matches sorted by score desc (0..1)
    public static List<(string name, double score)> Search(string query, IEnumerable<string> corpus, int maxResults = 10)
    {
        query = query ?? string.Empty;
        var q = query.Trim().ToLowerInvariant();
        var list = new List<(string name, double score)>();
        foreach (var item in corpus)
        {
            var display = item ?? string.Empty;
            var n = display;
            var s = Score(q, n.ToLowerInvariant());
            list.Add((display, s));
        }
        return list
            .OrderByDescending(x => x.score)
            .ThenBy(x => x.name)
            .Take(maxResults)
            .ToList();
    }

    // Normalized fuzzy score using Levenshtein distance and subsequence bonus
    public static double Score(string a, string b)
    {
        if (string.IsNullOrEmpty(a) && string.IsNullOrEmpty(b)) return 1.0;
        if (string.IsNullOrEmpty(a) || string.IsNullOrEmpty(b)) return 0.0;
        int dist = Levenshtein(a, b);
        double baseScore = 1.0 - (double)dist / Math.Max(a.Length, b.Length);
        if (IsSubsequence(a, b)) baseScore += 0.1;
        if (b.Contains(a, StringComparison.Ordinal)) baseScore += 0.1;
        return Math.Clamp(baseScore, 0.0, 1.0);
    }

    private static bool IsSubsequence(string a, string b)
    {
        int i = 0;
        foreach (var ch in b)
        {
            if (i < a.Length && a[i] == ch) i++;
        }
        return i == a.Length;
    }

    private static int Levenshtein(string s, string t)
    {
        int m = s.Length, n = t.Length;
        var dp = new int[m + 1, n + 1];
        for (int i = 0; i <= m; i++) dp[i, 0] = i;
        for (int j = 0; j <= n; j++) dp[0, j] = j;
        for (int i = 1; i <= m; i++)
        {
            for (int j = 1; j <= n; j++)
            {
                int cost = s[i - 1] == t[j - 1] ? 0 : 1;
                dp[i, j] = Math.Min(
                    Math.Min(dp[i - 1, j] + 1, dp[i, j - 1] + 1),
                    dp[i - 1, j - 1] + cost);
            }
        }
        return dp[m, n];
    }
}
