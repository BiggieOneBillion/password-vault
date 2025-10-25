using System;
using System.Threading;
using System.Threading.Tasks;
using TextCopy;

namespace PasswordVault.Services;

public static class ClipboardHelper
{
    public static void StartCopyWithAutoClear(string text, TimeSpan clearAfter)
    {
        ClipboardService.SetText(text);
        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(clearAfter);
                // Overwrite then clear
                ClipboardService.SetText("[cleared]");
                await Task.Delay(TimeSpan.FromMilliseconds(200));
                ClipboardService.SetText(string.Empty);
            }
            catch { /* ignore */ }
        });
    }
}
