using System;
using System.Diagnostics;
using System.Windows.Forms;

namespace GameModeApp
{
    // Custom NativeWindow implementation to handle WM_INPUT messages
    public class RawInputWindow : NativeWindow
    {
        private const int WM_INPUT = 0x00FF;
        private InputMonitor _inputMonitor;
        
        public RawInputWindow(InputMonitor inputMonitor)
        {
            _inputMonitor = inputMonitor;
        }
        
        protected override void WndProc(ref Message m)
        {
            // Special handling for WM_INPUT messages which might contain Razer-specific data
            if (m.Msg == WM_INPUT)
            {
                // We have our own processing in InputMonitor, but this provides an additional chance
                // to catch input messages that might otherwise be missed
                // Especially important for gaming peripherals that use custom input channels
                
                if (_inputMonitor.EnableLogging)
                {
                    Debug.WriteLine($"Raw input message received in RawInputWindow: WParam={m.WParam.ToInt32():X}, LParam={m.LParam.ToInt64():X}");
                }
                
                // Process the raw input directly
                ProcessRawInput(m.LParam);
            }
            
            base.WndProc(ref m);
        }
        
        private void ProcessRawInput(IntPtr lParam)
        {
            // This method provides an additional detection path for Razer side buttons
            // that might be missed by the standard input processing

            try
            {
                // Tell the input monitor about this raw input message
                // For now, we're using a simple approach - if we detect any raw input activity,
                // we'll raise an event that will be displayed in the UI
                
                if (_inputMonitor.EnableLogging)
                {
                    Debug.WriteLine($"RawInputWindow detected input: {lParam.ToInt64():X}");
                }
                
                // We can't directly invoke the RawInputEvent from outside InputMonitor
                // Instead, we'll just log the detection for now
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error processing raw input: {ex.Message}");
            }
        }
    }
}