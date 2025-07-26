using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using System.Threading;
using System.ComponentModel;
using System.Diagnostics;
using System.Collections.Generic;

namespace AutoClicker
{
    public partial class AutoClickerForm : Form
    {
        // Import required Windows API functions
        [DllImport("user32.dll")]
        static extern void mouse_event(int dwFlags, int dx, int dy, int dwData, int dwExtraInfo);
        private const int MOUSEEVENTF_LEFTDOWN = 0x02;
        private const int MOUSEEVENTF_LEFTUP = 0x04;

        // Variables to control the auto-clicking behavior
        private bool isClicking = false;
        private Random random = new Random();
        private BackgroundWorker clickWorker = new BackgroundWorker();
        private Keys hotkey = Keys.F6;
        
        // Added new field for hold mode
        private bool holdModeEnabled = false;
        
        // Added new field for random additional hold time
        private int maxAdditionalHoldTime = 200;
        
        // Removed the 'required' modifier and made the fields non-nullable
        private GlobalKeyboardHook keyboardHook = null!;
        private NumericUpDown nudInterval = null!;
        private NumericUpDown nudRandomOffset = null!;
        private Button btnToggle = null!;
        private Label lblStatus = null!;
        private ComboBox cmbMouseButton = null!;
        private Label lblCurrentInterval = null!;
        private GroupBox groupSettings = null!;
        private Label lblInterval = null!;
        private Label lblRandomOffset = null!;
        private Label lblMouseButton = null!;
        private Label lblHotkey = null!;
        private ComboBox cmbHotkey = null!;
        
        // New controls for hold mode
        private CheckBox chkHoldMode = null!;
        private NumericUpDown nudHoldDuration = null!;
        private Label lblHoldDuration = null!;
        private Label lblMaxAdditionalHold = null!;
        private NumericUpDown nudMaxAdditionalHold = null!;
        
        // New status panel for better visibility
        private Panel statusPanel = null!;
        private Label lblClickCount = null!;
        private Label lblTimeElapsed = null!;
        
        // Add tooltip for status information
        private ToolTip statusTooltip = null!;
        
        // Variables to track statistics
        private int clickCount = 0;
        private DateTime startTime;
        
        // Add cancellation support for hold operations
        private volatile bool holdCancellationRequested = false;
        private ManualResetEvent holdCancellationEvent = new ManualResetEvent(false);

        public AutoClickerForm()
        {
            InitializeComponent();
            InitializeClickWorker();
            SetupKeyboardHook();
        }

        private void InitializeComponent()
        {
            // Form settings
            this.Text = "Auto Clicker";
            this.Size = new Size(350, 400); // Further increased size to prevent cutoff
            this.MinimumSize = new Size(350, 400); // Set minimum size to prevent resizing issues
            this.FormBorderStyle = FormBorderStyle.FixedSingle;
            this.MaximizeBox = false;
            this.StartPosition = FormStartPosition.CenterScreen;
            this.Icon = SystemIcons.Application;

            // Create group box for settings
            groupSettings = new GroupBox
            {
                Text = "Settings",
                Location = new Point(12, 12),
                Size = new Size(310, 210) // Increased height for additional control
            };
            this.Controls.Add(groupSettings);

            // Create labels
            lblInterval = new Label
            {
                Text = "Click Interval (ms):",
                Location = new Point(15, 25),
                Size = new Size(120, 20)
            };
            groupSettings.Controls.Add(lblInterval);

            lblRandomOffset = new Label
            {
                Text = "Random Offset (ms):",
                Location = new Point(15, 55),
                Size = new Size(120, 20)
            };
            groupSettings.Controls.Add(lblRandomOffset);

            lblMouseButton = new Label
            {
                Text = "Mouse Button:",
                Location = new Point(15, 85),
                Size = new Size(120, 20)
            };
            groupSettings.Controls.Add(lblMouseButton);

            lblHotkey = new Label
            {
                Text = "Start/Stop Hotkey:",
                Location = new Point(15, 115),
                Size = new Size(120, 20)
            };
            groupSettings.Controls.Add(lblHotkey);
            
            // Add Hold Mode checkbox
            chkHoldMode = new CheckBox
            {
                Text = "Hold Mode",
                Location = new Point(15, 145),
                Size = new Size(120, 20),
                Checked = false
            };
            chkHoldMode.CheckedChanged += ChkHoldMode_CheckedChanged;
            groupSettings.Controls.Add(chkHoldMode);
            
            // Add Hold Duration label and control - renamed to make purpose clearer
            lblHoldDuration = new Label
            {
                Text = "Min Hold Duration (ms):",
                Location = new Point(15, 145),
                Size = new Size(120, 20),
                Visible = false
            };
            groupSettings.Controls.Add(lblHoldDuration);
            
            // Add Max Additional Hold Time label
            lblMaxAdditionalHold = new Label
            {
                Text = "Max Extra Hold (ms):",
                Location = new Point(15, 175),
                Size = new Size(120, 20),
                Visible = false
            };
            groupSettings.Controls.Add(lblMaxAdditionalHold);

            // Create interval numeric up/down
            nudInterval = new NumericUpDown
            {
                Location = new Point(150, 23),
                Size = new Size(140, 20),
                Minimum = 10,
                Maximum = 10000,
                Value = 500,
                Increment = 10
            };
            groupSettings.Controls.Add(nudInterval);

            // Create random offset numeric up/down
            nudRandomOffset = new NumericUpDown
            {
                Location = new Point(150, 53),
                Size = new Size(140, 20),
                Minimum = 0,
                Maximum = 1000,
                Value = 100
            };
            groupSettings.Controls.Add(nudRandomOffset);

            // Create mouse button combo box
            cmbMouseButton = new ComboBox
            {
                Location = new Point(150, 83),
                Size = new Size(140, 20),
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            cmbMouseButton.Items.AddRange(new object[] { "Left Button", "Right Button", "Middle Button" });
            cmbMouseButton.SelectedIndex = 0;
            groupSettings.Controls.Add(cmbMouseButton);

            // Create hotkey combo box
            cmbHotkey = new ComboBox
            {
                Location = new Point(150, 113),
                Size = new Size(140, 20),
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            cmbHotkey.Items.AddRange(new object[] { "F6", "F7", "F8", "F9", "F10", "F11", "F12" });
            cmbHotkey.SelectedIndex = 0;
            cmbHotkey.SelectedIndexChanged += CmbHotkey_SelectedIndexChanged;
            groupSettings.Controls.Add(cmbHotkey);
            
            // Add Hold Duration numeric up/down - renamed to make purpose clearer
            nudHoldDuration = new NumericUpDown
            {
                Location = new Point(150, 143),
                Size = new Size(140, 20),
                Minimum = 10,
                Maximum = 10000,
                Value = 200,
                Increment = 10,
                Visible = false
            };
            groupSettings.Controls.Add(nudHoldDuration);
            
            // Add Max Additional Hold Time numeric up/down
            nudMaxAdditionalHold = new NumericUpDown
            {
                Location = new Point(150, 173),
                Size = new Size(140, 20),
                Minimum = 0,
                Maximum = 5000,
                Value = 200,
                Increment = 10,
                Visible = false
            };
            nudMaxAdditionalHold.ValueChanged += NudMaxAdditionalHold_ValueChanged;
            groupSettings.Controls.Add(nudMaxAdditionalHold);

            // Create toggle button
            btnToggle = new Button
            {
                Text = "Start (F6)",
                Location = new Point(12, 230),
                Size = new Size(310, 30)
            };
            // Fix: Changed BtnToggle_Click to the correct method name
            btnToggle.Click += new EventHandler(BtnToggle_Click);
            this.Controls.Add(btnToggle);

            // Create status panel with a prominent border and more space
            statusPanel = new Panel
            {
                Location = new Point(12, 270),
                Size = new Size(310, 80), // Increased height to prevent cutoff
                BorderStyle = BorderStyle.Fixed3D,
                BackColor = Color.FromArgb(240, 240, 240),
                Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right // Anchor to bottom of form
            };
            this.Controls.Add(statusPanel);

            // Create status label inside the panel
            lblStatus = new Label
            {
                Text = "Status: Idle",
                Location = new Point(10, 5),
                Size = new Size(290, 20),
                Font = new Font("Microsoft Sans Serif", 8.25f, FontStyle.Bold),
                ForeColor = Color.Navy
            };
            statusPanel.Controls.Add(lblStatus);

            // Make current interval label more visible inside the panel
            lblCurrentInterval = new Label
            {
                Text = "Waiting to start...",
                Location = new Point(10, 25),
                Size = new Size(290, 20),
                BackColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle,
                Font = new Font("Consolas", 9f, FontStyle.Bold),
                AutoEllipsis = true // Add ellipsis if text is too long
            };
            statusPanel.Controls.Add(lblCurrentInterval);
            
            // Add click counter
            lblClickCount = new Label
            {
                Text = "Clicks: 0",
                Location = new Point(10, 50), // Increased Y position
                Size = new Size(140, 20),
                Font = new Font("Microsoft Sans Serif", 8.25f, FontStyle.Regular)
            };
            statusPanel.Controls.Add(lblClickCount);
            
            // Add elapsed time
            lblTimeElapsed = new Label
            {
                Text = "Time: 00:00:00",
                Location = new Point(160, 50), // Increased Y position
                Size = new Size(140, 20),
                Font = new Font("Microsoft Sans Serif", 8.25f, FontStyle.Regular),
                TextAlign = ContentAlignment.TopRight
            };
            statusPanel.Controls.Add(lblTimeElapsed);
            
            // Initialize tooltip for status information
            statusTooltip = new ToolTip();
            statusTooltip.SetToolTip(lblCurrentInterval, "Current click interval and hold duration");
            statusTooltip.SetToolTip(lblClickCount, "Total number of clicks performed");
            statusTooltip.SetToolTip(lblTimeElapsed, "Total time elapsed since starting");
            
            // Add version label at the very bottom of the form
            Label lblVersion = new Label
            {
                Text = "Auto Clicker v1.0",
                Location = new Point(12, 355), // Position at bottom of form
                Size = new Size(310, 15),
                Font = new Font("Microsoft Sans Serif", 7f, FontStyle.Regular),
                ForeColor = Color.Gray,
                TextAlign = ContentAlignment.MiddleCenter
            };
            this.Controls.Add(lblVersion);
        }

        // Store the additional hold time value
        private void NudMaxAdditionalHold_ValueChanged(object? sender, EventArgs e)
        {
            maxAdditionalHoldTime = (int)nudMaxAdditionalHold.Value;
        }

        // New event handler for hold mode checkbox
        private void ChkHoldMode_CheckedChanged(object? sender, EventArgs e)
        {
            holdModeEnabled = chkHoldMode.Checked;
            lblHoldDuration.Visible = holdModeEnabled;
            nudHoldDuration.Visible = holdModeEnabled;
            lblMaxAdditionalHold.Visible = holdModeEnabled;
            nudMaxAdditionalHold.Visible = holdModeEnabled;
        }

        private void InitializeClickWorker()
        {
            clickWorker.WorkerSupportsCancellation = true;
            clickWorker.DoWork += ClickWorker_DoWork;
            clickWorker.RunWorkerCompleted += ClickWorker_RunWorkerCompleted;
        }

        private void SetupKeyboardHook()
        {
            keyboardHook = new GlobalKeyboardHook();
            keyboardHook.KeyDown += KeyboardHook_KeyDown;
            keyboardHook.HookedKeys.Add(hotkey);
        }

        private void CmbHotkey_SelectedIndexChanged(object? sender, EventArgs e)
        {
            // Update hotkey based on selection
            keyboardHook.HookedKeys.Remove(hotkey);
            
            switch (cmbHotkey.SelectedIndex)
            {
                case 0: hotkey = Keys.F6; break;
                case 1: hotkey = Keys.F7; break;
                case 2: hotkey = Keys.F8; break;
                case 3: hotkey = Keys.F9; break;
                case 4: hotkey = Keys.F10; break;
                case 5: hotkey = Keys.F11; break;
                case 6: hotkey = Keys.F12; break;
            }
            
            keyboardHook.HookedKeys.Add(hotkey);
            btnToggle.Text = $"Start ({hotkey})";
        }

        private void KeyboardHook_KeyDown(object? sender, KeyEventArgs e)
        {
            if (e.KeyCode == hotkey)
            {
                // Signal cancellation if a hold operation is in progress
                if (isClicking && holdModeEnabled)
                {
                    holdCancellationRequested = true;
                    holdCancellationEvent.Set();
                }
                
                ToggleClicking();
                e.Handled = true;
            }
        }

        private void BtnToggle_Click(object? sender, EventArgs e)
        {
            ToggleClicking();
        }

        private void ToggleClicking()
        {
            isClicking = !isClicking;
            
            if (isClicking)
            {
                // Reset cancellation flag when starting
                holdCancellationRequested = false;
                holdCancellationEvent.Reset();
                
                btnToggle.Text = $"Stop ({hotkey})";
                lblStatus.Text = "Status: Running";
                lblCurrentInterval.Text = "Starting...";
                lblClickCount.Text = "Clicks: 0";
                
                // Reset statistics
                clickCount = 0;
                startTime = DateTime.Now;
                
                // Start timer for elapsed time
                System.Windows.Forms.Timer timer = new System.Windows.Forms.Timer();
                timer.Interval = 1000; // Update every second
                timer.Tick += (s, e) => 
                {
                    if (isClicking)
                    {
                        TimeSpan elapsed = DateTime.Now - startTime;
                        lblTimeElapsed.Text = $"Time: {elapsed.Hours:00}:{elapsed.Minutes:00}:{elapsed.Seconds:00}";
                    }
                    else
                    {
                        // Fix: Add null check before casting
                        var timerObj = s as System.Windows.Forms.Timer;
                        if (timerObj != null)
                        {
                            timerObj.Stop();
                            timerObj.Dispose();
                        }
                    }
                };
                timer.Start();
                
                // Disable controls while clicking
                nudInterval.Enabled = false;
                nudRandomOffset.Enabled = false;
                cmbMouseButton.Enabled = false;
                cmbHotkey.Enabled = false;
                
                // Disable hold mode controls while clicking
                chkHoldMode.Enabled = false;
                nudHoldDuration.Enabled = false;
                nudMaxAdditionalHold.Enabled = false;
                
                if (!clickWorker.IsBusy)
                {
                    clickWorker.RunWorkerAsync();
                }
            }
            else
            {
                // Signal cancellation when stopping
                holdCancellationRequested = true;
                holdCancellationEvent.Set();
                
                btnToggle.Text = $"Start ({hotkey})";
                lblStatus.Text = "Status: Idle";
                lblCurrentInterval.Text = "Stopping...";
                
                // Enable controls when stopped
                nudInterval.Enabled = true;
                nudRandomOffset.Enabled = true;
                cmbMouseButton.Enabled = true;
                cmbHotkey.Enabled = true;
                
                // Enable hold mode controls when stopped
                chkHoldMode.Enabled = true;
                nudHoldDuration.Enabled = true;
                nudMaxAdditionalHold.Enabled = true;
                
                if (clickWorker.IsBusy)
                {
                    clickWorker.CancelAsync();
                }
            }
        }

        private void ClickWorker_DoWork(object? sender, DoWorkEventArgs e)
        {
            // Fix: Add proper null check
            BackgroundWorker? worker = sender as BackgroundWorker;
            
            // Fix: Check for null before accessing CancellationPending
            if (worker == null)
                return;
                
            while (!worker.CancellationPending)
            {
                // Reset the cancellation event at the start of each click cycle
                if (holdCancellationRequested)
                {
                    holdCancellationRequested = false;
                    holdCancellationEvent.Reset();
                }
                
                // Calculate random interval
                int baseInterval = (int)nudInterval.Value;
                int maxOffset = (int)nudRandomOffset.Value;
                int offset = maxOffset > 0 ? random.Next(-maxOffset, maxOffset + 1) : 0;
                int actualInterval = Math.Max(10, baseInterval + offset);
                
                // For hold mode, get minimum hold duration
                int minHoldDuration = (int)nudHoldDuration.Value;
                int actualHoldDuration = minHoldDuration;
                
                // Add random additional time to hold duration
                if (holdModeEnabled)
                {
                    // Add random additional hold time between 0 and maxAdditionalHoldTime
                    int additionalTime = random.Next(0, maxAdditionalHoldTime + 1);
                    actualHoldDuration += additionalTime;
                }

                // Update UI to show current interval and hold duration with more detailed information
                this.Invoke((MethodInvoker)delegate
                {
                    try
                    {
                        if (holdModeEnabled)
                        {
                            lblCurrentInterval.Text = $"⏱️ Interval: {actualInterval} ms | ⌛ Hold: {actualHoldDuration} ms";
                        }
                        else
                        {
                            lblCurrentInterval.Text = $"⏱️ Interval: {actualInterval} ms";
                        }
                        
                        // Also update the tooltip with the same information for reference
                        statusTooltip.SetToolTip(lblCurrentInterval, lblCurrentInterval.Text);
                        
                        // Update click counter
                        clickCount++;
                        lblClickCount.Text = $"Clicks: {clickCount}";
                        
                        // Flash the label to make it more noticeable when updated
                        lblCurrentInterval.BackColor = Color.LightGreen;
                        Application.DoEvents();
                        Thread.Sleep(50);
                        lblCurrentInterval.BackColor = Color.White;
                    }
                    catch (Exception ex)
                    {
                        // Handle any UI update errors silently to prevent crashes
                        Debug.WriteLine($"Error updating UI: {ex.Message}");
                    }
                });

                // Perform click based on selected button
                this.Invoke((MethodInvoker)delegate
                {
                    // Fix: Check for null after proper null check above
                    if (worker.CancellationPending || holdCancellationRequested)
                        return;
                        
                    switch (cmbMouseButton.SelectedIndex)
                    {
                        case 0: // Left button
                            if (holdModeEnabled)
                                SimulateLeftHold(actualHoldDuration);
                            else
                                SimulateLeftClick();
                            break;
                        case 1: // Right button
                            if (holdModeEnabled)
                                SimulateRightHold(actualHoldDuration);
                            else
                                SimulateRightClick();
                            break;
                        case 2: // Middle button
                            if (holdModeEnabled)
                                SimulateMiddleHold(actualHoldDuration);
                            else
                                SimulateMiddleClick();
                            break;
                    }
                });

                // Check again for cancellation before waiting for the next interval
                if (worker.CancellationPending || holdCancellationRequested)
                    break;
                    
                // Wait for the calculated interval
                Thread.Sleep(actualInterval);
            }
        }

        private void ClickWorker_RunWorkerCompleted(object? sender, RunWorkerCompletedEventArgs e)
        {
            isClicking = false;
            btnToggle.Text = $"Start ({hotkey})";
            lblStatus.Text = "Status: Idle";
            lblCurrentInterval.Text = "Waiting to start...";
            lblTimeElapsed.Text = "Time: 00:00:00";
        }

        private void SimulateLeftClick()
        {
            // Send left mouse button down and up events
            mouse_event(MOUSEEVENTF_LEFTDOWN, 0, 0, 0, 0);
            Thread.Sleep(10 + random.Next(0, 15)); // Add random delay between down and up for realism
            mouse_event(MOUSEEVENTF_LEFTUP, 0, 0, 0, 0);
        }

        private void SimulateRightClick()
        {
            // Send right mouse button down and up events
            mouse_event(0x0008, 0, 0, 0, 0);
            Thread.Sleep(10 + random.Next(0, 15));
            mouse_event(0x0010, 0, 0, 0, 0);
        }

        private void SimulateMiddleClick()
        {
            // Send middle mouse button down and up events
            mouse_event(0x0020, 0, 0, 0, 0);
            Thread.Sleep(10 + random.Next(0, 15));
            mouse_event(0x0040, 0, 0, 0, 0);
        }

        // New methods for holding mouse buttons with cancellation support
        private void SimulateLeftHold(int holdDuration)
        {
            // Send left mouse button down
            mouse_event(MOUSEEVENTF_LEFTDOWN, 0, 0, 0, 0);
            
            try
            {
                // Wait for either the hold duration to complete or cancellation
                holdCancellationEvent.WaitOne(holdDuration);
            }
            finally
            {
                // Always release the button, even if cancelled
                mouse_event(MOUSEEVENTF_LEFTUP, 0, 0, 0, 0);
            }
        }

        private void SimulateRightHold(int holdDuration)
        {
            // Send right mouse button down
            mouse_event(0x0008, 0, 0, 0, 0);
            
            try
            {
                // Wait for either the hold duration to complete or cancellation
                holdCancellationEvent.WaitOne(holdDuration);
            }
            finally
            {
                // Always release the button, even if cancelled
                mouse_event(0x0010, 0, 0, 0, 0);
            }
        }

        private void SimulateMiddleHold(int holdDuration)
        {
            // Send middle mouse button down
            mouse_event(0x0020, 0, 0, 0, 0);
            
            try
            {
                // Wait for either the hold duration to complete or cancellation
                holdCancellationEvent.WaitOne(holdDuration);
            }
            finally
            {
                // Always release the button, even if cancelled
                mouse_event(0x0040, 0, 0, 0, 0);
            }
        }

        // Clean up resources when form is closed
        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            // Make sure to release any held buttons
            holdCancellationRequested = true;
            holdCancellationEvent.Set();
            
            if (isClicking && clickWorker.IsBusy)
            {
                clickWorker.CancelAsync();
            }
            
            holdCancellationEvent.Dispose();
            base.OnFormClosing(e);
        }
    }

    // Helper class for global keyboard hook
    public class GlobalKeyboardHook
    {
        [DllImport("user32.dll")]
        private static extern int SetWindowsHookEx(int idHook, KeyboardHookProc callback, IntPtr hInstance, uint threadId);

        [DllImport("user32.dll")]
        private static extern bool UnhookWindowsHookEx(int idHook);

        [DllImport("user32.dll")]
        private static extern int CallNextHookEx(int idHook, int nCode, int wParam, ref KeyboardHookStruct lParam);

        [DllImport("kernel32.dll")]
        private static extern IntPtr GetModuleHandle(string lpModuleName);

        private const int WH_KEYBOARD_LL = 13;
        private const int WM_KEYDOWN = 0x0100;
        private const int WM_KEYUP = 0x0101;
        private const int WM_SYSKEYDOWN = 0x0104;
        private const int WM_SYSKEYUP = 0x0105;

        private KeyboardHookProc hookDelegate;
        private int hookId = 0;

        // Fixed nullability warnings for events
        public event KeyEventHandler? KeyDown;
        public event KeyEventHandler? KeyUp;

        public List<Keys> HookedKeys = new List<Keys>();

        public GlobalKeyboardHook()
        {
            hookDelegate = HookCallback;
            hookId = SetHook(hookDelegate);
        }

        ~GlobalKeyboardHook()
        {
            UnhookWindowsHookEx(hookId);
        }

        private int SetHook(KeyboardHookProc proc)
        {
            using (Process curProcess = Process.GetCurrentProcess())
            using (ProcessModule curModule = curProcess.MainModule)
            {
                return SetWindowsHookEx(WH_KEYBOARD_LL, proc, GetModuleHandle(curModule.ModuleName), 0);
            }
        }

        private int HookCallback(int nCode, int wParam, ref KeyboardHookStruct lParam)
        {
            if (nCode >= 0 && (wParam == WM_KEYDOWN || wParam == WM_SYSKEYDOWN))
            {
                Keys key = (Keys)lParam.vkCode;
                if (HookedKeys.Contains(key))
                {
                    KeyEventArgs args = new KeyEventArgs(key);
                    KeyDown?.Invoke(this, args);
                    
                    if (args.Handled)
                        return 1;
                }
            }
            else if (nCode >= 0 && (wParam == WM_KEYUP || wParam == WM_SYSKEYUP))
            {
                Keys key = (Keys)lParam.vkCode;
                if (HookedKeys.Contains(key))
                {
                    KeyEventArgs args = new KeyEventArgs(key);
                    KeyUp?.Invoke(this, args);
                    
                    if (args.Handled)
                        return 1;
                }
            }
            
            return CallNextHookEx(hookId, nCode, wParam, ref lParam);
        }

        public delegate int KeyboardHookProc(int code, int wParam, ref KeyboardHookStruct lParam);

        public struct KeyboardHookStruct
        {
            public int vkCode;
            public int scanCode;
            public int flags;
            public int time;
            public int dwExtraInfo;
        }
    }

    static class Program
    {
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new AutoClickerForm());
        }
    }
}
