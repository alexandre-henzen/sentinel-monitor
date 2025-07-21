using System.Runtime.InteropServices;
using System.Text;

namespace EAM.Agent.Core.Windows;

/// <summary>
/// Definições de P/Invoke para APIs do Windows
/// </summary>
public static class WindowsApi
{
    #region Constants
    
    public const int WM_GETTEXT = 0x000D;
    public const int WM_GETTEXTLENGTH = 0x000E;
    public const int SW_HIDE = 0;
    public const int SW_MAXIMIZE = 3;
    public const int SW_MINIMIZE = 6;
    public const int SW_RESTORE = 9;
    public const int SW_SHOW = 5;
    public const int SW_SHOWDEFAULT = 10;
    public const int SW_SHOWMAXIMIZED = 3;
    public const int SW_SHOWMINIMIZED = 2;
    public const int SW_SHOWMINNOACTIVE = 7;
    public const int SW_SHOWNA = 8;
    public const int SW_SHOWNOACTIVATE = 4;
    public const int SW_SHOWNORMAL = 1;
    
    #endregion

    #region Structures
    
    [StructLayout(LayoutKind.Sequential)]
    public struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
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

    [StructLayout(LayoutKind.Sequential)]
    public struct POINT
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct WINDOWINFO
    {
        public uint cbSize;
        public RECT rcWindow;
        public RECT rcClient;
        public uint dwStyle;
        public uint dwExStyle;
        public uint dwWindowStatus;
        public uint cxWindowBorders;
        public uint cyWindowBorders;
        public ushort atomWindowType;
        public ushort wCreatorVersion;
    }

    #endregion

    #region User32.dll Imports
    
    /// <summary>
    /// Obtém o handle da janela em primeiro plano
    /// </summary>
    [DllImport("user32.dll")]
    public static extern IntPtr GetForegroundWindow();

    /// <summary>
    /// Obtém o título da janela
    /// </summary>
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    public static extern int GetWindowText(IntPtr hWnd, StringBuilder text, int count);

    /// <summary>
    /// Obtém o comprimento do título da janela
    /// </summary>
    [DllImport("user32.dll")]
    public static extern int GetWindowTextLength(IntPtr hWnd);

    /// <summary>
    /// Obtém a classe da janela
    /// </summary>
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    public static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

    /// <summary>
    /// Obtém o retângulo da janela
    /// </summary>
    [DllImport("user32.dll")]
    public static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

    /// <summary>
    /// Obtém o placement da janela
    /// </summary>
    [DllImport("user32.dll")]
    public static extern bool GetWindowPlacement(IntPtr hWnd, ref WINDOWPLACEMENT lpwndpl);

    /// <summary>
    /// Obtém o ID do processo que possui a janela
    /// </summary>
    [DllImport("user32.dll")]
    public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    /// <summary>
    /// Verifica se a janela está visível
    /// </summary>
    [DllImport("user32.dll")]
    public static extern bool IsWindowVisible(IntPtr hWnd);

    /// <summary>
    /// Verifica se a janela está minimizada
    /// </summary>
    [DllImport("user32.dll")]
    public static extern bool IsIconic(IntPtr hWnd);

    /// <summary>
    /// Verifica se a janela está maximizada
    /// </summary>
    [DllImport("user32.dll")]
    public static extern bool IsZoomed(IntPtr hWnd);

    /// <summary>
    /// Obtém informações da janela
    /// </summary>
    [DllImport("user32.dll")]
    public static extern bool GetWindowInfo(IntPtr hWnd, ref WINDOWINFO pwi);

    /// <summary>
    /// Envia mensagem para a janela
    /// </summary>
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    public static extern IntPtr SendMessage(IntPtr hWnd, int Msg, IntPtr wParam, IntPtr lParam);

    /// <summary>
    /// Enumera todas as janelas
    /// </summary>
    [DllImport("user32.dll")]
    public static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

    /// <summary>
    /// Delegate para enumeração de janelas
    /// </summary>
    public delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    /// <summary>
    /// Encontra janela por classe e título
    /// </summary>
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    public static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

    /// <summary>
    /// Encontra janela filha
    /// </summary>
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    public static extern IntPtr FindWindowEx(IntPtr hwndParent, IntPtr hwndChildAfter, string lpszClass, string lpszWindow);

    #endregion

    #region Kernel32.dll Imports

    /// <summary>
    /// Obtém o handle do módulo por ID do processo
    /// </summary>
    [DllImport("kernel32.dll")]
    public static extern IntPtr OpenProcess(uint dwDesiredAccess, bool bInheritHandle, uint dwProcessId);

    /// <summary>
    /// Obtém o nome do módulo
    /// </summary>
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    public static extern uint GetModuleFileName(IntPtr hModule, StringBuilder lpFilename, uint nSize);

    /// <summary>
    /// Fecha handle
    /// </summary>
    [DllImport("kernel32.dll")]
    public static extern bool CloseHandle(IntPtr hObject);

    /// <summary>
    /// Obtém o tempo do sistema
    /// </summary>
    [DllImport("kernel32.dll")]
    public static extern uint GetTickCount();

    #endregion

    #region Psapi.dll Imports

    /// <summary>
    /// Obtém o nome base do módulo
    /// </summary>
    [DllImport("psapi.dll", CharSet = CharSet.Unicode)]
    public static extern uint GetModuleBaseName(IntPtr hProcess, IntPtr hModule, StringBuilder lpBaseName, uint nSize);

    /// <summary>
    /// Obtém o nome do arquivo do módulo
    /// </summary>
    [DllImport("psapi.dll", CharSet = CharSet.Unicode)]
    public static extern uint GetModuleFileNameEx(IntPtr hProcess, IntPtr hModule, StringBuilder lpFilename, uint nSize);

    #endregion

    #region GDI32.dll Imports

    /// <summary>
    /// Cria um contexto de dispositivo compatível
    /// </summary>
    [DllImport("gdi32.dll")]
    public static extern IntPtr CreateCompatibleDC(IntPtr hdc);

    /// <summary>
    /// Cria um bitmap compatível
    /// </summary>
    [DllImport("gdi32.dll")]
    public static extern IntPtr CreateCompatibleBitmap(IntPtr hdc, int nWidth, int nHeight);

    /// <summary>
    /// Seleciona um objeto no contexto de dispositivo
    /// </summary>
    [DllImport("gdi32.dll")]
    public static extern IntPtr SelectObject(IntPtr hdc, IntPtr hgdiobj);

    /// <summary>
    /// Copia bits de um contexto para outro
    /// </summary>
    [DllImport("gdi32.dll")]
    public static extern bool BitBlt(IntPtr hdcDest, int nXDest, int nYDest, int nWidth, int nHeight, IntPtr hdcSrc, int nXSrc, int nYSrc, uint dwRop);

    /// <summary>
    /// Deleta um contexto de dispositivo
    /// </summary>
    [DllImport("gdi32.dll")]
    public static extern bool DeleteDC(IntPtr hdc);

    /// <summary>
    /// Deleta um objeto GDI
    /// </summary>
    [DllImport("gdi32.dll")]
    public static extern bool DeleteObject(IntPtr hObject);

    /// <summary>
    /// Obtém contexto de dispositivo da janela
    /// </summary>
    [DllImport("user32.dll")]
    public static extern IntPtr GetWindowDC(IntPtr hWnd);

    /// <summary>
    /// Obtém contexto de dispositivo da tela
    /// </summary>
    [DllImport("user32.dll")]
    public static extern IntPtr GetDC(IntPtr hWnd);

    /// <summary>
    /// Libera contexto de dispositivo
    /// </summary>
    [DllImport("user32.dll")]
    public static extern int ReleaseDC(IntPtr hWnd, IntPtr hdc);

    #endregion

    #region Helper Methods

    /// <summary>
    /// Obtém o título da janela de forma segura
    /// </summary>
    public static string GetWindowTitle(IntPtr hWnd)
    {
        try
        {
            int length = GetWindowTextLength(hWnd);
            if (length == 0) return string.Empty;

            var builder = new StringBuilder(length + 1);
            GetWindowText(hWnd, builder, builder.Capacity);
            return builder.ToString();
        }
        catch
        {
            return string.Empty;
        }
    }

    /// <summary>
    /// Obtém a classe da janela de forma segura
    /// </summary>
    public static string GetWindowClassName(IntPtr hWnd)
    {
        try
        {
            var builder = new StringBuilder(256);
            GetClassName(hWnd, builder, builder.Capacity);
            return builder.ToString();
        }
        catch
        {
            return string.Empty;
        }
    }

    /// <summary>
    /// Obtém o nome do processo de forma segura
    /// </summary>
    public static string GetProcessName(uint processId)
    {
        try
        {
            var processHandle = OpenProcess(0x0400 | 0x0010, false, processId);
            if (processHandle == IntPtr.Zero) return string.Empty;

            var builder = new StringBuilder(1024);
            GetModuleBaseName(processHandle, IntPtr.Zero, builder, (uint)builder.Capacity);
            CloseHandle(processHandle);
            
            return builder.ToString();
        }
        catch
        {
            return string.Empty;
        }
    }

    /// <summary>
    /// Obtém o caminho do executável de forma segura
    /// </summary>
    public static string GetProcessPath(uint processId)
    {
        try
        {
            var processHandle = OpenProcess(0x0400 | 0x0010, false, processId);
            if (processHandle == IntPtr.Zero) return string.Empty;

            var builder = new StringBuilder(1024);
            GetModuleFileNameEx(processHandle, IntPtr.Zero, builder, (uint)builder.Capacity);
            CloseHandle(processHandle);
            
            return builder.ToString();
        }
        catch
        {
            return string.Empty;
        }
    }

    #endregion
}