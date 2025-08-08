using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;
using System.Threading;

namespace GameModeApp
{
    public class InputMonitor
    {
        // Constants for hooks
        private const int WH_KEYBOARD_LL = 13;
        private const int WH_MOUSE_LL = 14;
        
        // Keyboard messages
        private const int WM_KEYDOWN = 0x0100;
        private const int WM_KEYUP = 0x0101;
        private const int WM_SYSKEYDOWN = 0x0104;
        private const int WM_SYSKEYUP = 0x0105;
        
        // Mouse messages
        private const int WM_MOUSEMOVE = 0x0200;
        private const int WM_LBUTTONDOWN = 0x0201;
        private const int WM_LBUTTONUP = 0x0202;
        private const int WM_RBUTTONDOWN = 0x0204;
        private const int WM_RBUTTONUP = 0x0205;
        private const int WM_MBUTTONDOWN = 0x0207;
        private const int WM_MBUTTONUP = 0x0208;
        private const int WM_MOUSEWHEEL = 0x020A;
        private const int WM_XBUTTONDOWN = 0x020B;
        private const int WM_XBUTTONUP = 0x020C;
        
        // Mouse X-button values
        private const int XBUTTON1 = 0x0001;
        private const int XBUTTON2 = 0x0002;
        
        // Keyboard hook delegate and handle
        private LowLevelKeyboardProc _keyboardProc;
        private IntPtr _keyboardHookID = IntPtr.Zero;
        
        // Mouse hook delegate and handle
        private LowLevelMouseProc _mouseProc;
        private IntPtr _mouseHookID = IntPtr.Zero;
        
        // Raw input buffer size
        private const int RawInputBufferSize = 40; // Typical raw input packet size in bytes
        
        // Event handlers
        public delegate void KeyboardEventHandler(KeyboardState keyboardState);
        public delegate void MouseEventHandler(MouseState mouseState);
        public delegate void RawInputEventHandler(RawInputData rawData);
        
        public event KeyboardEventHandler? KeyboardEvent;
        public event MouseEventHandler? MouseEvent;
        public event RawInputEventHandler? RawInputEvent;
        
        // Store detected inputs for analysis
        public List<InputEventData> DetectedInputs { get; private set; } = new List<InputEventData>();
        
        // Configuration
        public bool CaptureKeyboard { get; set; } = true;
        public bool CaptureMouse { get; set; } = true;
        public bool CaptureRawInput { get; set; } = true;
        public bool EnableLogging { get; set; } = true;
        
        private Form? _messageWindow; // Window for raw input messages
        
        public InputMonitor()
        {
            _keyboardProc = KeyboardHookCallback;
            _mouseProc = MouseHookCallback;
        }
        
        public void Start()
        {
            // Set up hooks based on configuration
            if (CaptureKeyboard)
                _keyboardHookID = SetKeyboardHook(_keyboardProc);
                
            if (CaptureMouse)
                _mouseHookID = SetMouseHook(_mouseProc);
                
            if (CaptureRawInput)
                StartRawInputCapture();
                
            if (EnableLogging)
                Debug.WriteLine("Input Monitor started");
        }
        
        public void Stop()
        {
            // Unhook everything
            if (_keyboardHookID != IntPtr.Zero)
                UnhookWindowsHookEx(_keyboardHookID);
                
            if (_mouseHookID != IntPtr.Zero)
                UnhookWindowsHookEx(_mouseHookID);
                
            if (_messageWindow != null)
            {
                _messageWindow.Close();
                _messageWindow.Dispose();
                _messageWindow = null;
            }
            
            _keyboardHookID = IntPtr.Zero;
            _mouseHookID = IntPtr.Zero;
            
            if (EnableLogging)
                Debug.WriteLine("Input Monitor stopped");
        }
        
        private void StartRawInputCapture()
        {
            // Create an invisible message window to receive raw input
            _messageWindow = new Form
            {
                ShowInTaskbar = false,
                WindowState = FormWindowState.Minimized,
                FormBorderStyle = FormBorderStyle.None,
                Opacity = 0
            };
            
            // Set up to receive raw input WM_INPUT messages
            _messageWindow.Load += (s, e) => RegisterRawInputDevices(_messageWindow.Handle);
            _messageWindow.FormClosed += (s, e) => UnregisterRawInputDevices();
            
            // Handle raw input messages
            _messageWindow.Shown += (s, e) => {
                RawInputWindow window = new RawInputWindow(this);
                window.AssignHandle(_messageWindow!.Handle);
            };
            
            // Show the form to start receiving messages
            _messageWindow.Show();
        }
        
        private void RegisterRawInputDevices(IntPtr windowHandle)
        {
            RAWINPUTDEVICE[] rid = new RAWINPUTDEVICE[2];
            
            // Register for mouse input
            rid[0].usUsagePage = 0x01;
            rid[0].usUsage = 0x02; // Mouse
            rid[0].dwFlags = 0;
            rid[0].hwndTarget = windowHandle;
            
            // Register for keyboard input
            rid[1].usUsagePage = 0x01;
            rid[1].usUsage = 0x06; // Keyboard
            rid[1].dwFlags = 0;
            rid[1].hwndTarget = windowHandle;
            
            // Register the devices
            if (!RegisterRawInputDevices(rid, (uint)rid.Length, (uint)Marshal.SizeOf(typeof(RAWINPUTDEVICE))))
            {
                int error = Marshal.GetLastWin32Error();
                Debug.WriteLine($"Failed to register raw input devices: Error {error}");
            }
            else
            {
                Debug.WriteLine("Raw input devices registered successfully");
            }
        }
        
        private void UnregisterRawInputDevices()
        {
            // To unregister, set the flags to RIDEV_REMOVE
            RAWINPUTDEVICE[] rid = new RAWINPUTDEVICE[2];
            
            rid[0].usUsagePage = 0x01;
            rid[0].usUsage = 0x02; // Mouse
            rid[0].dwFlags = 0x00000001; // RIDEV_REMOVE
            rid[0].hwndTarget = IntPtr.Zero;
            
            rid[1].usUsagePage = 0x01;
            rid[1].usUsage = 0x06; // Keyboard
            rid[1].dwFlags = 0x00000001; // RIDEV_REMOVE
            rid[1].hwndTarget = IntPtr.Zero;
            
            RegisterRawInputDevices(rid, (uint)rid.Length, (uint)Marshal.SizeOf(typeof(RAWINPUTDEVICE)));
        }
        
        private IntPtr SetKeyboardHook(LowLevelKeyboardProc proc)
        {
            using (Process curProcess = Process.GetCurrentProcess())
            using (ProcessModule? curModule = curProcess.MainModule)
            {
                return SetWindowsHookEx(WH_KEYBOARD_LL, proc,
                    GetModuleHandle(curModule?.ModuleName), 0);
            }
        }
        
        private IntPtr SetMouseHook(LowLevelMouseProc proc)
        {
            using (Process curProcess = Process.GetCurrentProcess())
            using (ProcessModule? curModule = curProcess.MainModule)
            {
                return SetWindowsHookEx(WH_MOUSE_LL, proc,
                    GetModuleHandle(curModule?.ModuleName), 0);
            }
        }
        
        private IntPtr KeyboardHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0)
            {
                int messageType = wParam.ToInt32();
                
                if (messageType == WM_KEYDOWN || messageType == WM_SYSKEYDOWN ||
                    messageType == WM_KEYUP || messageType == WM_SYSKEYUP)
                {
                    // Get keyboard info
                    KBDLLHOOKSTRUCT keyInfo = Marshal.PtrToStructure<KBDLLHOOKSTRUCT>(lParam);
                    
                    // Create keyboard state object
                    KeyboardState state = new KeyboardState
                    {
                        VirtualKeyCode = keyInfo.vkCode,
                        ScanCode = keyInfo.scanCode,
                        Flags = keyInfo.flags,
                        Time = keyInfo.time,
                        ExtraInfo = keyInfo.dwExtraInfo,
                        IsKeyDown = (messageType == WM_KEYDOWN || messageType == WM_SYSKEYDOWN),
                        IsExtendedKey = ((keyInfo.flags & 0x01) != 0),
                        MessageType = messageType
                    };
                    
                    // Add to detected inputs
                    DetectedInputs.Add(new InputEventData
                    {
                        Type = InputType.Keyboard,
                        VirtualKeyCode = keyInfo.vkCode,
                        MessageType = messageType,
                        Flags = keyInfo.flags,
                        ExtraInfo = keyInfo.dwExtraInfo.ToInt64(),
                        TimeStamp = DateTime.Now,
                        RawData = keyInfo.scanCode.ToString("X4")
                    });
                    
                    // Raise event
                    KeyboardEvent?.Invoke(state);
                    
                    // Log if enabled
                    if (EnableLogging)
                    {
                        string keyName = ((Keys)keyInfo.vkCode).ToString();
                        string direction = state.IsKeyDown ? "DOWN" : "UP";
                        Debug.WriteLine($"Key {direction}: {keyName} (VK:{keyInfo.vkCode:X}, SC:{keyInfo.scanCode:X}, Flags:{keyInfo.flags:X}, ExtraInfo:{keyInfo.dwExtraInfo.ToInt64():X})");
                    }
                }
            }
            
            // Call the next hook
            return CallNextHookEx(_keyboardHookID, nCode, wParam, lParam);
        }
        
        private IntPtr MouseHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0)
            {
                int messageType = wParam.ToInt32();
                
                if (messageType == WM_LBUTTONDOWN || messageType == WM_RBUTTONDOWN || 
                    messageType == WM_MBUTTONDOWN || messageType == WM_XBUTTONDOWN ||
                    messageType == WM_LBUTTONUP || messageType == WM_RBUTTONUP ||
                    messageType == WM_MBUTTONUP || messageType == WM_XBUTTONUP ||
                    messageType == WM_MOUSEWHEEL)
                {
                    // Get mouse info
                    MSLLHOOKSTRUCT mouseInfo = Marshal.PtrToStructure<MSLLHOOKSTRUCT>(lParam);
                    
                    // Determine which button was pressed
                    string buttonName = "Unknown";
                    int xButton = 0;
                    
                    switch (messageType)
                    {
                        case WM_LBUTTONDOWN:
                        case WM_LBUTTONUP:
                            buttonName = "Left";
                            break;
                        case WM_RBUTTONDOWN:
                        case WM_RBUTTONUP:
                            buttonName = "Right";
                            break;
                        case WM_MBUTTONDOWN:
                        case WM_MBUTTONUP:
                            buttonName = "Middle";
                            break;
                        case WM_XBUTTONDOWN:
                        case WM_XBUTTONUP:
                            xButton = (int)((mouseInfo.mouseData >> 16) & 0xFFFF);
                            buttonName = (xButton == XBUTTON1) ? "X1" : ((xButton == XBUTTON2) ? "X2" : $"X{xButton}");
                            break;
                        case WM_MOUSEWHEEL:
                            short wheelDelta = (short)((mouseInfo.mouseData >> 16) & 0xFFFF);
                            buttonName = (wheelDelta > 0) ? "WheelUp" : "WheelDown";
                            break;
                    }
                    
                    // Create mouse state object
                    MouseState state = new MouseState
                    {
                        X = mouseInfo.pt.x,
                        Y = mouseInfo.pt.y,
                        ButtonPressed = buttonName,
                        XButton = xButton,
                        Data = mouseInfo.mouseData,
                        Flags = mouseInfo.flags,
                        Time = mouseInfo.time,
                        ExtraInfo = mouseInfo.dwExtraInfo,
                        IsButtonDown = (messageType == WM_LBUTTONDOWN || messageType == WM_RBUTTONDOWN ||
                                       messageType == WM_MBUTTONDOWN || messageType == WM_XBUTTONDOWN),
                        MessageType = messageType
                    };
                    
                    // Add to detected inputs
                    DetectedInputs.Add(new InputEventData
                    {
                        Type = InputType.Mouse,
                        Button = buttonName,
                        XButton = xButton,
                        MessageType = messageType,
                        Flags = mouseInfo.flags,
                        MouseData = mouseInfo.mouseData,
                        ExtraInfo = mouseInfo.dwExtraInfo.ToInt64(),
                        TimeStamp = DateTime.Now,
                        RawData = $"{mouseInfo.mouseData:X8}"
                    });
                    
                    // Raise event
                    MouseEvent?.Invoke(state);
                    
                    // Log if enabled
                    if (EnableLogging)
                    {
                        string direction = state.IsButtonDown ? "DOWN" : "UP";
                        Debug.WriteLine($"Mouse {buttonName} {direction}: (Pos:{mouseInfo.pt.x},{mouseInfo.pt.y}, Data:{mouseInfo.mouseData:X}, Flags:{mouseInfo.flags:X}, ExtraInfo:{mouseInfo.dwExtraInfo.ToInt64():X})");
                    }
                }
            }
            
            // Call the next hook
            return CallNextHookEx(_mouseHookID, nCode, wParam, lParam);
        }
        
        protected IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam)
        {
            const int WM_INPUT = 0x00FF;
            
            if (msg == WM_INPUT)
            {
                ProcessRawInput(lParam);
            }
            
            return DefWindowProc(hwnd, msg, wParam, lParam);
        }
        
        private void ProcessRawInput(IntPtr lParam)
        {
            uint dwSize = 0;
            
            // Get the size of the input data
            GetRawInputData(lParam, 0x10000003, IntPtr.Zero, ref dwSize, (uint)Marshal.SizeOf(typeof(RAWINPUTHEADER)));
            
            if (dwSize > 0)
            {
                IntPtr buffer = Marshal.AllocHGlobal((int)dwSize);
                try
                {
                    // Get the input data
                    if (GetRawInputData(lParam, 0x10000003, buffer, ref dwSize, (uint)Marshal.SizeOf(typeof(RAWINPUTHEADER))) == dwSize)
                    {
                        RAWINPUT raw = Marshal.PtrToStructure<RAWINPUT>(buffer);
                        
                        // Process the raw input based on its type
                        switch (raw.header.dwType)
                        {
                            case 0: // RIM_TYPEMOUSE
                                ProcessRawMouseInput(raw);
                                break;
                            case 1: // RIM_TYPEKEYBOARD
                                ProcessRawKeyboardInput(raw);
                                break;
                            case 2: // RIM_TYPEHID
                                ProcessRawHIDInput(raw);
                                break;
                        }
                    }
                }
                finally
                {
                    Marshal.FreeHGlobal(buffer);
                }
            }
        }
        
        private void ProcessRawMouseInput(RAWINPUT raw)
        {
            RAWMOUSE mouseData = raw.data.mouse;
            
            // Build flags string
            StringBuilder flagsStr = new StringBuilder();
            if ((mouseData.usFlags & 0x0001) != 0) flagsStr.Append("MOUSE_MOVE_RELATIVE ");
            if ((mouseData.usFlags & 0x0002) != 0) flagsStr.Append("MOUSE_MOVE_ABSOLUTE ");
            if ((mouseData.usFlags & 0x0004) != 0) flagsStr.Append("MOUSE_VIRTUAL_DESKTOP ");
            if ((mouseData.usFlags & 0x0008) != 0) flagsStr.Append("MOUSE_ATTRIBUTES_CHANGED ");
            if ((mouseData.usFlags & 0x0010) != 0) flagsStr.Append("MOUSE_MOVE_NOCOALESCE ");
            
            // Build buttons string
            StringBuilder buttonsStr = new StringBuilder();
            if ((mouseData.usButtonFlags & 0x0001) != 0) buttonsStr.Append("LEFT_DOWN ");
            if ((mouseData.usButtonFlags & 0x0002) != 0) buttonsStr.Append("LEFT_UP ");
            if ((mouseData.usButtonFlags & 0x0004) != 0) buttonsStr.Append("RIGHT_DOWN ");
            if ((mouseData.usButtonFlags & 0x0008) != 0) buttonsStr.Append("RIGHT_UP ");
            if ((mouseData.usButtonFlags & 0x0010) != 0) buttonsStr.Append("MIDDLE_DOWN ");
            if ((mouseData.usButtonFlags & 0x0020) != 0) buttonsStr.Append("MIDDLE_UP ");
            if ((mouseData.usButtonFlags & 0x0040) != 0) buttonsStr.Append("XBUTTON1_DOWN ");
            if ((mouseData.usButtonFlags & 0x0080) != 0) buttonsStr.Append("XBUTTON1_UP ");
            if ((mouseData.usButtonFlags & 0x0100) != 0) buttonsStr.Append("XBUTTON2_DOWN ");
            if ((mouseData.usButtonFlags & 0x0200) != 0) buttonsStr.Append("XBUTTON2_UP ");
            if ((mouseData.usButtonFlags & 0x0400) != 0) buttonsStr.Append("WHEEL ");
            if ((mouseData.usButtonFlags & 0x0800) != 0) buttonsStr.Append("HWHEEL ");
            
            // Special detection for potential Razer buttons (look at ALL raw input)
            // Some gaming mice like the DeathAdder V3 Pro may use non-standard button flags
            bool hasButtonActivity = mouseData.usButtonFlags != 0 ||
                                     mouseData.ulRawButtons != 0 ||
                                     mouseData.ulExtraInformation != 0;
            
            if (EnableLogging)
            {
                Debug.WriteLine($"Raw Mouse: Flags={flagsStr}, Buttons={buttonsStr}, LastX={mouseData.lLastX}, LastY={mouseData.lLastY}, ExtraInfo={mouseData.ulExtraInformation:X}, RawButtons={mouseData.ulRawButtons:X}");
            }
            
            // Add to detected inputs if there's any mouse activity (broader detection)
            if (hasButtonActivity)
            {
                // Get button name based on flags
                string buttonName = "Unknown";
                if (buttonsStr.Length > 0)
                {
                    buttonName = buttonsStr.ToString().Trim();
                }
                else if (mouseData.ulRawButtons != 0)
                {
                    buttonName = $"RawButton_{mouseData.ulRawButtons:X}";
                }
                else if (mouseData.ulExtraInformation != 0)
                {
                    buttonName = $"ExtraButton_{mouseData.ulExtraInformation:X}";
                }
                
                // Create comprehensive input data
                InputEventData inputData = new InputEventData
                {
                    Type = InputType.RawMouse,
                    Flags = mouseData.usFlags,
                    ButtonFlags = mouseData.usButtonFlags,
                    Button = buttonName,
                    RawData = $"RawButtons:{mouseData.ulRawButtons:X} Flags:{mouseData.usFlags:X} ButtonFlags:{mouseData.usButtonFlags:X} X:{mouseData.lLastX} Y:{mouseData.lLastY}",
                    TimeStamp = DateTime.Now,
                    ExtraInfo = (long)mouseData.ulExtraInformation
                };
                
                DetectedInputs.Add(inputData);
                
                // Raise event
                RawInputEvent?.Invoke(new RawInputData
                {
                    DeviceType = "Mouse",
                    ButtonFlags = mouseData.usButtonFlags,
                    Flags = mouseData.usFlags,
                    ExtraData = mouseData.ulExtraInformation,
                    RawData = $"X:{mouseData.lLastX} Y:{mouseData.lLastY}"
                });
            }
        }
        
        private void ProcessRawKeyboardInput(RAWINPUT raw)
        {
            if (EnableLogging)
            {
                RAWKEYBOARD keyData = raw.data.keyboard;
                
                // Get key name
                string keyName = ((Keys)keyData.VKey).ToString();
                
                // Build message type string
                string messageType = (keyData.Message == WM_KEYDOWN || keyData.Message == WM_SYSKEYDOWN) ? "KEY_DOWN" : "KEY_UP";
                
                Debug.WriteLine($"Raw Keyboard: Key={keyName}, VKey={keyData.VKey:X}, ScanCode={keyData.MakeCode:X}, Flags={keyData.Flags:X}, Message={messageType}");
                
                // Add to detected inputs
                DetectedInputs.Add(new InputEventData
                {
                    Type = InputType.RawKeyboard,
                    VirtualKeyCode = keyData.VKey,
                    ScanCode = keyData.MakeCode,
                    Flags = keyData.Flags,
                    MessageType = (int)keyData.Message,
                    TimeStamp = DateTime.Now,
                    ExtraInfo = (long)keyData.ExtraInformation
                });
                
                // Raise event
                RawInputEvent?.Invoke(new RawInputData
                {
                    DeviceType = "Keyboard",
                    VirtualKey = keyData.VKey,
                    ScanCode = keyData.MakeCode,
                    Flags = keyData.Flags,
                    Message = keyData.Message,
                    ExtraData = keyData.ExtraInformation,
                    RawData = $"VK:{keyData.VKey:X} SC:{keyData.MakeCode:X}"
                });
            }
        }
        
        private void ProcessRawHIDInput(RAWINPUT raw)
        {
            if (EnableLogging)
            {
                Debug.WriteLine($"Raw HID Input: Size={raw.data.hid.dwSizeHid}, Count={raw.data.hid.dwCount}");
                
                // Add to detected inputs
                DetectedInputs.Add(new InputEventData
                {
                    Type = InputType.RawHID,
                    RawData = $"Size:{raw.data.hid.dwSizeHid} Count:{raw.data.hid.dwCount}",
                    TimeStamp = DateTime.Now
                });
                
                // Raise event
                RawInputEvent?.Invoke(new RawInputData
                {
                    DeviceType = "HID",
                    RawData = $"Size:{raw.data.hid.dwSizeHid} Count:{raw.data.hid.dwCount}"
                });
            }
        }
        
        #region Win32 API
        
        // Keyboard hook callback
        private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);
        
        // Mouse hook callback
        private delegate IntPtr LowLevelMouseProc(int nCode, IntPtr wParam, IntPtr lParam);
        
        [StructLayout(LayoutKind.Sequential)]
        private struct KBDLLHOOKSTRUCT
        {
            public uint vkCode;
            public uint scanCode;
            public uint flags;
            public uint time;
            public IntPtr dwExtraInfo;
        }
        
        [StructLayout(LayoutKind.Sequential)]
        private struct MSLLHOOKSTRUCT
        {
            public POINT pt;
            public uint mouseData;
            public uint flags;
            public uint time;
            public IntPtr dwExtraInfo;
        }
        
        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int x;
            public int y;
        }
        
        [StructLayout(LayoutKind.Sequential)]
        private struct RAWINPUTDEVICE
        {
            public ushort usUsagePage;
            public ushort usUsage;
            public uint dwFlags;
            public IntPtr hwndTarget;
        }
        
        [StructLayout(LayoutKind.Sequential)]
        private struct RAWINPUTHEADER
        {
            public uint dwType;
            public uint dwSize;
            public IntPtr hDevice;
            public IntPtr wParam;
        }
        
        [StructLayout(LayoutKind.Explicit)]
        private struct RAWINPUTDATA
        {
            [FieldOffset(0)]
            public RAWMOUSE mouse;
            [FieldOffset(0)]
            public RAWKEYBOARD keyboard;
            [FieldOffset(0)]
            public RAWHID hid;
        }
        
        [StructLayout(LayoutKind.Sequential)]
        private struct RAWINPUT
        {
            public RAWINPUTHEADER header;
            public RAWINPUTDATA data;
        }
        
        [StructLayout(LayoutKind.Sequential)]
        private struct RAWMOUSE
        {
            public ushort usFlags;
            public ushort usButtonFlags;
            public ushort usButtonData;
            public uint ulRawButtons;
            public int lLastX;
            public int lLastY;
            public uint ulExtraInformation;
        }
        
        [StructLayout(LayoutKind.Sequential)]
        private struct RAWKEYBOARD
        {
            public ushort MakeCode;
            public ushort Flags;
            public ushort Reserved;
            public ushort VKey;
            public uint Message;
            public uint ExtraInformation;
        }
        
        [StructLayout(LayoutKind.Sequential)]
        private struct RAWHID
        {
            public uint dwSizeHid;
            public uint dwCount;
            public byte bRawData;
        }
        
        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);
        
        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelMouseProc lpfn, IntPtr hMod, uint dwThreadId);
        
        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);
        
        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);
        
        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string? lpModuleName);
        
        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool RegisterRawInputDevices(
            RAWINPUTDEVICE[] pRawInputDevices,
            uint uiNumDevices,
            uint cbSize);
            
        [DllImport("user32.dll")]
        private static extern uint GetRawInputData(
            IntPtr hRawInput,
            uint uiCommand,
            IntPtr pData,
            ref uint pcbSize,
            uint cbSizeHeader);
            
        [DllImport("user32.dll")]
        private static extern IntPtr DefWindowProc(
            IntPtr hWnd,
            int msg,
            IntPtr wParam,
            IntPtr lParam);
            
        #endregion
    }
    
    public class KeyboardState
    {
        public uint VirtualKeyCode { get; set; }
        public uint ScanCode { get; set; }
        public uint Flags { get; set; }
        public uint Time { get; set; }
        public IntPtr ExtraInfo { get; set; }
        public bool IsKeyDown { get; set; }
        public bool IsExtendedKey { get; set; }
        public int MessageType { get; set; }
    }
    
    public class MouseState
    {
        public int X { get; set; }
        public int Y { get; set; }
        public string ButtonPressed { get; set; } = string.Empty;
        public int XButton { get; set; }
        public uint Data { get; set; }
        public uint Flags { get; set; }
        public uint Time { get; set; }
        public IntPtr ExtraInfo { get; set; }
        public bool IsButtonDown { get; set; }
        public int MessageType { get; set; }
    }
    
    public class RawInputData
    {
        public string DeviceType { get; set; } = string.Empty;
        public uint VirtualKey { get; set; }
        public uint ScanCode { get; set; }
        public uint Flags { get; set; }
        public uint Message { get; set; }
        public ushort ButtonFlags { get; set; }
        public uint ExtraData { get; set; }
        public string RawData { get; set; } = string.Empty;
    }
    
    public enum InputType
    {
        Keyboard,
        Mouse,
        RawKeyboard,
        RawMouse,
        RawHID
    }
    
    public class InputEventData
    {
        public InputType Type { get; set; }
        public uint VirtualKeyCode { get; set; }
        public uint ScanCode { get; set; }
        public string Button { get; set; } = string.Empty;
        public int XButton { get; set; }
        public uint Flags { get; set; }
        public ushort ButtonFlags { get; set; }
        public uint MouseData { get; set; }
        public long ExtraInfo { get; set; }
        public int MessageType { get; set; }
        public DateTime TimeStamp { get; set; }
        public string RawData { get; set; } = string.Empty;
        
        public override string ToString()
        {
            switch (Type)
            {
                case InputType.Keyboard:
                    return $"{TimeStamp.ToString("HH:mm:ss.fff")} Key: {(Keys)VirtualKeyCode} (VK:{VirtualKeyCode:X}, Msg:{MessageType:X}, Flags:{Flags:X})";
                case InputType.Mouse:
                    return $"{TimeStamp.ToString("HH:mm:ss.fff")} Mouse: {Button} (Msg:{MessageType:X}, Data:{MouseData:X}, Flags:{Flags:X})";
                case InputType.RawKeyboard:
                    return $"{TimeStamp.ToString("HH:mm:ss.fff")} Raw KB: {(Keys)VirtualKeyCode} (VK:{VirtualKeyCode:X}, SC:{ScanCode:X}, Msg:{MessageType:X})";
                case InputType.RawMouse:
                    return $"{TimeStamp.ToString("HH:mm:ss.fff")} Raw Mouse: Flags:{Flags:X}, ButtonFlags:{ButtonFlags:X}, {RawData}";
                case InputType.RawHID:
                    return $"{TimeStamp.ToString("HH:mm:ss.fff")} Raw HID: {RawData}";
                default:
                    return $"{TimeStamp.ToString("HH:mm:ss.fff")} {Type}: {RawData}";
            }
        }
    }
}