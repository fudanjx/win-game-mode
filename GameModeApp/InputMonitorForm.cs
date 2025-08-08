using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace GameModeApp
{
    public class InputMonitorForm : Form
    {
        private ListBox? inputListBox;
        private Button? clearButton;
        private Button? applyButton;
        private CheckBox? enableKeyboardCheckbox;
        private CheckBox? enableMouseCheckbox;
        private CheckBox? enableRawInputCheckbox;
        private CheckBox? loggingCheckbox;
        private Label? statusLabel;
        
        private InputMonitor? inputMonitor;
        
        // Flag to indicate if we're watching for a specific button
        private bool isWatchingForMouseButton = false;
        private bool isWatchingForKeyboardButton = false;
        
        // Maximum items to display in the list box
        private const int MaxListItems = 1000;
        
        // The selected side buttons to map
        public InputEventData? FirstSideButtonData { get; private set; }
        public InputEventData? SecondSideButtonData { get; private set; }
        public bool HasSelectedButtons => FirstSideButtonData != null;
        
        // Event raised when buttons are selected
        public event EventHandler? ButtonsSelected;
        
        public InputMonitorForm()
        {
            InitializeComponents();
            
            // Create input monitor
            inputMonitor = new InputMonitor
            {
                CaptureKeyboard = enableKeyboardCheckbox?.Checked ?? true,
                CaptureMouse = enableMouseCheckbox?.Checked ?? true,
                CaptureRawInput = enableRawInputCheckbox?.Checked ?? true,
                EnableLogging = loggingCheckbox?.Checked ?? true
            };
            
            // Subscribe to events
            if (inputMonitor != null)
            {
                inputMonitor.KeyboardEvent += InputMonitor_KeyboardEvent;
                inputMonitor.MouseEvent += InputMonitor_MouseEvent;
                inputMonitor.RawInputEvent += InputMonitor_RawInputEvent;
            }
        }
        
        private void InitializeComponents()
        {
            this.Text = "Input Monitor - Detect Mouse Buttons";
            this.Width = 800;
            this.Height = 600;
            this.FormBorderStyle = FormBorderStyle.Sizable;
            this.StartPosition = FormStartPosition.CenterScreen;
            
            // Create main layout panel
            TableLayoutPanel mainPanel = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 3,
                Padding = new Padding(10)
            };
            
            // Create control panel
            Panel controlPanel = new Panel
            {
                Dock = DockStyle.Fill,
                Height = 100
            };
            
            // Create options panel
            TableLayoutPanel optionsPanel = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                ColumnCount = 2,
                RowCount = 2,
                Height = 60,
                Padding = new Padding(5)
            };
            
            // Create checkboxes
            enableKeyboardCheckbox = new CheckBox
            {
                Text = "Capture Keyboard",
                Checked = true
            };
            
            enableMouseCheckbox = new CheckBox
            {
                Text = "Capture Mouse",
                Checked = true
            };
            
            enableRawInputCheckbox = new CheckBox
            {
                Text = "Capture Raw Input",
                Checked = true
            };
            
            loggingCheckbox = new CheckBox
            {
                Text = "Debug Logging",
                Checked = true
            };
            
            // Add checkboxes to options panel
            optionsPanel.Controls.Add(enableKeyboardCheckbox, 0, 0);
            optionsPanel.Controls.Add(enableMouseCheckbox, 1, 0);
            optionsPanel.Controls.Add(enableRawInputCheckbox, 0, 1);
            optionsPanel.Controls.Add(loggingCheckbox, 1, 1);
            
            // Create button panel
            TableLayoutPanel buttonPanel = new TableLayoutPanel
            {
                Dock = DockStyle.Bottom,
                ColumnCount = 5,
                RowCount = 1,
                Height = 40,
                Padding = new Padding(5)
            };
            
            // Create buttons
            clearButton = new Button
            {
                Text = "Clear",
                Dock = DockStyle.Fill
            };
            
            Button startButton = new Button
            {
                Text = "Start Monitor",
                Dock = DockStyle.Fill
            };
            
            Button stopButton = new Button
            {
                Text = "Stop Monitor",
                Dock = DockStyle.Fill
            };
            
            Button identifyButton = new Button
            {
                Text = "Identify Side Buttons",
                Dock = DockStyle.Fill
            };
            
            applyButton = new Button
            {
                Text = "Apply Button Selection",
                Dock = DockStyle.Fill,
                Enabled = false
            };
            
            // Add buttons to panel
            buttonPanel.Controls.Add(clearButton, 0, 0);
            buttonPanel.Controls.Add(startButton, 1, 0);
            buttonPanel.Controls.Add(stopButton, 2, 0);
            buttonPanel.Controls.Add(identifyButton, 3, 0);
            buttonPanel.Controls.Add(applyButton, 4, 0);
            
            // Set column sizing
            buttonPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20));
            buttonPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20));
            buttonPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20));
            buttonPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20));
            buttonPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20));
            
            // Add button and options panels to control panel
            controlPanel.Controls.Add(buttonPanel);
            controlPanel.Controls.Add(optionsPanel);
            
            // Create status label
            statusLabel = new Label
            {
                Text = "Ready",
                Dock = DockStyle.Bottom,
                TextAlign = ContentAlignment.MiddleLeft,
                Height = 20
            };
            
            // Create list box
            inputListBox = new ListBox
            {
                Dock = DockStyle.Fill,
                Font = new Font("Consolas", 9),
                IntegralHeight = false,
                HorizontalScrollbar = true,
                SelectionMode = SelectionMode.MultiExtended
            };
            
            // Add controls to main panel
            mainPanel.Controls.Add(controlPanel);
            mainPanel.Controls.Add(inputListBox);
            mainPanel.Controls.Add(statusLabel);
            
            // Set row sizing
            mainPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 100));
            mainPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            mainPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 20));
            
            // Add main panel to form
            this.Controls.Add(mainPanel);
            
            // Wire up events
            clearButton.Click += (s, e) => inputListBox.Items.Clear();
            startButton.Click += (s, e) => StartMonitoring();
            stopButton.Click += (s, e) => StopMonitoring();
            identifyButton.Click += (s, e) => StartButtonIdentification();
            applyButton.Click += (s, e) => ApplyButtonSelection();
            
            inputListBox.SelectedIndexChanged += (s, e) => 
            {
                applyButton.Enabled = inputListBox.SelectedIndices.Count > 0 && inputListBox.SelectedIndices.Count <= 2;
            };
            
            this.FormClosing += (s, e) => StopMonitoring();
        }
        
        private void StartMonitoring()
        {
            // Update settings from UI
            if (inputMonitor != null)
            {
                inputMonitor.CaptureKeyboard = enableKeyboardCheckbox?.Checked ?? true;
                inputMonitor.CaptureMouse = enableMouseCheckbox?.Checked ?? true;
                inputMonitor.CaptureRawInput = enableRawInputCheckbox?.Checked ?? true;
                inputMonitor.EnableLogging = loggingCheckbox?.Checked ?? true;
            }
            
            // Start the monitor
            inputMonitor?.Start();
            
            // Update status
            UpdateStatus("Monitoring input...");
        }
        
        private void StopMonitoring()
        {
            // Stop the monitor
            inputMonitor?.Stop();
            
            // Reset watching state
            isWatchingForMouseButton = false;
            isWatchingForKeyboardButton = false;
            
            // Update status
            UpdateStatus("Monitoring stopped");
        }
        
        private void StartButtonIdentification()
        {
            // Clear existing data
            inputListBox?.Items.Clear();
            FirstSideButtonData = null;
            SecondSideButtonData = null;
            
            // Set watching state
            isWatchingForMouseButton = true;
            
            // Start monitoring
            StartMonitoring();
            
            // Update status
            UpdateStatus("Press your mouse side buttons one at a time to identify them");
        }
        
        private void ApplyButtonSelection()
        {
            // Get selected items
            if (inputListBox?.SelectedIndices.Count > 0)
            {
                List<InputEventData> selectedData = new List<InputEventData>();
                
                foreach (int index in inputListBox!.SelectedIndices)
                {
                    if (inputListBox!.Items[index] is InputEventData data)
                    {
                        selectedData.Add(data);
                    }
                }
                
                // Set first and second button data
                if (selectedData.Count >= 1)
                    FirstSideButtonData = selectedData[0];
                    
                if (selectedData.Count >= 2)
                    SecondSideButtonData = selectedData[1];
                    
                // Raise event
                if (HasSelectedButtons)
                {
                    ButtonsSelected?.Invoke(this, EventArgs.Empty);
                    
                    // Update status
                    UpdateStatus("Buttons selected: " + FirstSideButtonData?.Button + 
                               (SecondSideButtonData != null ? ", " + SecondSideButtonData?.Button : ""));
                }
                else
                {
                    UpdateStatus("Please select at least one button");
                }
            }
        }
        
        private void InputMonitor_KeyboardEvent(KeyboardState state)
        {
            if (this.IsDisposed) return;
            
            // Add to list box
            this.Invoke((MethodInvoker)delegate
            {
                if (isWatchingForKeyboardButton)
                {
                    // Format a user-friendly description
                    string keyName = ((Keys)state.VirtualKeyCode).ToString();
                    string direction = state.IsKeyDown ? "Pressed" : "Released";
                    
                    // Create input event data
                    InputEventData data = new InputEventData
                    {
                        Type = InputType.Keyboard,
                        VirtualKeyCode = state.VirtualKeyCode,
                        ScanCode = state.ScanCode,
                        Flags = state.Flags,
                        ExtraInfo = state.ExtraInfo.ToInt64(),
                        MessageType = state.MessageType,
                        TimeStamp = DateTime.Now,
                        Button = keyName,
                        RawData = $"VK:{state.VirtualKeyCode:X}, SC:{state.ScanCode:X}"
                    };
                    
                    // Add to list
                    AddItemToList(data);
                }
            });
        }
        
        private void InputMonitor_MouseEvent(MouseState state)
        {
            if (this.IsDisposed) return;
            
            // Add to list box
            this.Invoke((MethodInvoker)delegate
            {
                if (isWatchingForMouseButton && state.IsButtonDown)
                {
                    // Create input event data
                    InputEventData data = new InputEventData
                    {
                        Type = InputType.Mouse,
                        Button = state.ButtonPressed,
                        XButton = state.XButton,
                        Flags = state.Flags,
                        MouseData = state.Data,
                        ExtraInfo = state.ExtraInfo.ToInt64(),
                        MessageType = state.MessageType,
                        TimeStamp = DateTime.Now,
                        RawData = $"Data:{state.Data:X}, XButton:{state.XButton}"
                    };
                    
                    // Add to list
                    AddItemToList(data);
                }
            });
        }
        
        private void InputMonitor_RawInputEvent(RawInputData data)
        {
            if (this.IsDisposed) return;
            
            // Add to list box if it's a mouse button press
            this.Invoke((MethodInvoker)delegate
            {
                if (isWatchingForMouseButton && data.DeviceType == "Mouse" && data.ButtonFlags != 0)
                {
                    // Create input event data
                    InputEventData eventData = new InputEventData
                    {
                        Type = InputType.RawMouse,
                        Flags = data.Flags,
                        ButtonFlags = data.ButtonFlags,
                        ExtraInfo = data.ExtraData,
                        TimeStamp = DateTime.Now,
                        RawData = data.RawData,
                        Button = GetButtonNameFromRawFlags(data.ButtonFlags)
                    };
                    
                    // Add to list
                    AddItemToList(eventData);
                }
            });
        }
        
        private string GetButtonNameFromRawFlags(ushort buttonFlags)
        {
            if ((buttonFlags & 0x0001) != 0) return "Left Down";
            if ((buttonFlags & 0x0002) != 0) return "Left Up";
            if ((buttonFlags & 0x0004) != 0) return "Right Down";
            if ((buttonFlags & 0x0008) != 0) return "Right Up";
            if ((buttonFlags & 0x0010) != 0) return "Middle Down";
            if ((buttonFlags & 0x0020) != 0) return "Middle Up";
            if ((buttonFlags & 0x0040) != 0) return "XButton1 Down";
            if ((buttonFlags & 0x0080) != 0) return "XButton1 Up";
            if ((buttonFlags & 0x0100) != 0) return "XButton2 Down";
            if ((buttonFlags & 0x0200) != 0) return "XButton2 Up";
            if ((buttonFlags & 0x0400) != 0) return "Wheel";
            if ((buttonFlags & 0x0800) != 0) return "HWheel";
            
            return $"Unknown ({buttonFlags:X})";
        }
        
        private void AddItemToList(InputEventData data)
        {
            // Add to beginning for most recent at top
            if (inputListBox?.Items != null)
            {
                inputListBox.Items.Insert(0, data);
            }
            
            // Keep list from growing too large
            if (inputListBox?.Items != null && inputListBox.Items.Count > MaxListItems)
            {
                inputListBox.Items.RemoveAt(inputListBox.Items.Count - 1);
            }
            
            // If this is a mouse button, highlight the entry
            if (data.Type == InputType.Mouse || 
                (data.Type == InputType.RawMouse && data.Button.Contains("Down")))
            {
                if (inputListBox != null)
                {
                    inputListBox.SelectedIndex = 0;
                }
            }
        }
        
        private void UpdateStatus(string message)
        {
            this.Invoke((MethodInvoker)delegate
            {
                if (statusLabel != null)
                {
                    statusLabel.Text = message;
                }
            });
        }
        
        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            base.OnFormClosed(e);
            
            // Clean up
            StopMonitoring();
        }
    }
}