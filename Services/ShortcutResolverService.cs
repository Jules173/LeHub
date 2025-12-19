using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Text;
using Path = System.IO.Path;

namespace LeHub.Services;

public class ShortcutResolverService
{
    private static ShortcutResolverService? _instance;
    public static ShortcutResolverService Instance => _instance ??= new ShortcutResolverService();

    public record ShortcutInfo(string TargetPath, string Arguments, string WorkingDirectory);

    public ShortcutInfo? ResolveShortcut(string lnkPath)
    {
        if (!lnkPath.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase))
            return null;

        try
        {
            var shell = (IShellLinkW)new ShellLink();
            var file = (IPersistFile)shell;

            file.Load(lnkPath, 0);

            var targetPath = new StringBuilder(260);
            shell.GetPath(targetPath, targetPath.Capacity, IntPtr.Zero, 0);

            var arguments = new StringBuilder(1024);
            shell.GetArguments(arguments, arguments.Capacity);

            var workingDir = new StringBuilder(260);
            shell.GetWorkingDirectory(workingDir, workingDir.Capacity);

            var target = targetPath.ToString();
            if (string.IsNullOrWhiteSpace(target))
                return null;

            return new ShortcutInfo(
                target,
                arguments.ToString(),
                workingDir.ToString()
            );
        }
        catch
        {
            return null;
        }
    }

    public string GetAppNameFromPath(string path)
    {
        if (path.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase))
        {
            return Path.GetFileNameWithoutExtension(path);
        }
        return Path.GetFileNameWithoutExtension(path);
    }

    [ComImport]
    [Guid("00021401-0000-0000-C000-000000000046")]
    private class ShellLink { }

    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("000214F9-0000-0000-C000-000000000046")]
    private interface IShellLinkW
    {
        void GetPath([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszFile, int cch, IntPtr pfd, int fFlags);
        void GetIDList(out IntPtr ppidl);
        void SetIDList(IntPtr pidl);
        void GetDescription([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszName, int cch);
        void SetDescription([MarshalAs(UnmanagedType.LPWStr)] string pszName);
        void GetWorkingDirectory([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszDir, int cch);
        void SetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)] string pszDir);
        void GetArguments([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszArgs, int cch);
        void SetArguments([MarshalAs(UnmanagedType.LPWStr)] string pszArgs);
        void GetHotkey(out short pwHotkey);
        void SetHotkey(short wHotkey);
        void GetShowCmd(out int piShowCmd);
        void SetShowCmd(int iShowCmd);
        void GetIconLocation([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszIconPath, int cch, out int piIcon);
        void SetIconLocation([MarshalAs(UnmanagedType.LPWStr)] string pszIconPath, int iIcon);
        void SetRelativePath([MarshalAs(UnmanagedType.LPWStr)] string pszPathRel, int dwReserved);
        void Resolve(IntPtr hwnd, int fFlags);
        void SetPath([MarshalAs(UnmanagedType.LPWStr)] string pszFile);
    }
}
