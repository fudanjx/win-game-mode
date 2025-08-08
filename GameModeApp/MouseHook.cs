using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace GameModeApp
{
    public class MouseHook
    {
        // Standard mouse messages
        private const int WH_MOUSE_LL = 14;
        private const int WM_XBUTTONDOWN = 0x020B;
        private const int WM_XBUTTONUP = 0x020C;
        private const int WM_MBUTTONDOWN = 0x0207;
        private const int WM_LBUTTONDOWN = 0x0201;
        private const int WM_RBUTTONDOWN = 0x0204;
        private const int WM_MOUSEMOVE = 0x0200;
        
        // Mouse X-button values and bitmasks
        private const int XBUTTON1 = 0x0001;
        private const int XBUTTON2 = 0x0002;
        private const uint XBUTTON_BITMASK = 0xFFFF0000;
        
        // Razer DeathAdder V3 Pro Specific Values (from learning mode)
        private const int RAZER_DEATHADDER_V3_PRO_FIRST_BUTTON = 0x020B; // Value 208 hex (WM_XBUTTONDOWN)
        private const int RAZER_DEATHADDER_V3_PRO_FIRST_DATA = 0x20000; // Value 20000 hex
        
        // Possible values for the second button - we need to try different approaches
        // These are educated guesses for potential patterns based on common button mappings
        private static readonly int[] POSSIBLE_SECOND_BUTTON_MESSAGES = new int[] {
            0x0100, // WM_KEYDOWN - some mice map buttons to keyboard keys
            0x0201, // WM_LBUTTONDOWN
            0x0204, // WM_RBUTTONDOWN
            0x0207, // WM_MBUTTONDOWN
            0x020B, // WM_XBUTTONDOWN
            0x00FF  // WM_INPUT - Raw input
        };
        
        // Windows keyboard scan codes
        private const ushort VK_1 = 0x31;
        private const ushort VK_2 = 0x32;
        private const ushort VK_SHIFT = 0x10;
        
        // Button learning mode
        public bool IsLearningMode { get; set; }
        public Dictionary<string, ButtonInfo> LearnedButtons { get; private set; } = new Dictionary<string, ButtonInfo>();
        public Dictionary<ButtonInfo, string> CustomMappings { get; private set; } = new Dictionary<ButtonInfo, string>();
        
        // Event handler to allow external handling of button detections
        public void OnButtonDetected(ButtonInfo buttonInfo)
        {
            ButtonDetected?.Invoke(buttonInfo);
        }
        
        // Track button presses for multi-button handling
        private bool isFirstSideButtonProcessed = false;
        private bool isSecondSideButtonProcessed = false;

        // Delegates
        public delegate void ButtonDetectedHandler(ButtonInfo buttonInfo);
        public event ButtonDetectedHandler? ButtonDetected;
        
        private LowLevelMouseProc _proc;
        private IntPtr _hookID = IntPtr.Zero;
        
        public bool IsHookEnabled { get; set; }
        public KeyMappingProfile CurrentProfile { get; set; } = KeyMappingProfile.Disabled;
        
        // Event that's raised when a mouse button is remapped
        public event EventHandler<MouseRemapEventArgs>? MouseRemapped;
        
        // For debugging
        public bool EnableDebugLogging { get; set; } = false;

        public MouseHook()
        {
            _proc = HookCallback;
            InitializeLearningMode();
            
            // Add system-wide hook for raw input messages
            Application.AddMessageFilter(new RawInputMessageFilter(this));
        }
        
        private void InitializeLearningMode()
        {
            // Default mapping for known buttons
            LearnedButtons["XButton1"] = new ButtonInfo
            {
                MessageType = WM_XBUTTONDOWN,
                ButtonId = XBUTTON1,
                Name = "XButton1"
            };
            
            LearnedButtons["XButton2"] = new ButtonInfo
            {
                MessageType = WM_XBUTTONDOWN,
                ButtonId = XBUTTON2,
                Name = "XButton2"
            };
            
            // Add the specific Razer DeathAdder V3 Pro buttons we found
            // IMPORTANT: From your testing, this is actually the SECOND side button
            LearnedButtons["DeathAdderSecondSide"] = new ButtonInfo
            {
                MessageType = RAZER_DEATHADDER_V3_PRO_FIRST_BUTTON,
                ButtonId = RAZER_DEATHADDER_V3_PRO_FIRST_DATA,
                Name = "DeathAdderSecondSide"
            };
            
            // For the first side button, we'll try multiple button types
            // since it seems to be detected differently
            
            // Try as left mouse button
            LearnedButtons["DeathAdderFirstSide_LMB"] = new ButtonInfo
            {
                MessageType = WM_LBUTTONDOWN,
                ButtonId = 0,
                Name = "DeathAdderFirstSide_LMB"
            };
            
            // Try as right mouse button
            LearnedButtons["DeathAdderFirstSide_RMB"] = new ButtonInfo
            {
                MessageType = WM_RBUTTONDOWN,
                ButtonId = 0,
                Name = "DeathAdderFirstSide_RMB"
            };
            
            // Try as middle mouse button
            LearnedButtons["DeathAdderFirstSide_MMB"] = new ButtonInfo
            {
                MessageType = WM_MBUTTONDOWN,
                ButtonId = 0,
                Name = "DeathAdderFirstSide_MMB"
            };
            
            // Try as XButton1
            LearnedButtons["DeathAdderFirstSide_X1"] = new ButtonInfo
            {
                MessageType = WM_XBUTTONDOWN,
                ButtonId = XBUTTON1,
                Name = "DeathAdderFirstSide_X1"
            };
            
            // Try as XButton2
            LearnedButtons["DeathAdderFirstSide_X2"] = new ButtonInfo
            {
                MessageType = WM_XBUTTONDOWN,
                ButtonId = XBUTTON2,
                Name = "DeathAdderFirstSide_X2"
            };
        }
        
        public void Install()
        {
            _hookID = SetHook(_proc);
            IsHookEnabled = true;
            isFirstSideButtonProcessed = false;
            isSecondSideButtonProcessed = false;
            Debug.WriteLine("Mouse hook installed");
            
            // Set up low level keyboard hook too to catch potential keyboard mappings
            SetupKeyboardHook();
        }
        
        private void SetupKeyboardHook()
        {
            // Many gaming mice map their buttons to keyboard keys
            // This is especially common for side buttons on Razer mice
            // We add this hook to catch such mappings
            try
            {
                // Register Raw Input for both keyboard and mouse
                RegisterRawInputDevices();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error setting up keyboard hook: {ex.Message}");
            }
        }
        
        private void RegisterRawInputDevices()
        {
            RAWINPUTDEVICE[] rid = new RAWINPUTDEVICE[2];
            
            // Register for mouse input
            rid[0].usUsagePage = 0x01;
            rid[0].usUsage = 0x02; // Mouse
            rid[0].dwFlags = 0;
            rid[0].hwndTarget = IntPtr.Zero;
            
            // Register for keyboard input
            rid[1].usUsagePage = 0x01;
            rid[1].usUsage = 0x06; // Keyboard
            rid[1].dwFlags = 0; 
            rid[1].hwndTarget = IntPtr.Zero;
            
            if (!RegisterRawInputDevices(rid, (uint)rid.Length, (uint)Marshal.SizeOf(typeof(RAWINPUTDEVICE))))
            {
                Debug.WriteLine("Failed to register raw input devices: " + Marshal.GetLastWin32Error());
            }
        }

        public void Uninstall()
        {
            if (_hookID != IntPtr.Zero)
            {
                UnhookWindowsHookEx(_hookID);
                _hookID = IntPtr.Zero;
            }
            IsHookEnabled = false;
            Debug.WriteLine("Mouse hook uninstalled");
        }
        
        public void AddCustomButtonMapping(int messageType, long extraInfoMask, int buttonData, string name)
        {
            ButtonInfo buttonInfo = new ButtonInfo
            {
                MessageType = messageType,
                ExtraInfoMask = extraInfoMask,
                ButtonId = buttonData,
                Name = name
            };
            
            LearnedButtons[name] = buttonInfo;
            Debug.WriteLine($"Added custom button mapping: {name}");
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

        private delegate IntPtr LowLevelMouseProc(
            int nCode, IntPtr wParam, IntPtr lParam);

        private IntPtr HookCallback(
            int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0)
            {
                int messageType = wParam.ToInt32();
                
                // Skip mouse move events for performance
                if (messageType == WM_MOUSEMOVE)
                {
                    return CallNextHookEx(_hookID, nCode, wParam, lParam);
                }
                
                // Extract info from mouse event
                MSLLHOOKSTRUCT hookStruct = Marshal.PtrToStructure<MSLLHOOKSTRUCT>(lParam);
                
                // Special handling for Razer DeathAdder V3 Pro based on learning mode results
                if (IsDeathAdderSideButton(messageType, hookStruct))
                {
                    if (EnableDebugLogging)
                    {
                        Debug.WriteLine($"DeathAdder side button detected: {messageType:X} with data: {hookStruct.mouseData:X}");
                    }
                    
                    if (IsLearningMode)
                    {
                        // In learning mode, capture the button info
                        string buttonName = $"RazerButton_{messageType:X}_{hookStruct.mouseData:X}_{hookStruct.dwExtraInfo.ToInt64():X}";
                        var buttonInfo = new ButtonInfo
                        {
                            MessageType = messageType,
                            ButtonId = (int)hookStruct.mouseData,
                            ExtraInfoMask = hookStruct.dwExtraInfo.ToInt64(),
                            Name = buttonName
                        };
                        LearnedButtons[buttonName] = buttonInfo;
                        ButtonDetected?.Invoke(buttonInfo);
                        
                        return (IntPtr)1; // Handled, prevent original button press in learning mode
                    }
                    
                    if (IsHookEnabled && CurrentProfile != KeyMappingProfile.Disabled)
                    {
                        // Mark the side button as processed
                        if (!isFirstSideButtonProcessed) 
                        {
                            isFirstSideButtonProcessed = true;
                            if (EnableDebugLogging) Debug.WriteLine("First side button processed");
                            return HandleButtonPress("DeathAdderSide1", buttonName: "First Side Button");
                        }
                    }
                }
                
                // Log mouse button activity if debug is enabled
                if (EnableDebugLogging)
                {
                    if (messageType == WM_LBUTTONDOWN || messageType == WM_RBUTTONDOWN ||
                        messageType == WM_MBUTTONDOWN || messageType == WM_XBUTTONDOWN)
                    {
                        Debug.WriteLine($"Button press detected: Msg={messageType:X}, Data={hookStruct.mouseData:X}, " +
                                      $"ExtraInfo={hookStruct.dwExtraInfo.ToInt64():X}, Flags={hookStruct.flags:X}");
                    }
                }
                
                // Check for standard X buttons
                if (messageType == WM_XBUTTONDOWN && IsHookEnabled && CurrentProfile != KeyMappingProfile.Disabled)
                {
                    int xButtonFlags = (int)((hookStruct.mouseData >> 16) & 0xFFFF);
                    
                    // Check specific patterns
                    if (xButtonFlags == XBUTTON1 || xButtonFlags == XBUTTON2)
                    {
                        // For the Razer DeathAdder V3 Pro, the second side button might use this pattern
                        if (!isFirstSideButtonProcessed && !isSecondSideButtonProcessed)
                        {
                            // If this is the first button press we've seen, assume it's the first side button
                            isFirstSideButtonProcessed = true;
                            return HandleButtonPress("DeathAdderSide1", buttonName: "First Side Button");
                        }
                        else if (isFirstSideButtonProcessed && !isSecondSideButtonProcessed)
                        {
                            // If we already processed one button, this might be the second side button
                            isSecondSideButtonProcessed = true;
                            return HandleButtonPress("DeathAdderSide2", buttonName: "Second Side Button");
                        }
                        else
                        {
                            // Reset our tracking after handling both buttons
                            isFirstSideButtonProcessed = false;
                            isSecondSideButtonProcessed = false;
                            return HandleButtonPress(xButtonFlags == XBUTTON1 ? "XButton1" : "XButton2", buttonName: xButtonFlags == XBUTTON1 ? "XButton1" : "XButton2");
                        }
                    }
                }
                
                // Special case for very specific button pattern we found in learning mode
                if (messageType == RAZER_DEATHADDER_V3_PRO_FIRST_BUTTON &&
                    hookStruct.mouseData == RAZER_DEATHADDER_V3_PRO_FIRST_DATA &&
                    IsHookEnabled && CurrentProfile != KeyMappingProfile.Disabled)
                {
                    if (EnableDebugLogging)
                    {
                        Debug.WriteLine("Exact match for Razer DeathAdder V3 Pro button detected - Second side button");
                    }
                    
                    // This is actually the second side button based on testing
                    return HandleButtonPress("RazerDeathAdderSecondButton", buttonName: "Second Side Button");
                }
                
                // For first side button, we'll use a more flexible approach
                // Check for any mouse button that might be the first side button
                if ((messageType == WM_LBUTTONDOWN || messageType == WM_RBUTTONDOWN || 
                     messageType == WM_MBUTTONDOWN || messageType == WM_XBUTTONDOWN) &&
                    IsHookEnabled && CurrentProfile != KeyMappingProfile.Disabled)
                {
                    // Get some context to identify the button
                    long extraInfo = hookStruct.dwExtraInfo.ToInt64();
                    int xButtonFlags = 0;
                    
                    if (messageType == WM_XBUTTONDOWN)
                    {
                        xButtonFlags = (int)((hookStruct.mouseData >> 16) & 0xFFFF);
                    }
                    
                    // If this doesn't match our known second button pattern, 
                    // it's likely the first button or another button
                    if (messageType != RAZER_DEATHADDER_V3_PRO_FIRST_BUTTON || 
                        hookStruct.mouseData != RAZER_DEATHADDER_V3_PRO_FIRST_DATA)
                    {
                        if (EnableDebugLogging)
                        {
                            Debug.WriteLine($"Potential first side button: Msg={messageType:X}, Data={hookStruct.mouseData:X}, ExtraInfo={extraInfo:X}");
                        }
                        
                        // This is likely the first side button
                        if (!isSecondSideButtonProcessed)
                        {
                            return HandleButtonPress("RazerDeathAdderFirstButton", buttonName: "First Side Button");
                        }
                    }
                }
            }
            
            return CallNextHookEx(_hookID, nCode, wParam, lParam);
        }
        
        private bool IsDeathAdderSideButton(int messageType, MSLLHOOKSTRUCT hookStruct)
        {
            // Since only the second button is detected with 0x20B and 0x20000,
            // We need to handle this specific button differently.
            if (messageType == RAZER_DEATHADDER_V3_PRO_FIRST_BUTTON && 
                hookStruct.mouseData == RAZER_DEATHADDER_V3_PRO_FIRST_DATA)
            {
                // Based on the testing, this is actually the SECOND button
                // Let's identify this as the second side button
                if (EnableDebugLogging)
                {
                    Debug.WriteLine($"DeathAdder SECOND side button detected: {messageType:X} with data: {hookStruct.mouseData:X}");
                }
                
                // Mark as second button
                isSecondSideButtonProcessed = true;
                return true;
            }
            
            // For the first button, we need to try some different approaches
            
            // Check for X buttons (common for side buttons)
            if (messageType == WM_XBUTTONDOWN)
            {
                // Get the XButton value
                int xButtonFlags = (int)((hookStruct.mouseData >> 16) & 0xFFFF);
                
                if (EnableDebugLogging)
                {
                    Debug.WriteLine($"XButton detected: XButton={xButtonFlags}, ExtraInfo={hookStruct.dwExtraInfo.ToInt64():X}");
                }
                
                // This is likely the first side button
                isFirstSideButtonProcessed = true;
                return true;
            }
            
            // Also check for other mouse buttons that might be mapped
            if (messageType == WM_MBUTTONDOWN || 
                messageType == WM_RBUTTONDOWN ||
                messageType == WM_LBUTTONDOWN)
            {
                // Check for specific flags or patterns
                // Some Razer mice use injected flags
                if ((hookStruct.flags & 0x01) != 0)  // LLMHF_INJECTED flag
                {
                    if (EnableDebugLogging)
                    {
                        Debug.WriteLine($"Injected mouse button detected: Msg={messageType:X}, Flags={hookStruct.flags:X}");
                    }
                    
                    // Try as first button
                    isFirstSideButtonProcessed = true;
                    return true;
                }
                
                // Check for specific extra info patterns that might indicate it's from the Razer driver
                long extraInfo = hookStruct.dwExtraInfo.ToInt64();
                if (extraInfo != 0 && extraInfo != 0x01000000 && extraInfo != 0x70000000)
                {
                    if (EnableDebugLogging)
                    {
                        Debug.WriteLine($"Special mouse button detected: Msg={messageType:X}, ExtraInfo={extraInfo:X}");
                    }
                    
                    // Try as first button
                    isFirstSideButtonProcessed = true;
                    return true;
                }
            }
            
            // Add specific detection for Razer buttons using raw input 
            // First button might be detected as a different type of event
            if (!isFirstSideButtonProcessed && !isSecondSideButtonProcessed)
            {
                // If we haven't processed any button yet, treat this as a first button
                isFirstSideButtonProcessed = true;
                return true;
            }
            
            return false;
        }
        
        private IntPtr HandleButtonPress(string buttonId, string? buttonName = null)
        {
            if (EnableDebugLogging)
            {
                Debug.WriteLine($"Handling button press for: {buttonId} ({buttonName ?? buttonId})");
            }
            
            switch (CurrentProfile)
            {
                case KeyMappingProfile.CSGO:
                    // Send keyboard strokes: "2" then "1" with 1ms delay
                    SendKeyPress(VK_2);
                    Thread.Sleep(1);
                    SendKeyPress(VK_1);
                    
                    MouseRemapped?.Invoke(this, new MouseRemapEventArgs(
                        buttonName ?? buttonId, "2 → 1", CurrentProfile));
                    return (IntPtr)1; // Handled, prevent original button press
                    
                case KeyMappingProfile.OW:
                    // Send left shift keyboard stroke
                    SendKeyPress(VK_SHIFT);
                    
                    MouseRemapped?.Invoke(this, new MouseRemapEventArgs(
                        buttonName ?? buttonId, "LEFT SHIFT", CurrentProfile));
                    return (IntPtr)1; // Handled, prevent original button press
            }
            
            return (IntPtr)0; // Not handled
        }
        
        private void SendKeyPress(ushort keyCode)
        {
            // Create input for key down
            INPUT[] inputs = new INPUT[2];
            
            // Key down
            inputs[0].type = 1; // INPUT_KEYBOARD
            inputs[0].U.ki.wVk = keyCode;
            inputs[0].U.ki.dwFlags = 0; // Key down
            
            // Key up
            inputs[1].type = 1; // INPUT_KEYBOARD
            inputs[1].U.ki.wVk = keyCode;
            inputs[1].U.ki.dwFlags = 2; // KEYEVENTF_KEYUP
            
            // Send the input
            SendInput(2, inputs, Marshal.SizeOf(typeof(INPUT)));
        }

        #region Win32 API
        private const int INPUT_KEYBOARD = 1;
        private const int KEYEVENTF_KEYUP = 0x0002;
        
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
        private struct INPUT
        {
            public int type;
            public InputUnion U;
        }
        
        [StructLayout(LayoutKind.Explicit)]
        private struct InputUnion
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
            public uint mouseData;
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
        
        [StructLayout(LayoutKind.Sequential)]
        private struct RAWINPUTDEVICE
        {
            public ushort usUsagePage;
            public ushort usUsage;
            public uint dwFlags;
            public IntPtr hwndTarget;
        }

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook,
            LowLevelMouseProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode,
            IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string? lpModuleName);
        
        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);
        
        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool RegisterRawInputDevices(
            RAWINPUTDEVICE[] pRawInputDevices, 
            uint uiNumDevices,
            uint cbSize);
        #endregion
    }
    
    public class ButtonInfo
    {
        public int MessageType { get; set; }
        public int ButtonId { get; set; }
        public long ExtraInfoMask { get; set; }
        public string Name { get; set; } = string.Empty;
        
        public override string ToString()
        {
            return $"{Name}: Msg={MessageType:X}, ID={ButtonId:X}, Mask={ExtraInfoMask:X}";
        }
    }
    
    public enum KeyMappingProfile
    {
        Disabled,
        CSGO,
        OW
    }
    
    public class MouseRemapEventArgs : EventArgs
    {
        public string SourceButton { get; }
        public string MappedTo { get; }
        public KeyMappingProfile Profile { get; }
        
        public MouseRemapEventArgs(string sourceButton, string mappedTo, KeyMappingProfile profile)
        {
            SourceButton = sourceButton;
            MappedTo = mappedTo;
            Profile = profile;
        }
    }
    
    // Message filter to intercept raw input messages directly from Windows message queue
    internal class RawInputMessageFilter : IMessageFilter
    {
        private const int WM_INPUT = 0x00FF;
        private MouseHook _mouseHook;
        
        public RawInputMessageFilter(MouseHook mouseHook)
        {
            _mouseHook = mouseHook;
        }
        
        public bool PreFilterMessage(ref Message m)
        {
            // Check if this is a raw input message
            if (m.Msg == WM_INPUT)
            {
                // If we're in learning mode, try to capture this message
                if (_mouseHook.IsLearningMode)
                {
                    // Get the device info
                    uint dwSize = 0;
                    GetRawInputData(m.LParam, 0x10000003, IntPtr.Zero, ref dwSize, 16); // 16 is the size of the RAWINPUTHEADER structure
                    
                    if (dwSize > 0)
                    {
                        IntPtr buffer = Marshal.AllocHGlobal((int)dwSize);
                        try
                        {
                            if (GetRawInputData(m.LParam, 0x10000003, buffer, ref dwSize, 16) == dwSize)
                            {
                                // Process the raw input data
                                if (_mouseHook.EnableDebugLogging)
                                {
                                    Debug.WriteLine("Raw input message intercepted");
                                }
                                
                                // Create a button info for this raw input
                                var buttonInfo = new ButtonInfo
                                {
                                    MessageType = WM_INPUT,
                                    ButtonId = (int)m.WParam,
                                    ExtraInfoMask = m.LParam.ToInt64(),
                                    Name = $"RawInput_{m.WParam.ToInt32():X}_{m.LParam.ToInt64():X}"
                                };
                                
                                _mouseHook.LearnedButtons[buttonInfo.Name] = buttonInfo;
                                _mouseHook.OnButtonDetected(buttonInfo);
                            }
                        }
                        finally
                        {
                            Marshal.FreeHGlobal(buffer);
                        }
                    }
                }
            }
            
            // Allow the message to continue to the application
            return false;
        }
        
        [DllImport("user32.dll")]
        private static extern uint GetRawInputData(
            IntPtr hRawInput,
            uint uiCommand,
            IntPtr pData,
            ref uint pcbSize,
            uint cbSizeHeader);
    }
}