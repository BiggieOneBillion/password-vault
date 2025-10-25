using System;
using System.IO;
using System.Security.AccessControl;
using System.Security.Cryptography;

namespace PasswordVault.Services;

public static class FileSecurityHelper
{
    public static void SetPrivateFilePermissions(string path)
    {
#if WINDOWS
        var fileInfo = new FileInfo(path);
        var security = fileInfo.GetAccessControl();
        security.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);
        var sid = System.Security.Principal.WindowsIdentity.GetCurrent().User!;
        var rule = new FileSystemAccessRule(sid, FileSystemRights.FullControl, AccessControlType.Allow);
        security.SetOwner(sid);
        security.ResetAccessRule(rule);
        fileInfo.SetAccessControl(security);
#else
        // Unix-like: 600
        System.IO.File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
#endif
    }

    public static void AtomicWriteAllBytes(string path, byte[] bytes)
    {
        // Console.WriteLine($"Path: {path}, {string.Join(", ", bytes)}");

        var dir = Path.GetDirectoryName($"./{path}")!;
        // Console.WriteLine($"Path: {path}, Dir:{dir}");
        Directory.CreateDirectory(dir);
        var tmp = Path.Combine(dir, $".{Path.GetFileName(path)}.tmp-{Guid.NewGuid():N}");
        using (var fs = new FileStream(tmp, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
        {
            fs.Write(bytes, 0, bytes.Length);
            fs.Flush(true);
        }
        SetPrivateFilePermissions(tmp);
        if (File.Exists(path)) File.Delete(path);
        File.Move(tmp, path);
        SetPrivateFilePermissions(path);
    }
}
