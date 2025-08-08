using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace GameModeApp
{
    public partial class MainForm : Form
    {
        private NotifyIcon? trayIcon;
        private ContextMenuStrip? trayMenu;
        private KeyboardHook keyboardHook;
        private MouseHook mouseHook;
        private bool isGameModeActive = false;
        private Form? buttonLearningForm;
        private InputMonitorForm? inputMonitorForm;

        // Icons for active and inactive states
        private Icon? activeIcon;
        private Icon? inactiveIcon;

        // Menu items for game profiles
        private ToolStripMenuItem? keyMapProfileMenu;
        private ToolStripMenuItem? disabledProfileItem;
        private ToolStripMenuItem? csgoProfileItem;
        private ToolStripMenuItem? owProfileItem;
        private ToolStripMenuItem? debugLoggingItem;
        private ToolStripMenuItem? learningModeItem;
        private ToolStripMenuItem? inputMonitorMenuItem;

        // List to store detected buttons during learning mode
        private List<ButtonInfo> detectedButtons = new List<ButtonInfo>();

        public MainForm()
        {
            // We're creating UI manually, not using InitializeComponent
            
            // Initialize the keyboard hook
            keyboardHook = new KeyboardHook();
            keyboardHook.KeyBlocked += KeyboardHook_KeyBlocked;

            // Initialize the mouse hook
            mouseHook = new MouseHook();
            mouseHook.MouseRemapped += MouseHook_MouseRemapped;
            mouseHook.ButtonDetected += MouseHook_ButtonDetected;

            // Set up the tray icon and menu
            SetupTrayIcon();

            // Load icons
            LoadIcons();
            
            // For Razer DeathAdder buttons
            AddRazerMouseButtonMappings();

            // Hide form on startup
            this.WindowState = FormWindowState.Minimized;
            this.ShowInTaskbar = false;
            this.FormBorderStyle = FormBorderStyle.SizableToolWindow;
            this.Opacity = 0;
            this.Size = new Size(0, 0);
        }
        
        private void AddRazerMouseButtonMappings()
        {
            // Add common patterns for Razer DeathAdder V3 Pro side buttons
            // These are based on observed patterns but may need adjustment
            
            // From our testing, we know the second button uses this pattern
            mouseHook.AddCustomButtonMapping(0x020B, 0, 0x20000, "DeathAdderSecondButton");
            
            // For the first button, we'll try multiple patterns that could match
            // We'll use every common mouse button type to try to catch it
            
            // Try standard mouse buttons
            mouseHook.AddCustomButtonMapping(0x0201, 0, 0, "DeathAdderFirstButton_LMB"); // Left button
            mouseHook.AddCustomButtonMapping(0x0204, 0, 0, "DeathAdderFirstButton_RMB"); // Right button
            mouseHook.AddCustomButtonMapping(0x0207, 0, 0, "DeathAdderFirstButton_MMB"); // Middle button
            
            // Try X buttons
            mouseHook.AddCustomButtonMapping(0x020B, 0, 0x10000, "DeathAdderFirstButton_X1"); // XButton1
            mouseHook.AddCustomButtonMapping(0x020B, 0, 0x10000, "DeathAdderFirstButton_X2"); // XButton2
            
            // Try common forwarding values (browser back/forward)
            mouseHook.AddCustomButtonMapping(0x0204, 0x01000000, 0, "DeathAdderFirstButton_Forward");
            mouseHook.AddCustomButtonMapping(0x0201, 0x01000000, 0, "DeathAdderFirstButton_Back");
            
            // Try alternative ExtraInfo patterns
            mouseHook.AddCustomButtonMapping(0x0204, 0x70000000, 0, "DeathAdderFirstButton_AltForward");
            mouseHook.AddCustomButtonMapping(0x0201, 0x70000000, 0, "DeathAdderFirstButton_AltBack");
            
            // Try "any mouse button" approach - this is important for buttons 
            // that might appear as standard buttons but have special meaning
            mouseHook.AddCustomButtonMapping(0, 0, 0, "DeathAdderFirstButton_Any");
        }

        private void MouseHook_ButtonDetected(ButtonInfo buttonInfo)
        {
            // Called when a button is detected in learning mode
            detectedButtons.Add(buttonInfo);
            
            // Update the learning form if it's open
            if (buttonLearningForm != null && !buttonLearningForm.IsDisposed)
            {
                if (buttonLearningForm.Controls.Count > 0 && 
                    buttonLearningForm.Controls[0] is ListBox listBox)
                {
                    listBox.Invoke((MethodInvoker)delegate {
                        listBox.Items.Add(buttonInfo.ToString());
                    });
                }
            }
        }

        private void MouseHook_MouseRemapped(object? sender, MouseRemapEventArgs e)
        {
            // Log remapped mouse button
            Debug.WriteLine($"Remapped {e.SourceButton} to {e.MappedTo} using {e.Profile} profile");
        }

        private void LoadIcons()
        {
            try
            {
                // Try to load custom icons
                string activePath = Path.Combine(Application.StartupPath, "Resources", "active.ico");
                string inactivePath = Path.Combine(Application.StartupPath, "Resources", "inactive.ico");
                
                if (File.Exists(activePath) && File.Exists(inactivePath))
                {
                    activeIcon = new Icon(activePath);
                    inactiveIcon = new Icon(inactivePath);
                }
                else
                {
                    // If custom icons fail to load, use the application icon
                    string appIconPath = Path.Combine(Application.StartupPath, "Resources", "app.ico");
                    if (File.Exists(appIconPath))
                    {
                        activeIcon = new Icon(appIconPath);
                        inactiveIcon = new Icon(appIconPath);
                    }
                    else
                    {
                        activeIcon = SystemIcons.Application;
                        inactiveIcon = SystemIcons.Application;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error loading icons: {ex.Message}");
                activeIcon = SystemIcons.Application;
                inactiveIcon = SystemIcons.Application;
            }

            // Set initial icon
            if (trayIcon != null && inactiveIcon != null)
            {
                trayIcon.Icon = inactiveIcon;
            }
        }

        private void SetupTrayIcon()
        {
            trayMenu = new ContextMenuStrip();
            trayMenu.Items.Add("Enable Game Mode", null, OnToggleGameMode);
            
            // Add key mapping submenu
            keyMapProfileMenu = new ToolStripMenuItem("Key Mapping Profile");
            
            disabledProfileItem = new ToolStripMenuItem("Disabled", null, OnKeyMapProfileSelected);
            disabledProfileItem.Tag = KeyMappingProfile.Disabled;
            disabledProfileItem.Checked = true;
            keyMapProfileMenu.DropDownItems.Add(disabledProfileItem);
            
            csgoProfileItem = new ToolStripMenuItem("CSGO (Mouse → 2 → 1)", null, OnKeyMapProfileSelected);
            csgoProfileItem.Tag = KeyMappingProfile.CSGO;
            keyMapProfileMenu.DropDownItems.Add(csgoProfileItem);
            
            owProfileItem = new ToolStripMenuItem("OW (Mouse → Left Shift)", null, OnKeyMapProfileSelected);
            owProfileItem.Tag = KeyMappingProfile.OW;
            keyMapProfileMenu.DropDownItems.Add(owProfileItem);
            
            trayMenu.Items.Add(keyMapProfileMenu);
            
            // Add debug logging option
            debugLoggingItem = new ToolStripMenuItem("Enable Debug Logging", null, OnToggleDebugLogging);
            trayMenu.Items.Add(debugLoggingItem);
            
            // Add button learning mode option
            learningModeItem = new ToolStripMenuItem("Button Learning Mode", null, OnToggleLearningMode);
            trayMenu.Items.Add(learningModeItem);
            
            // Add input monitor option
            inputMonitorMenuItem = new ToolStripMenuItem("Advanced Input Monitor", null, OnShowInputMonitor);
            trayMenu.Items.Add(inputMonitorMenuItem);
            
            trayMenu.Items.Add("-"); // Separator
            trayMenu.Items.Add("Exit", null, OnExit);

            trayIcon = new NotifyIcon();
            trayIcon.Text = "Game Mode - Inactive";
            trayIcon.ContextMenuStrip = trayMenu;
            trayIcon.Visible = true;
            trayIcon.MouseClick += TrayIcon_MouseClick;
        }
        
        private void OnShowInputMonitor(object? sender, EventArgs e)
        {
            // Create and show the input monitor form
            if (inputMonitorForm == null || inputMonitorForm.IsDisposed)
            {
                inputMonitorForm = new InputMonitorForm();
                inputMonitorForm.ButtonsSelected += InputMonitorForm_ButtonsSelected;
                inputMonitorForm.FormClosed += (s, args) => inputMonitorForm = null;
                inputMonitorForm.Show();
            }
            else
            {
                inputMonitorForm.BringToFront();
            }
        }
        
        private void InputMonitorForm_ButtonsSelected(object? sender, EventArgs e)
        {
            // Get the selected buttons from the input monitor form
            if (inputMonitorForm != null && inputMonitorForm.HasSelectedButtons)
            {
                // Add the selected buttons to our MouseHook
                if (inputMonitorForm.FirstSideButtonData != null)
                {
                    AddCustomButtonFromInputData(inputMonitorForm.FirstSideButtonData, "FirstSideButton");
                }
                
                if (inputMonitorForm.SecondSideButtonData != null)
                {
                    AddCustomButtonFromInputData(inputMonitorForm.SecondSideButtonData, "SecondSideButton");
                }
                
                MessageBox.Show("Mouse buttons have been configured. They will now be mapped according to the selected profile.", 
                    "Button Configuration Complete", 
                    MessageBoxButtons.OK, 
                    MessageBoxIcon.Information);
            }
        }
        
        private void AddCustomButtonFromInputData(InputEventData data, string name)
        {
            if (data.Type == InputType.Mouse)
            {
                mouseHook.AddCustomButtonMapping(data.MessageType, data.ExtraInfo, (int)data.MouseData, name);
                Debug.WriteLine($"Added custom button from mouse input: {name}, Msg={data.MessageType:X}, ExtraInfo={data.ExtraInfo:X}, Data={data.MouseData:X}");
            }
            else if (data.Type == InputType.RawMouse)
            {
                mouseHook.AddCustomButtonMapping(0, data.ExtraInfo, data.ButtonFlags, name);
                Debug.WriteLine($"Added custom button from raw mouse input: {name}, ButtonFlags={data.ButtonFlags:X}, ExtraInfo={data.ExtraInfo:X}");
            }
            else if (data.Type == InputType.Keyboard)
            {
                // For keyboard-mapped buttons
                Debug.WriteLine($"Added keyboard mapping for mouse button: {name}, Key={data.VirtualKeyCode:X}");
                // Handle keyboard-mapped buttons
            }
        }
        
        private void OnToggleLearningMode(object? sender, EventArgs e)
        {
            if (learningModeItem != null)
            {
                learningModeItem.Checked = !learningModeItem.Checked;
                mouseHook.IsLearningMode = learningModeItem.Checked;
                
                if (mouseHook.IsLearningMode)
                {
                    // Clear previous button detections
                    detectedButtons.Clear();
                    
                    // Make sure hook is installed
                    if (!mouseHook.IsHookEnabled)
                    {
                        mouseHook.Install();
                    }
                    
                    ShowButtonLearningWindow();
                }
                else if (buttonLearningForm != null)
                {
                    buttonLearningForm.Close();
                    buttonLearningForm = null;
                }
                
                Debug.WriteLine($"Learning mode {(mouseHook.IsLearningMode ? "enabled" : "disabled")}");
            }
        }
        
        private void ShowButtonLearningWindow()
        {
            buttonLearningForm = new Form
            {
                Text = "Button Learning Mode - Press Mouse Buttons",
                Width = 600,
                Height = 400,
                StartPosition = FormStartPosition.CenterScreen
            };
            
            // Add list box to show detected buttons
            ListBox listBox = new ListBox
            {
                Dock = DockStyle.Fill,
                Font = new Font("Consolas", 10)
            };
            
            // Add instructions label
            Label label = new Label
            {
                Text = "Press your mouse buttons to identify them.\nDetected buttons will appear below.",
                Dock = DockStyle.Top,
                Height = 40,
                TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font("Segoe UI", 10)
            };
            
            // Add try advanced button
            Button advancedButton = new Button
            {
                Text = "Try Advanced Input Monitor",
                Dock = DockStyle.Bottom,
                Height = 30
            };
            
            advancedButton.Click += (s, e) => 
            {
                OnShowInputMonitor(null, EventArgs.Empty);
                buttonLearningForm!.Close();
            };
            
            // Add close button
            Button closeButton = new Button
            {
                Text = "Close",
                Dock = DockStyle.Bottom,
                Height = 30
            };
            
            closeButton.Click += (s, e) => 
            {
                buttonLearningForm!.Close();
                mouseHook.IsLearningMode = false;
                if (learningModeItem != null) 
                {
                    learningModeItem.Checked = false;
                }
            };
            
            buttonLearningForm.Controls.Add(listBox);
            buttonLearningForm.Controls.Add(label);
            buttonLearningForm.Controls.Add(advancedButton);
            buttonLearningForm.Controls.Add(closeButton);
            
            buttonLearningForm.FormClosing += (s, e) => 
            {
                mouseHook.IsLearningMode = false;
                if (learningModeItem != null) 
                {
                    learningModeItem.Checked = false;
                }
            };
            
            buttonLearningForm.Show();
        }
        
        private void OnToggleDebugLogging(object? sender, EventArgs e)
        {
            if (debugLoggingItem != null)
            {
                debugLoggingItem.Checked = !debugLoggingItem.Checked;
                mouseHook.EnableDebugLogging = debugLoggingItem.Checked;
                Debug.WriteLine($"Debug logging {(mouseHook.EnableDebugLogging ? "enabled" : "disabled")}");
            }
        }

        private void OnKeyMapProfileSelected(object? sender, EventArgs e)
        {
            if (sender is ToolStripMenuItem menuItem && menuItem.Tag is KeyMappingProfile profile)
            {
                // Update checked state
                disabledProfileItem!.Checked = (profile == KeyMappingProfile.Disabled);
                csgoProfileItem!.Checked = (profile == KeyMappingProfile.CSGO);
                owProfileItem!.Checked = (profile == KeyMappingProfile.OW);
                
                // Update profile
                mouseHook.CurrentProfile = profile;
                
                // Activate mouse hook if needed
                if (profile != KeyMappingProfile.Disabled && !mouseHook.IsHookEnabled)
                {
                    mouseHook.Install();
                }
                else if (profile == KeyMappingProfile.Disabled && mouseHook.IsHookEnabled && !mouseHook.IsLearningMode)
                {
                    mouseHook.Uninstall();
                }
                
                // Update tray text
                UpdateTrayText();
            }
        }

        private void KeyboardHook_KeyBlocked(object? sender, KeyEventArgs e)
        {
            // This could be logged or shown in a debug window
            Debug.WriteLine($"Blocked key: {e.KeyCode}");
        }

        private void TrayIcon_MouseClick(object? sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Right)
            {
                // Show the context menu on right-click
                trayMenu?.Show(Cursor.Position);
            }
        }

        private void OnToggleGameMode(object? sender, EventArgs e)
        {
            ToggleGameMode();
        }

        private void ToggleGameMode()
        {
            isGameModeActive = !isGameModeActive;

            if (isGameModeActive)
            {
                // Enable the keyboard hook
                keyboardHook.Install();
                if (trayIcon != null && activeIcon != null)
                {
                    trayIcon.Icon = activeIcon;
                    UpdateTrayText();
                    if (trayMenu != null)
                    {
                        ((ToolStripMenuItem)trayMenu.Items[0]).Text = "Disable Game Mode";
                    }
                }
            }
            else
            {
                // Disable the keyboard hook
                keyboardHook.Uninstall();
                if (trayIcon != null && inactiveIcon != null)
                {
                    trayIcon.Icon = inactiveIcon;
                    UpdateTrayText();
                    if (trayMenu != null)
                    {
                        ((ToolStripMenuItem)trayMenu.Items[0]).Text = "Enable Game Mode";
                    }
                }
            }
        }
        
        private void UpdateTrayText()
        {
            if (trayIcon != null)
            {
                string gameMode = isGameModeActive ? "Active" : "Inactive";
                string keyMode = mouseHook.CurrentProfile == KeyMappingProfile.Disabled 
                    ? "" 
                    : $" | {mouseHook.CurrentProfile}";
                
                trayIcon.Text = $"Game Mode - {gameMode}{keyMode}";
            }
        }

        private void OnExit(object? sender, EventArgs e)
        {
            // Clean up before exiting
            if (trayIcon != null)
            {
                trayIcon.Visible = false;
            }
            keyboardHook.Uninstall();
            mouseHook.Uninstall();
            
            // Close any open forms
            if (buttonLearningForm != null && !buttonLearningForm.IsDisposed)
            {
                buttonLearningForm.Close();
                buttonLearningForm = null;
            }
            
            if (inputMonitorForm != null && !inputMonitorForm.IsDisposed)
            {
                inputMonitorForm.Close();
                inputMonitorForm = null;
            }
            
            Application.Exit();
        }

        protected override void SetVisibleCore(bool value)
        {
            // Keep the form hidden
            base.SetVisibleCore(false);
        }
    }
}