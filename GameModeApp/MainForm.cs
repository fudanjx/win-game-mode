using System;
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
        private bool isGameModeActive = false;

        // Icons for active and inactive states
        private Icon? activeIcon;
        private Icon? inactiveIcon;

        public MainForm()
        {
            // We're creating UI manually, not using InitializeComponent
            
            // Initialize the keyboard hook
            keyboardHook = new KeyboardHook();
            keyboardHook.KeyBlocked += KeyboardHook_KeyBlocked;

            // Set up the tray icon and menu
            SetupTrayIcon();

            // Load icons
            LoadIcons();

            // Hide form on startup
            this.WindowState = FormWindowState.Minimized;
            this.ShowInTaskbar = false;
            this.FormBorderStyle = FormBorderStyle.SizableToolWindow;
            this.Opacity = 0;
            this.Size = new Size(0, 0);
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
            trayMenu.Items.Add("-"); // Separator
            trayMenu.Items.Add("Exit", null, OnExit);

            trayIcon = new NotifyIcon();
            trayIcon.Text = "Game Mode - Inactive";
            trayIcon.ContextMenuStrip = trayMenu;
            trayIcon.Visible = true;
            trayIcon.MouseClick += TrayIcon_MouseClick;
        }
        

        private void KeyboardHook_KeyBlocked(object? sender, KeyEventArgs e)
        {
            // Simplified key blocked handler - no action needed
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
                keyboardHook.Uninstall(); // First uninstall in case it was already installed
                keyboardHook.Install();   // Then reinstall it fresh
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
                trayIcon.Text = $"Game Mode - {gameMode}";
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
            
            Application.Exit();
        }

        protected override void SetVisibleCore(bool value)
        {
            // Keep the form hidden
            base.SetVisibleCore(false);
        }
    }
}