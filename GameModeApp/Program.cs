using System;
using System.Threading;
using System.Windows.Forms;

namespace GameModeApp
{
    internal static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            
            // Generate icons on first run
            IconGenerator.GenerateIcons();
            
            // Make sure only one instance runs
            bool createdNew;
            using (Mutex mutex = new Mutex(true, "GameModeAppInstance", out createdNew))
            {
                if (createdNew)
                {
                    MainForm form = new MainForm();
                    Application.Run(form);
                }
                else
                {
                    MessageBox.Show("Game Mode is already running.", "Game Mode", 
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
        }
    }
}