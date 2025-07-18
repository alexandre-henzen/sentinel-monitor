using System.Runtime.InteropServices;
using System.Text;

namespace EAM.Agent.Helpers;

public static class Win32Helper
{
    [DllImport("user32.dll")]
    public static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    public static extern int GetWindowText(IntPtr hWnd, StringBuilder text, int count);

    [DllImport("user32.dll")]
    public static extern int GetClassName(IntPtr hWnd, StringBuilder className, int count);

    [DllImport("user32.dll")]
    public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

    [DllImport("user32.dll")]
    public static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

    [DllImport("user32.dll")]
    public static extern bool IsWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    public static extern bool IsWindowVisible(IntPtr hWnd);

    [DllImport("user32.dll")]
    public static extern IntPtr FindWindow(string? lpClassName, string? lpWindowName);

    [DllImport("user32.dll")]
    public static extern IntPtr FindWindowEx(IntPtr hwndParent, IntPtr hwndChildAfter, string? lpszClass, string? lpszWindow);

    [DllImport("user32.dll")]
    public static extern int SendMessage(IntPtr hWnd, int Msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    public static extern bool EnumWindows(EnumWindowsProc enumProc, IntPtr lParam);

    [DllImport("user32.dll")]
    public static extern bool EnumChildWindows(IntPtr hWndParent, EnumWindowsProc lpEnumFunc, IntPtr lParam);

    [DllImport("user32.dll")]
    public static extern bool GetWindowPlacement(IntPtr hWnd, ref WINDOWPLACEMENT lpwndpl);

    [DllImport("kernel32.dll")]
    public static extern IntPtr OpenProcess(uint processAccess, bool bInheritHandle, uint processId);

    [DllImport("kernel32.dll")]
    public static extern bool CloseHandle(IntPtr hObject);

    [DllImport("psapi.dll")]
    public static extern uint GetModuleFileNameEx(IntPtr hProcess, IntPtr hModule, StringBuilder lpBaseName, uint nSize);

    // Estruturas
    [StructLayout(LayoutKind.Sequential)]
    public struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct POINT
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct WINDOWPLACEMENT
    {
        public int Length;
        public int Flags;
        public int ShowCmd;
        public POINT MinPosition;
        public POINT MaxPosition;
        public RECT NormalPosition;
    }

    // Delegates
    public delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    // Constantes
    public const int WM_GETTEXT = 0x000D;
    public const int WM_GETTEXTLENGTH = 0x000E;
    public const uint PROCESS_QUERY_INFORMATION = 0x0400;
    public const uint PROCESS_VM_READ = 0x0010;

    // Métodos auxiliares
    public static string GetWindowText(IntPtr hWnd)
    {
        var length = SendMessage(hWnd, WM_GETTEXTLENGTH, IntPtr.Zero, IntPtr.Zero);
        if (length <= 0) return string.Empty;

        var builder = new StringBuilder(length + 1);
        GetWindowText(hWnd, builder, builder.Capacity);
        return builder.ToString();
    }

    public static string GetClassName(IntPtr hWnd)
    {
        var builder = new StringBuilder(256);
        GetClassName(hWnd, builder, builder.Capacity);
        return builder.ToString();
    }

    public static uint GetWindowProcessId(IntPtr hWnd)
    {
        GetWindowThreadProcessId(hWnd, out uint processId);
        return processId;
    }

    public static bool IsValidWindow(IntPtr hWnd)
    {
        return hWnd != IntPtr.Zero && IsWindow(hWnd) && IsWindowVisible(hWnd);
    }

    public static List<IntPtr> GetAllWindows()
    {
        var windows = new List<IntPtr>();
        EnumWindows((hWnd, lParam) =>
        {
            if (IsValidWindow(hWnd))
                windows.Add(hWnd);
            return true;
        }, IntPtr.Zero);
        return windows;
    }

    public static List<IntPtr> GetWindowsByProcessName(string processName)
    {
        var windows = new List<IntPtr>();
        EnumWindows((hWnd, lParam) =>
        {
            if (IsValidWindow(hWnd))
            {
                var processId = GetWindowProcessId(hWnd);
                try
                {
                    using var process = System.Diagnostics.Process.GetProcessById((int)processId);
                    if (string.Equals(process.ProcessName, processName, StringComparison.OrdinalIgnoreCase))
                        windows.Add(hWnd);
                }
                catch
                {
                    // Processo pode ter terminado
                }
            }
            return true;
        }, IntPtr.Zero);
        return windows;
    }
}