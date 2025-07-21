using System.Runtime.InteropServices;
using System.Text;

namespace EAM.Agent.Helpers
{
    internal static class NativeMethods
        {
            [DllImport("user32.dll")]
            internal static extern IntPtr GetForegroundWindow();
    
            [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
            internal static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);
    
            [DllImport("user32.dll")]
            internal static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);
        }
    }