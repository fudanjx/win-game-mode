using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace GameModeApp
{
    public class KeyboardHook
    {
        private const int WH_KEYBOARD_LL = 13;
        private const int WM_KEYDOWN = 0x0100;
        private const int WM_KEYUP = 0x0101;
        private const int WM_SYSKEYDOWN = 0x0104;
        private const int WM_SYSKEYUP = 0x0105;

        // Win keys virtual key codes
        private const int VK_LWIN = 0x5B;
        private const int VK_RWIN = 0x5C;

        private LowLevelKeyboardProc _proc;
        private IntPtr _hookID = IntPtr.Zero;
        public bool IsHookEnabled { get; set; }
        
        public event EventHandler<KeyEventArgs>? KeyBlocked;

        public KeyboardHook()
        {
            _proc = HookCallback;
        }

        public void Install()
        {
            _hookID = SetHook(_proc);
            IsHookEnabled = true;
        }

        public void Uninstall()
        {
            if (_hookID != IntPtr.Zero)
            {
                UnhookWindowsHookEx(_hookID);
                _hookID = IntPtr.Zero;
            }
            IsHookEnabled = false;
        }

        private IntPtr SetHook(LowLevelKeyboardProc proc)
        {
            using (Process curProcess = Process.GetCurrentProcess())
            using (ProcessModule? curModule = curProcess.MainModule)
            {
                return SetWindowsHookEx(WH_KEYBOARD_LL, proc,
                    GetModuleHandle(curModule?.ModuleName), 0);
            }
        }

        private delegate IntPtr LowLevelKeyboardProc(
            int nCode, IntPtr wParam, IntPtr lParam);

        private IntPtr HookCallback(
            int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0 && IsHookEnabled)
            {
                int messageType = wParam.ToInt32();
                KBDLLHOOKSTRUCT keyInfo = Marshal.PtrToStructure<KBDLLHOOKSTRUCT>(lParam);
                int vkCode = keyInfo.vkCode;
                
                // Always block any Windows key events (down, up, etc.)
                if (vkCode == VK_LWIN || vkCode == VK_RWIN)
                {
                    KeyBlocked?.Invoke(this, new KeyEventArgs((Keys)vkCode));
                    return (IntPtr)1; // Block the key
                }
                
                // Block certain Win key combinations (like Win+Tab, Win+D, etc.)
                if ((Control.ModifierKeys & Keys.LWin) == Keys.LWin || 
                    (Control.ModifierKeys & Keys.RWin) == Keys.RWin)
                {
                    // Block common Win key combinations
                    if (vkCode == (int)Keys.Tab || // Win+Tab (Task View)
                        vkCode == (int)Keys.D ||   // Win+D (Show Desktop)
                        vkCode == (int)Keys.E ||   // Win+E (File Explorer)
                        vkCode == (int)Keys.R ||   // Win+R (Run dialog)
                        vkCode == (int)Keys.S ||   // Win+S (Search)
                        vkCode == (int)Keys.X)     // Win+X (Power User Menu)
                    {
                        KeyBlocked?.Invoke(this, new KeyEventArgs((Keys)vkCode));
                        return (IntPtr)1; // Block the key
                    }
                }
            }

            return CallNextHookEx(_hookID, nCode, wParam, lParam);
        }

        #region DLL Imports
        
        [StructLayout(LayoutKind.Sequential)]
        private struct KBDLLHOOKSTRUCT
        {
            public int vkCode;
            public int scanCode;
            public int flags;
            public int time;
            public IntPtr dwExtraInfo;
        }

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook,
            LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode,
            IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string? lpModuleName);

        #endregion
    }
}