using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace GameModeApp
{
    public enum KeyMappingProfile
    {
        Disabled,
        CSGO,
        OW
    }

    public class MouseHook
    {
        private const int WH_MOUSE_LL = 14;
        private const int WM_LBUTTONDOWN = 0x0201;
        private const int WM_LBUTTONUP = 0x0202;
        private const int WM_RBUTTONDOWN = 0x0204;
        private const int WM_RBUTTONUP = 0x0205;
        private const int WM_XBUTTONDOWN = 0x020B;
        private const int WM_XBUTTONUP = 0x020C;
        private const int WM_MOUSEMOVE = 0x0200;

        // Special constants for Razer DeathAdder V3 Pro
        private const int RAZER_SIDE_BUTTON_MASK = 0x20000;
        private const int RAZER_FORWARD_BUTTON_MASK = 0x10000;

        // Virtual key codes for keyboard simulation
        private const byte VK_1 = 0x31;
        private const byte VK_2 = 0x32;
        private const byte VK_SHIFT = 0xA0;

        private LowLevelMouseProc _proc;
        private IntPtr _hookID = IntPtr.Zero;
        public bool IsHookEnabled { get; set; }
        public KeyMappingProfile CurrentProfile { get; set; } = KeyMappingProfile.Disabled;

        public event EventHandler<string>? MouseButtonDetected;

        public MouseHook()
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

        private IntPtr SetHook(LowLevelMouseProc proc)
        {
            using (Process curProcess = Process.GetCurrentProcess())
            using (ProcessModule? curModule = curProcess.MainModule)
            {
                return SetWindowsHookEx(WH_MOUSE_LL, proc,
                    GetModuleHandle(curModule?.ModuleName), 0);
            }
        }

        private delegate IntPtr LowLevelMouseProc(int nCode, IntPtr wParam, IntPtr lParam);

        private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0 && IsHookEnabled && CurrentProfile != KeyMappingProfile.Disabled)
            {
                int messageType = wParam.ToInt32();

                // XButton is used for the mouse side buttons (XButton1 and XButton2)
                if (messageType == WM_XBUTTONDOWN)
                {
                    MSLLHOOKSTRUCT mouseInfo = Marshal.PtrToStructure<MSLLHOOKSTRUCT>(lParam);
                    int mouseData = (int)mouseInfo.mouseData;
                    
                    // Standard side buttons (XBUTTON1 = 0x0001, XBUTTON2 = 0x0002)
                    bool isXButton1 = (mouseData >> 16) == 1;
                    bool isXButton2 = (mouseData >> 16) == 2;
                    
                    // Detect Razer DeathAdder V3 Pro side buttons
                    bool isRazerSideButton = (mouseData & RAZER_SIDE_BUTTON_MASK) != 0;
                    bool isRazerForwardButton = (mouseData & RAZER_FORWARD_BUTTON_MASK) != 0;

                    if (isXButton1 || isXButton2 || isRazerSideButton || isRazerForwardButton)
                    {
                        string buttonId = isXButton1 ? "XButton1" : 
                                        isXButton2 ? "XButton2" : 
                                        isRazerSideButton ? "RazerSideButton" : "RazerForwardButton";
                        
                        MouseButtonDetected?.Invoke(this, $"Detected: {buttonId}, Data: 0x{mouseData:X}");
                        
                        // Handle button press based on active profile
                        return HandleButtonPress(buttonId);
                    }
                }
            }
            
            // Call the next hook
            return CallNextHookEx(_hookID, nCode, wParam, lParam);
        }

        private IntPtr HandleButtonPress(string buttonId)
        {
            switch (CurrentProfile)
            {
                case KeyMappingProfile.CSGO:
                    // Send keyboard strokes: "2" then "1" with 1ms delay
                    SendKeyPress(VK_2);
                    Thread.Sleep(1);
                    SendKeyPress(VK_1);
                    return (IntPtr)1; // Handled
                    
                case KeyMappingProfile.OW:
                    // Send left shift keyboard stroke
                    SendKeyPress(VK_SHIFT);
                    return (IntPtr)1; // Handled
                    
                default:
                    return (IntPtr)0; // Not handled
            }
        }

        private void SendKeyPress(byte keyCode)
        {
            // Create input structure
            INPUT[] inputs = new INPUT[2];
            
            // Key down event
            inputs[0] = new INPUT
            {
                type = INPUT_KEYBOARD,
                u = new INPUT_UNION
                {
                    ki = new KEYBDINPUT
                    {
                        wVk = keyCode,
                        wScan = 0,
                        dwFlags = 0,
                        time = 0,
                        dwExtraInfo = IntPtr.Zero
                    }
                }
            };
            
            // Key up event
            inputs[1] = new INPUT
            {
                type = INPUT_KEYBOARD,
                u = new INPUT_UNION
                {
                    ki = new KEYBDINPUT
                    {
                        wVk = keyCode,
                        wScan = 0,
                        dwFlags = KEYEVENTF_KEYUP,
                        time = 0,
                        dwExtraInfo = IntPtr.Zero
                    }
                }
            };
            
            // Send the keypress
            SendInput(2, inputs, Marshal.SizeOf(typeof(INPUT)));
        }

        #region DLL Imports and Structures

        [StructLayout(LayoutKind.Sequential)]
        private struct MSLLHOOKSTRUCT
        {
            public POINT pt;
            public int mouseData;
            public int flags;
            public int time;
            public IntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int x;
            public int y;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct INPUT
        {
            public int type;
            public INPUT_UNION u;
        }

        [StructLayout(LayoutKind.Explicit)]
        private struct INPUT_UNION
        {
            [FieldOffset(0)]
            public MOUSEINPUT mi;
            [FieldOffset(0)]
            public KEYBDINPUT ki;
            [FieldOffset(0)]
            public HARDWAREINPUT hi;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct KEYBDINPUT
        {
            public ushort wVk;
            public ushort wScan;
            public uint dwFlags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MOUSEINPUT
        {
            public int dx;
            public int dy;
            public int mouseData;
            public uint dwFlags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct HARDWAREINPUT
        {
            public uint uMsg;
            public ushort wParamL;
            public ushort wParamH;
        }

        private const int INPUT_KEYBOARD = 1;
        private const int KEYEVENTF_KEYUP = 0x0002;

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelMouseProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string? lpModuleName);
        
        [DllImport("user32.dll")]
        private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

        #endregion
    }
}