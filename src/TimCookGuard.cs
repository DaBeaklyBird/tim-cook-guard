using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using Microsoft.Win32;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace TimCookGuard
{
    internal static class Program
    {
        [STAThread]
        private static void Main(string[] args)
        {
            if (args.Length > 0 && String.Equals(args[0], "--self-test", StringComparison.OrdinalIgnoreCase))
            {
                Rectangle testScreen = new Rectangle(0, 0, 1920, 1080);
                bool passed = GuardContext.IsTopRight(new Point(1919, 0), testScreen)
                    && GuardContext.IsTopLeft(new Point(0, 0), testScreen)
                    && !GuardContext.IsTopRight(new Point(960, 540), testScreen)
                    && !GuardContext.IsTopLeft(new Point(960, 540), testScreen);
                Console.WriteLine(passed ? "SELF-TEST PASSED" : "SELF-TEST FAILED");
                Environment.ExitCode = passed ? 0 : 1;
                return;
            }

            bool createdNew;
            using (System.Threading.Mutex mutex = new System.Threading.Mutex(true, "Local\\TimCookGuard-6EAD67FC-CA19-4EB2-A33C-84578B53439C", out createdNew))
            {
                if (!createdNew)
                    return;

                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                bool showSettings = Array.Exists(args, delegate(string arg) { return String.Equals(arg, "--settings", StringComparison.OrdinalIgnoreCase); });
                using (GuardContext context = new GuardContext(showSettings))
                    Application.Run(context);
            }
        }
    }

    internal sealed class GuardSettings
    {
        private const string RegistryPath = "Software\\TimCookGuard";

        internal int IdleMinutes = 5;
        internal bool AutomaticArmingEnabled;
        internal int ChallengeSeconds = 3;
        internal int ShutdownSeconds = 10;
        internal bool ShutdownEnabled;
        internal int VideoSeconds = 5;
        internal bool WrongCodeReaction = true;
        internal bool CapturePhoto = true;
        internal bool CaptureVideo = true;
        internal bool DiscordEnabled;
        internal string DiscordWebhook = String.Empty;

        internal static GuardSettings Load()
        {
            GuardSettings settings = new GuardSettings();
            try
            {
                using (RegistryKey key = Registry.CurrentUser.CreateSubKey(RegistryPath))
                {
                    settings.IdleMinutes = ReadInt(key, "IdleMinutes", 5, 1, 120);
                    settings.AutomaticArmingEnabled = ReadBool(key, "AutomaticArmingEnabled", false);
                    settings.ChallengeSeconds = ReadInt(key, "ChallengeSeconds", 3, 1, 10);
                    settings.ShutdownSeconds = ReadInt(key, "ShutdownSeconds", 10, 5, 120);
                    settings.ShutdownEnabled = ReadBool(key, "ShutdownEnabled", false);
                    settings.VideoSeconds = ReadInt(key, "VideoSeconds", 5, 1, 30);
                    settings.WrongCodeReaction = ReadBool(key, "WrongCodeReaction", true);
                    settings.CapturePhoto = ReadBool(key, "CapturePhoto", true);
                    settings.CaptureVideo = ReadBool(key, "CaptureVideo", true);
                    settings.DiscordEnabled = ReadBool(key, "DiscordEnabled", false);
                    string protectedWebhook = Convert.ToString(key.GetValue("DiscordWebhook", String.Empty));
                    if (!String.IsNullOrEmpty(protectedWebhook))
                    {
                        byte[] encrypted = Convert.FromBase64String(protectedWebhook);
                        settings.DiscordWebhook = Encoding.UTF8.GetString(ProtectedData.Unprotect(encrypted, null, DataProtectionScope.CurrentUser));
                    }
                }
            }
            catch
            {
                settings.DiscordWebhook = String.Empty;
                settings.DiscordEnabled = false;
            }
            return settings;
        }

        internal void Save()
        {
            using (RegistryKey key = Registry.CurrentUser.CreateSubKey(RegistryPath))
            {
                key.SetValue("IdleMinutes", IdleMinutes, RegistryValueKind.DWord);
                key.SetValue("AutomaticArmingEnabled", AutomaticArmingEnabled ? 1 : 0, RegistryValueKind.DWord);
                key.SetValue("ChallengeSeconds", ChallengeSeconds, RegistryValueKind.DWord);
                key.SetValue("ShutdownSeconds", ShutdownSeconds, RegistryValueKind.DWord);
                key.SetValue("ShutdownEnabled", ShutdownEnabled ? 1 : 0, RegistryValueKind.DWord);
                key.SetValue("VideoSeconds", VideoSeconds, RegistryValueKind.DWord);
                key.SetValue("WrongCodeReaction", WrongCodeReaction ? 1 : 0, RegistryValueKind.DWord);
                key.SetValue("CapturePhoto", CapturePhoto ? 1 : 0, RegistryValueKind.DWord);
                key.SetValue("CaptureVideo", CaptureVideo ? 1 : 0, RegistryValueKind.DWord);
                key.SetValue("DiscordEnabled", DiscordEnabled ? 1 : 0, RegistryValueKind.DWord);
                string protectedWebhook = String.Empty;
                if (!String.IsNullOrWhiteSpace(DiscordWebhook))
                {
                    byte[] encrypted = ProtectedData.Protect(Encoding.UTF8.GetBytes(DiscordWebhook.Trim()), null, DataProtectionScope.CurrentUser);
                    protectedWebhook = Convert.ToBase64String(encrypted);
                }
                key.SetValue("DiscordWebhook", protectedWebhook, RegistryValueKind.String);
            }
        }

        private static int ReadInt(RegistryKey key, string name, int fallback, int minimum, int maximum)
        {
            int value;
            if (!Int32.TryParse(Convert.ToString(key.GetValue(name, fallback)), out value))
                value = fallback;
            return Math.Max(minimum, Math.Min(maximum, value));
        }

        private static bool ReadBool(RegistryKey key, string name, bool fallback)
        {
            int value;
            return Int32.TryParse(Convert.ToString(key.GetValue(name, fallback ? 1 : 0)), out value) ? value != 0 : fallback;
        }
    }

    internal static class StartupManager
    {
        private const string RunPath = "Software\\Microsoft\\Windows\\CurrentVersion\\Run";

        internal static bool IsEnabled()
        {
            using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RunPath))
                return key != null && !String.IsNullOrWhiteSpace(Convert.ToString(key.GetValue("TimCookGuard")));
        }

        internal static void SetEnabled(bool enabled)
        {
            using (RegistryKey key = Registry.CurrentUser.CreateSubKey(RunPath))
            {
                if (enabled)
                    key.SetValue("TimCookGuard", "\"" + Assembly.GetExecutingAssembly().Location + "\"", RegistryValueKind.String);
                else
                    key.DeleteValue("TimCookGuard", false);
            }
        }
    }

    internal sealed class LegacyControlPanelForm : Form
    {
        private readonly GuardSettings settings;
        private readonly Action saveCallback;
        private readonly Action armCallback;
        private readonly NumericUpDown idle = new NumericUpDown();
        private readonly NumericUpDown challenge = new NumericUpDown();
        private readonly NumericUpDown shutdown = new NumericUpDown();
        private readonly NumericUpDown video = new NumericUpDown();
        private readonly CheckBox wrongReaction = new CheckBox();
        private readonly CheckBox photo = new CheckBox();
        private readonly CheckBox videoEnabled = new CheckBox();
        private readonly CheckBox discordEnabled = new CheckBox();
        private readonly TextBox webhook = new TextBox();
        private readonly CheckBox showWebhook = new CheckBox();
        private readonly CheckBox startup = new CheckBox();
        private readonly Label status = new Label();

        internal LegacyControlPanelForm(GuardSettings current, Action save, Action arm)
        {
            settings = current;
            saveCallback = save;
            armCallback = arm;
            Text = "Tim Cook Guard Control Panel";
            Icon = SystemIcons.Shield;
            ClientSize = new Size(540, 525);
            MinimumSize = new Size(556, 564);
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            BackColor = Color.FromArgb(245, 247, 250);
            Font = new Font("Segoe UI", 9F);
            BuildControls();
            LoadValues();
        }

        private void BuildControls()
        {
            Label title = new Label();
            title.Text = "Tim Cook Guard";
            title.Font = new Font("Segoe UI Semibold", 18F, FontStyle.Bold);
            title.AutoSize = true;
            title.Location = new Point(24, 18);
            Controls.Add(title);

            Label subtitle = new Label();
            subtitle.Text = "Ctrl + Alt + T arms silently. Type cook to dismiss Tim Cook.";
            subtitle.ForeColor = Color.DimGray;
            subtitle.AutoSize = true;
            subtitle.Location = new Point(27, 55);
            Controls.Add(subtitle);

            AddNumber("Automatic arm after", idle, "minutes", 90, 1, 120);
            AddNumber("Corner gesture window", challenge, "seconds", 128, 1, 10);
            AddNumber("Shutdown deadline", shutdown, "seconds", 166, 5, 120);
            AddNumber("Evidence video length", video, "seconds", 204, 1, 30);

            wrongReaction.Text = "Replay “wow” after each incorrect four-letter code";
            wrongReaction.AutoSize = true;
            wrongReaction.Location = new Point(28, 248);
            Controls.Add(wrongReaction);

            photo.Text = "Take a photo before shutdown";
            photo.AutoSize = true;
            photo.Location = new Point(28, 278);
            Controls.Add(photo);

            videoEnabled.Text = "Record the final webcam video before shutdown";
            videoEnabled.AutoSize = true;
            videoEnabled.Location = new Point(270, 278);
            Controls.Add(videoEnabled);

            discordEnabled.Text = "Send incident evidence to Discord";
            discordEnabled.AutoSize = true;
            discordEnabled.Location = new Point(28, 312);
            Controls.Add(discordEnabled);

            Label webhookLabel = new Label();
            webhookLabel.Text = "Discord webhook";
            webhookLabel.AutoSize = true;
            webhookLabel.Location = new Point(28, 346);
            Controls.Add(webhookLabel);

            webhook.Location = new Point(28, 368);
            webhook.Size = new Size(404, 25);
            webhook.UseSystemPasswordChar = true;
            Controls.Add(webhook);

            showWebhook.Text = "Show";
            showWebhook.AutoSize = true;
            showWebhook.Location = new Point(442, 370);
            showWebhook.CheckedChanged += delegate { webhook.UseSystemPasswordChar = !showWebhook.Checked; };
            Controls.Add(showWebhook);

            startup.Text = "Start automatically when I sign into Windows";
            startup.AutoSize = true;
            startup.Location = new Point(28, 410);
            Controls.Add(startup);

            Button saveButton = new Button();
            saveButton.Text = "Save settings";
            saveButton.Size = new Size(125, 36);
            saveButton.Location = new Point(28, 454);
            saveButton.BackColor = Color.FromArgb(0, 120, 215);
            saveButton.ForeColor = Color.White;
            saveButton.FlatStyle = FlatStyle.Flat;
            saveButton.Click += SaveClicked;
            Controls.Add(saveButton);

            Button armButton = new Button();
            armButton.Text = "Arm now";
            armButton.Size = new Size(105, 36);
            armButton.Location = new Point(164, 454);
            armButton.Click += delegate { armCallback(); status.Text = "Armed. The next input starts the corner challenge."; };
            Controls.Add(armButton);

            status.AutoSize = false;
            status.Size = new Size(235, 40);
            status.Location = new Point(285, 451);
            status.ForeColor = Color.DarkGreen;
            Controls.Add(status);
        }

        private void AddNumber(string labelText, NumericUpDown input, string suffix, int y, int minimum, int maximum)
        {
            Label label = new Label();
            label.Text = labelText;
            label.AutoSize = true;
            label.Location = new Point(28, y + 4);
            Controls.Add(label);
            input.Minimum = minimum;
            input.Maximum = maximum;
            input.Location = new Point(270, y);
            input.Size = new Size(72, 25);
            Controls.Add(input);
            Label unit = new Label();
            unit.Text = suffix;
            unit.AutoSize = true;
            unit.Location = new Point(351, y + 4);
            Controls.Add(unit);
        }

        private void LoadValues()
        {
            idle.Value = settings.IdleMinutes;
            challenge.Value = settings.ChallengeSeconds;
            shutdown.Value = settings.ShutdownSeconds;
            video.Value = settings.VideoSeconds;
            wrongReaction.Checked = settings.WrongCodeReaction;
            photo.Checked = settings.CapturePhoto;
            videoEnabled.Checked = settings.CaptureVideo;
            discordEnabled.Checked = settings.DiscordEnabled;
            webhook.Text = settings.DiscordWebhook;
            startup.Checked = StartupManager.IsEnabled();
        }

        private void SaveClicked(object sender, EventArgs e)
        {
            string enteredWebhook = webhook.Text.Trim();
            if (discordEnabled.Checked && !String.IsNullOrEmpty(enteredWebhook) && !DiscordUploader.IsValidWebhook(enteredWebhook))
            {
                status.ForeColor = Color.DarkRed;
                status.Text = "Enter a valid Discord webhook URL.";
                return;
            }
            settings.IdleMinutes = (int)idle.Value;
            settings.ChallengeSeconds = (int)challenge.Value;
            settings.ShutdownSeconds = (int)shutdown.Value;
            settings.VideoSeconds = Math.Min((int)video.Value, settings.ShutdownSeconds);
            video.Value = settings.VideoSeconds;
            settings.WrongCodeReaction = wrongReaction.Checked;
            settings.CapturePhoto = photo.Checked;
            settings.CaptureVideo = videoEnabled.Checked;
            settings.DiscordEnabled = discordEnabled.Checked && !String.IsNullOrEmpty(enteredWebhook);
            settings.DiscordWebhook = enteredWebhook;
            StartupManager.SetEnabled(startup.Checked);
            saveCallback();
            status.ForeColor = Color.DarkGreen;
            status.Text = "Saved. Changes apply immediately.";
        }
    }

    internal sealed class ArmConfirmationForm : Form
    {
        private ArmConfirmationForm()
        {
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            TopMost = true;
            BackColor = Color.FromArgb(22, 145, 80);
            ForeColor = Color.White;
            Opacity = 0.92;
            ClientSize = new Size(126, 38);
            Label label = new Label();
            label.Text = "ARMED";
            label.TextAlign = ContentAlignment.MiddleCenter;
            label.Dock = DockStyle.Fill;
            label.Font = new Font("Segoe UI Semibold", 11F, FontStyle.Bold);
            Controls.Add(label);
            Rectangle work = Screen.PrimaryScreen.WorkingArea;
            Location = new Point(work.Right - Width - 18, work.Bottom - Height - 18);
        }

        protected override bool ShowWithoutActivation { get { return true; } }

        protected override CreateParams CreateParams
        {
            get
            {
                const int WS_EX_NOACTIVATE = 0x08000000;
                CreateParams parameters = base.CreateParams;
                parameters.ExStyle |= WS_EX_NOACTIVATE;
                return parameters;
            }
        }

        internal static void ShowBrief()
        {
            ArmConfirmationForm confirmation = new ArmConfirmationForm();
            Timer timer = new Timer();
            timer.Interval = 700;
            timer.Tick += delegate
            {
                timer.Stop();
                timer.Dispose();
                confirmation.Close();
                confirmation.Dispose();
            };
            confirmation.Show();
            timer.Start();
        }
    }

    internal sealed class GuardContext : ApplicationContext
    {
        private const int CornerSize = 100;

        private readonly Timer monitorTimer;
        private readonly Timer shutdownTimer;
        private readonly Timer videoStartTimer;
        private readonly List<OverlayForm> overlays = new List<OverlayForm>();
        private readonly Image timCookImage;
        private readonly Image appLogo;
        private readonly TimCookSound timCookSound;
        private readonly GuardSettings settings;
        private readonly NotifyIcon trayIcon;
        private bool challengeActive;
        private bool sawTopRight;
        private long challengeStarted;
        private uint lastObservedInputTick;
        private bool manualArmSettling;
        private bool manuallyArmed;
        private bool manualArmLatched;
        private bool sessionLocked;
        private long manualArmStarted;
        private KeyboardBlocker keyboardBlocker;
        private readonly ArmHotkeyWindow armHotkey;
        private EvidenceSession evidence;
        private ControlPanelForm controlPanel;

        internal GuardContext(bool showSettings)
        {
            settings = GuardSettings.Load();
            timCookImage = LoadEmbeddedImage();
            appLogo = LoadEmbeddedLogo();
            timCookSound = new TimCookSound();
            lastObservedInputTick = GetLastInputTick();
            armHotkey = new ArmHotkeyWindow(ArmManually);
            SystemEvents.SessionSwitch += SessionSwitch;
            monitorTimer = new Timer();
            monitorTimer.Interval = 50;
            monitorTimer.Tick += MonitorTimerTick;
            monitorTimer.Start();
            shutdownTimer = new Timer();
            shutdownTimer.Tick += ShutdownTimerTick;
            videoStartTimer = new Timer();
            videoStartTimer.Tick += VideoStartTimerTick;
            trayIcon = CreateTrayIcon();
            if (showSettings)
                ShowControlPanel();
        }

        private void MonitorTimerTick(object sender, EventArgs e)
        {
            uint lastInputTick = GetLastInputTick();

            if (sessionLocked)
            {
                lastObservedInputTick = lastInputTick;
                return;
            }

            if (manualArmSettling)
            {
                double settlingMs = (Stopwatch.GetTimestamp() - manualArmStarted) * 1000.0 / Stopwatch.Frequency;
                lastObservedInputTick = lastInputTick;
                if (settlingMs >= 400)
                {
                    manualArmSettling = false;
                    manuallyArmed = true;
                }
                return;
            }

            if (!challengeActive && lastInputTick != lastObservedInputTick)
            {
                uint timeSincePreviousInput = lastInputTick - lastObservedInputTick;
                lastObservedInputTick = lastInputTick;
                if (manuallyArmed || (settings.AutomaticArmingEnabled && timeSincePreviousInput >= settings.IdleMinutes * 60 * 1000))
                {
                    manuallyArmed = false;
                    challengeActive = true;
                    sawTopRight = false;
                    challengeStarted = Stopwatch.GetTimestamp();
                }
            }

            if (!challengeActive)
                return;

            Rectangle primaryBounds = Screen.PrimaryScreen.Bounds;
            Point cursor = Cursor.Position;

            if (!sawTopRight && IsTopRight(cursor, primaryBounds))
            {
                sawTopRight = true;
            }
            else if (sawTopRight && IsTopLeft(cursor, primaryBounds))
            {
                challengeActive = false;
                manualArmLatched = false;
                lastObservedInputTick = GetLastInputTick();
                return;
            }

            double elapsedMs = (Stopwatch.GetTimestamp() - challengeStarted) * 1000.0 / Stopwatch.Frequency;
            if (elapsedMs >= settings.ChallengeSeconds * 1000)
            {
                challengeActive = false;
                ShowTimCook();
            }
        }

        private void SessionSwitch(object sender, SessionSwitchEventArgs e)
        {
            if (e.Reason == SessionSwitchReason.SessionLock)
            {
                sessionLocked = true;
                ResetAfterSessionChange(false);
            }
            else if (e.Reason == SessionSwitchReason.SessionUnlock)
            {
                sessionLocked = false;
                ResetAfterSessionChange(true);
            }
        }

        private void ResetAfterSessionChange(bool unlocking)
        {
            if (overlays.Count > 0)
                return;

            challengeActive = false;
            manualArmSettling = false;
            sawTopRight = false;
            lastObservedInputTick = GetLastInputTick();

            if (manualArmLatched)
            {
                manuallyArmed = !unlocking;
                if (unlocking)
                {
                    manualArmSettling = true;
                    manualArmStarted = Stopwatch.GetTimestamp();
                }
            }
            else
            {
                manuallyArmed = false;
            }
        }

        private void ArmManually()
        {
            if (challengeActive || overlays.Count > 0)
                return;

            manualArmLatched = true;
            manuallyArmed = false;
            manualArmSettling = true;
            manualArmStarted = Stopwatch.GetTimestamp();
            lastObservedInputTick = GetLastInputTick();
            ArmConfirmationForm.ShowBrief();
        }

        private NotifyIcon CreateTrayIcon()
        {
            ContextMenuStrip menu = new ContextMenuStrip();
            menu.Items.Add("Open Control Panel", null, delegate { ShowControlPanel(); });
            menu.Items.Add("Arm Now  (Ctrl+Alt+T)", null, delegate { ArmManually(); });
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("Exit", null, delegate { ExitGuard(); });

            NotifyIcon icon = new NotifyIcon();
            icon.Icon = Icon.ExtractAssociatedIcon(Assembly.GetExecutingAssembly().Location);
            icon.Text = "Tim Cook Guard";
            icon.ContextMenuStrip = menu;
            icon.Visible = true;
            icon.DoubleClick += delegate { ShowControlPanel(); };
            return icon;
        }

        private void ShowControlPanel()
        {
            if (controlPanel == null || controlPanel.IsDisposed)
                controlPanel = new ControlPanelForm(settings, SaveSettings, ArmManually, appLogo);
            controlPanel.Show();
            controlPanel.WindowState = FormWindowState.Normal;
            controlPanel.Activate();
        }

        private void SaveSettings()
        {
            settings.Save();
            trayIcon.Text = "Tim Cook Guard - settings saved";
            Timer reset = new Timer();
            reset.Interval = 1500;
            reset.Tick += delegate
            {
                reset.Stop();
                reset.Dispose();
                trayIcon.Text = "Tim Cook Guard";
            };
            reset.Start();
        }

        private void ExitGuard()
        {
            if (overlays.Count > 0)
                return;
            trayIcon.Visible = false;
            ExitThread();
        }

        internal static bool IsTopRight(Point point, Rectangle bounds)
        {
            return point.X >= bounds.Right - CornerSize && point.X < bounds.Right
                && point.Y >= bounds.Top && point.Y < bounds.Top + CornerSize;
        }

        internal static bool IsTopLeft(Point point, Rectangle bounds)
        {
            return point.X >= bounds.Left && point.X < bounds.Left + CornerSize
                && point.Y >= bounds.Top && point.Y < bounds.Top + CornerSize;
        }

        private void ShowTimCook()
        {
            monitorTimer.Stop();
            timCookSound.Play();
            keyboardBlocker = new KeyboardBlocker(HideTimCook, WrongCodeReaction);
            keyboardBlocker.Install();

            foreach (Screen screen in Screen.AllScreens)
            {
                OverlayForm overlay = new OverlayForm(timCookImage, screen.Bounds);
                overlays.Add(overlay);
                overlay.Show();
            }

            if (overlays.Count > 0)
                overlays[0].Activate();
            evidence = new EvidenceSession(settings.CapturePhoto, settings.CaptureVideo);
            shutdownTimer.Interval = settings.ShutdownSeconds * 1000;
            shutdownTimer.Start();
            if (settings.CaptureVideo)
            {
                int videoSeconds = Math.Min(settings.VideoSeconds, settings.ShutdownSeconds);
                int videoDelay = Math.Max(1, (settings.ShutdownSeconds - videoSeconds) * 1000);
                videoStartTimer.Interval = videoDelay;
                videoStartTimer.Start();
            }
        }

        private void WrongCodeReaction()
        {
            if (settings.WrongCodeReaction)
                timCookSound.Play();
        }

        private void VideoStartTimerTick(object sender, EventArgs e)
        {
            videoStartTimer.Stop();
            if (evidence != null)
                evidence.StartVideo(Math.Min(settings.VideoSeconds, settings.ShutdownSeconds));
        }

        private void ShutdownTimerTick(object sender, EventArgs e)
        {
            shutdownTimer.Stop();
            videoStartTimer.Stop();
            List<string> files = evidence == null ? new List<string>() : evidence.FinishForShutdown(8000);
            IncidentLog.Write("shutdown", files);
            if (settings.DiscordEnabled && !String.IsNullOrWhiteSpace(settings.DiscordWebhook))
                DiscordUploader.Send(settings.DiscordWebhook, files, 8000);
            if (!settings.ShutdownEnabled)
                return;
            ProcessStartInfo shutdown = new ProcessStartInfo();
            shutdown.FileName = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "shutdown.exe");
            shutdown.Arguments = "/s /f /t 0";
            shutdown.UseShellExecute = false;
            shutdown.CreateNoWindow = true;
            Process.Start(shutdown);
        }

        private void HideTimCook()
        {
            shutdownTimer.Stop();
            videoStartTimer.Stop();
            if (evidence != null)
            {
                evidence.Cancel();
                evidence = null;
            }
            if (keyboardBlocker != null)
            {
                keyboardBlocker.Dispose();
                keyboardBlocker = null;
            }

            foreach (OverlayForm overlay in overlays)
            {
                overlay.AllowClose = true;
                overlay.Close();
                overlay.Dispose();
            }
            overlays.Clear();
            challengeActive = false;
            manuallyArmed = false;
            manualArmLatched = false;
            manualArmSettling = false;
            lastObservedInputTick = GetLastInputTick();
            monitorTimer.Start();
        }

        private static Image LoadEmbeddedImage()
        {
            Assembly assembly = Assembly.GetExecutingAssembly();
            using (Stream stream = assembly.GetManifestResourceStream("TimCookGuard.tim-cook.jpg"))
            {
                if (stream == null)
                    throw new InvalidOperationException("The embedded Tim Cook image is missing.");
                using (Image source = Image.FromStream(stream))
                    return new Bitmap(source);
            }
        }

        private static Image LoadEmbeddedLogo()
        {
            Assembly assembly = Assembly.GetExecutingAssembly();
            using (Stream stream = assembly.GetManifestResourceStream("TimCookGuard.logo.png"))
            {
                if (stream == null)
                    throw new InvalidOperationException("The embedded Tim Cook Guard logo is missing.");
                using (Image source = Image.FromStream(stream))
                    return new Bitmap(source);
            }
        }

        private static uint GetLastInputTick()
        {
            LASTINPUTINFO info = new LASTINPUTINFO();
            info.cbSize = (uint)Marshal.SizeOf(info);
            return GetLastInputInfo(ref info) ? info.dwTime : unchecked((uint)Environment.TickCount);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                SystemEvents.SessionSwitch -= SessionSwitch;
                monitorTimer.Dispose();
                shutdownTimer.Dispose();
                videoStartTimer.Dispose();
                armHotkey.Dispose();
                trayIcon.Visible = false;
                trayIcon.Dispose();
                if (keyboardBlocker != null)
                    keyboardBlocker.Dispose();
                timCookSound.Dispose();
                appLogo.Dispose();
                timCookImage.Dispose();
            }
            base.Dispose(disposing);
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct LASTINPUTINFO
        {
            public uint cbSize;
            public uint dwTime;
        }

        [DllImport("user32.dll")]
        private static extern bool GetLastInputInfo(ref LASTINPUTINFO plii);
    }

    internal sealed class EvidenceSession
    {
        private readonly bool capturePhoto;
        private readonly bool captureVideo;
        private Process videoProcess;
        private string videoPath;
        private string videoScriptPath;

        internal EvidenceSession(bool shouldCapturePhoto, bool shouldCaptureVideo)
        {
            capturePhoto = shouldCapturePhoto;
            captureVideo = shouldCaptureVideo;
        }

        internal void StartVideo(int seconds)
        {
            if (!captureVideo || videoProcess != null)
                return;
            try
            {
                string python = FindPython();
                if (python == null)
                    return;
                string downloads = GetDownloads();
                videoPath = Path.Combine(downloads, "TimCook-" + DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss-fff") + ".mp4");
                videoScriptPath = Path.Combine(Path.GetTempPath(), "TimCookGuard-video-" + Process.GetCurrentProcess().Id + ".py");
                File.WriteAllText(videoScriptPath,
                    "import cv2, sys, time\n" +
                    "out_path, duration = sys.argv[1], float(sys.argv[2])\n" +
                    "cam = cv2.VideoCapture(0, cv2.CAP_DSHOW)\n" +
                    "if not cam.isOpened(): cam = cv2.VideoCapture(0)\n" +
                    "ok, frame = cam.read()\n" +
                    "if not ok or frame is None: cam.release(); raise SystemExit(2)\n" +
                    "h, w = frame.shape[:2]\n" +
                    "fps = 15.0\n" +
                    "writer = cv2.VideoWriter(out_path, cv2.VideoWriter_fourcc(*'mp4v'), fps, (w, h))\n" +
                    "if not writer.isOpened(): cam.release(); raise SystemExit(3)\n" +
                    "started = time.monotonic()\n" +
                    "while time.monotonic() - started < duration:\n" +
                    "    ok, frame = cam.read()\n" +
                    "    if ok and frame is not None: writer.write(frame)\n" +
                    "    time.sleep(1.0 / fps)\n" +
                    "writer.release(); cam.release()\n",
                    Encoding.UTF8);
                videoProcess = StartPython(python, videoScriptPath, videoPath, seconds.ToString());
            }
            catch
            {
                CleanupVideo(false);
            }
        }

        internal List<string> FinishForShutdown(int timeoutMilliseconds)
        {
            List<string> files = new List<string>();
            if (videoProcess != null)
            {
                try
                {
                    if (!videoProcess.WaitForExit(timeoutMilliseconds))
                        videoProcess.Kill();
                    if (videoProcess.HasExited && videoProcess.ExitCode == 0 && IsUsableFile(videoPath))
                        files.Add(videoPath);
                }
                catch
                {
                }
                CleanupVideo(files.Contains(videoPath));
            }

            if (capturePhoto)
            {
                string photo = CapturePhoto(timeoutMilliseconds);
                if (photo != null)
                    files.Add(photo);
            }
            return files;
        }

        internal void Cancel()
        {
            try
            {
                if (videoProcess != null && !videoProcess.HasExited)
                    videoProcess.Kill();
            }
            catch
            {
            }
            CleanupVideo(false);
        }

        private static string CapturePhoto(int timeoutMilliseconds)
        {
            string script = null;
            string photo = null;
            try
            {
                string python = FindPython();
                if (python == null)
                    return null;
                photo = Path.Combine(GetDownloads(), "TimCook-" + DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss-fff") + ".jpg");
                script = Path.Combine(Path.GetTempPath(), "TimCookGuard-photo-" + Process.GetCurrentProcess().Id + ".py");
                File.WriteAllText(script,
                    "import cv2, sys\n" +
                    "cam = cv2.VideoCapture(0, cv2.CAP_DSHOW)\n" +
                    "if not cam.isOpened(): cam = cv2.VideoCapture(0)\n" +
                    "ok, frame = False, None\n" +
                    "for _ in range(12): ok, frame = cam.read()\n" +
                    "cam.release()\n" +
                    "saved = bool(ok and frame is not None and cv2.imwrite(sys.argv[1], frame))\n" +
                    "raise SystemExit(0 if saved else 1)\n",
                    Encoding.UTF8);
                using (Process process = StartPython(python, script, photo))
                {
                    if (!process.WaitForExit(timeoutMilliseconds))
                        process.Kill();
                    if (process.HasExited && process.ExitCode == 0 && IsUsableFile(photo))
                        return photo;
                }
            }
            catch
            {
            }
            finally
            {
                DeleteQuietly(script);
            }
            DeleteQuietly(photo);
            return null;
        }

        private void CleanupVideo(bool keepVideo)
        {
            if (videoProcess != null)
            {
                videoProcess.Dispose();
                videoProcess = null;
            }
            DeleteQuietly(videoScriptPath);
            if (!keepVideo)
                DeleteQuietly(videoPath);
            videoScriptPath = null;
            videoPath = null;
        }

        private static Process StartPython(string python, params string[] arguments)
        {
            StringBuilder command = new StringBuilder();
            foreach (string argument in arguments)
                command.Append(" \"").Append(argument.Replace("\"", "")).Append("\"");
            ProcessStartInfo start = new ProcessStartInfo();
            start.FileName = python;
            start.Arguments = command.ToString();
            start.UseShellExecute = false;
            start.CreateNoWindow = true;
            start.WindowStyle = ProcessWindowStyle.Hidden;
            return Process.Start(start);
        }

        private static string FindPython()
        {
            string root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "Python");
            if (!Directory.Exists(root))
                return null;
            string[] candidates = Directory.GetFiles(root, "python.exe", SearchOption.AllDirectories);
            return candidates.Length > 0 ? candidates[0] : null;
        }

        private static string GetDownloads()
        {
            string downloads = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
            Directory.CreateDirectory(downloads);
            return downloads;
        }

        private static bool IsUsableFile(string path)
        {
            return path != null && File.Exists(path) && new FileInfo(path).Length > 1024;
        }

        private static void DeleteQuietly(string path)
        {
            try
            {
                if (path != null && File.Exists(path))
                    File.Delete(path);
            }
            catch
            {
            }
        }
    }

    internal static class IncidentLog
    {
        internal static void Write(string outcome, IList<string> files)
        {
            try
            {
                string path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads", "TimCookGuard-Incidents.log");
                string line = DateTime.Now.ToString("O") + " | " + outcome + " | " + String.Join(", ", new List<string>(files).ConvertAll(Path.GetFileName).ToArray()) + Environment.NewLine;
                File.AppendAllText(path, line, Encoding.UTF8);
            }
            catch
            {
            }
        }
    }

    internal static class DiscordUploader
    {
        internal static bool IsValidWebhook(string webhook)
        {
            Uri uri;
            return Uri.TryCreate(webhook, UriKind.Absolute, out uri)
                && uri.Scheme == Uri.UriSchemeHttps
                && IsDiscordHost(uri.Host)
                && uri.AbsolutePath.StartsWith("/api/webhooks/", StringComparison.OrdinalIgnoreCase);
        }

        internal static bool Send(string webhook, IList<string> files, int timeoutMilliseconds)
        {
            Uri uri;
            if (!IsValidWebhook(webhook) || !Uri.TryCreate(webhook, UriKind.Absolute, out uri))
                return false;
            try
            {
                using (HttpClient client = new HttpClient())
                using (MultipartFormDataContent form = new MultipartFormDataContent())
                {
                    client.Timeout = TimeSpan.FromMilliseconds(timeoutMilliseconds);
                    string content = "Tim Cook Guard triggered on " + Environment.MachineName + " at " + DateTime.Now.ToString("O");
                    form.Add(new StringContent("{\"content\":\"" + JsonEscape(content) + "\"}"), "payload_json");
                    List<FileStream> streams = new List<FileStream>();
                    try
                    {
                        int index = 0;
                        foreach (string file in files)
                        {
                            if (!File.Exists(file))
                                continue;
                            FileStream stream = File.OpenRead(file);
                            streams.Add(stream);
                            StreamContent attachment = new StreamContent(stream);
                            attachment.Headers.ContentType = new MediaTypeHeaderValue(Path.GetExtension(file).Equals(".mp4", StringComparison.OrdinalIgnoreCase) ? "video/mp4" : "image/jpeg");
                            form.Add(attachment, "files[" + index + "]", Path.GetFileName(file));
                            index++;
                        }
                        Task<HttpResponseMessage> post = client.PostAsync(uri, form);
                        if (!post.Wait(timeoutMilliseconds))
                            return false;
                        using (HttpResponseMessage response = post.Result)
                            return response.IsSuccessStatusCode;
                    }
                    finally
                    {
                        foreach (FileStream stream in streams)
                            stream.Dispose();
                    }
                }
            }
            catch
            {
                return false;
            }
        }

        private static bool IsDiscordHost(string host)
        {
            return host.Equals("discord.com", StringComparison.OrdinalIgnoreCase)
                || host.EndsWith(".discord.com", StringComparison.OrdinalIgnoreCase)
                || host.Equals("discordapp.com", StringComparison.OrdinalIgnoreCase)
                || host.EndsWith(".discordapp.com", StringComparison.OrdinalIgnoreCase);
        }

        private static string JsonEscape(string value)
        {
            return value.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\r", "").Replace("\n", "\\n");
        }
    }

    internal sealed class TimCookSound : IDisposable
    {
        private const string Alias = "TimCookGuardWow";
        private readonly string audioPath;
        private bool disposed;

        internal TimCookSound()
        {
            audioPath = Path.Combine(Path.GetTempPath(), "TimCookGuard-wow-" + Process.GetCurrentProcess().Id + ".mp3");
            Assembly assembly = Assembly.GetExecutingAssembly();
            using (Stream input = assembly.GetManifestResourceStream("TimCookGuard.wow.mp3"))
            {
                if (input == null)
                    throw new InvalidOperationException("The embedded Tim Cook sound is missing.");
                using (FileStream output = new FileStream(audioPath, FileMode.Create, FileAccess.Write, FileShare.Read))
                    input.CopyTo(output);
            }
        }

        internal void Play()
        {
            mciSendString("close " + Alias, null, 0, IntPtr.Zero);
            int opened = mciSendString("open \"" + audioPath + "\" type mpegvideo alias " + Alias, null, 0, IntPtr.Zero);
            if (opened == 0)
                mciSendString("play " + Alias + " from 0", null, 0, IntPtr.Zero);
        }

        public void Dispose()
        {
            if (disposed)
                return;
            disposed = true;
            mciSendString("stop " + Alias, null, 0, IntPtr.Zero);
            mciSendString("close " + Alias, null, 0, IntPtr.Zero);
            try
            {
                if (File.Exists(audioPath))
                    File.Delete(audioPath);
            }
            catch
            {
            }
            GC.SuppressFinalize(this);
        }

        [DllImport("winmm.dll", CharSet = CharSet.Auto)]
        private static extern int mciSendString(string command, StringBuilder returnValue, int returnLength, IntPtr callback);
    }

    internal sealed class ArmHotkeyWindow : NativeWindow, IDisposable
    {
        private const int WM_HOTKEY = 0x0312;
        private const int HotkeyId = 0x5443;
        private const uint ModAlt = 0x0001;
        private const uint ModControl = 0x0002;
        private readonly Action arm;
        private bool disposed;

        internal ArmHotkeyWindow(Action armCallback)
        {
            arm = armCallback;
            CreateHandle(new CreateParams());
            if (!RegisterHotKey(Handle, HotkeyId, ModControl | ModAlt, (uint)Keys.T))
                throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error(), "Could not register Ctrl+Alt+T.");
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WM_HOTKEY && m.WParam.ToInt32() == HotkeyId)
            {
                arm();
                return;
            }
            base.WndProc(ref m);
        }

        public void Dispose()
        {
            if (disposed)
                return;
            disposed = true;
            if (Handle != IntPtr.Zero)
            {
                UnregisterHotKey(Handle, HotkeyId);
                DestroyHandle();
            }
            GC.SuppressFinalize(this);
        }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint modifiers, uint virtualKey);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);
    }

    internal sealed class OverlayForm : Form
    {
        internal bool AllowClose { get; set; }

        internal OverlayForm(Image image, Rectangle bounds)
        {
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            Bounds = bounds;
            TopMost = true;
            ShowInTaskbar = false;
            BackColor = Color.Black;
            BackgroundImage = image;
            BackgroundImageLayout = ImageLayout.Zoom;
            KeyPreview = true;
            Cursor.Hide();
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (!AllowClose)
            {
                e.Cancel = true;
                return;
            }
            Cursor.Show();
            base.OnFormClosing(e);
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (!AllowClose && (keyData & Keys.KeyCode) == Keys.F4)
                return true;
            return base.ProcessCmdKey(ref msg, keyData);
        }

        protected override void WndProc(ref Message m)
        {
            const int WM_SYSCOMMAND = 0x0112;
            const int SC_CLOSE = 0xF060;
            if (!AllowClose && m.Msg == WM_SYSCOMMAND && ((int)m.WParam & 0xFFF0) == SC_CLOSE)
                return;
            base.WndProc(ref m);
        }
    }

    internal sealed class KeyboardBlocker : IDisposable
    {
        private const int WH_KEYBOARD_LL = 13;
        private const int WM_KEYDOWN = 0x0100;
        private const int WM_SYSKEYDOWN = 0x0104;
        private readonly Action accepted;
        private readonly Action rejected;
        private readonly StringBuilder typed = new StringBuilder();
        private readonly LowLevelKeyboardProc callback;
        private IntPtr hook;
        private bool disposed;

        internal KeyboardBlocker(Action acceptedCallback, Action rejectedCallback)
        {
            accepted = acceptedCallback;
            rejected = rejectedCallback;
            callback = HookCallback;
        }

        internal void Install()
        {
            using (Process process = Process.GetCurrentProcess())
            using (ProcessModule module = process.MainModule)
                hook = SetWindowsHookEx(WH_KEYBOARD_LL, callback, GetModuleHandle(module.ModuleName), 0);

            if (hook == IntPtr.Zero)
                throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
        }

        private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0 && (wParam == (IntPtr)WM_KEYDOWN || wParam == (IntPtr)WM_SYSKEYDOWN))
            {
                int keyCode = Marshal.ReadInt32(lParam);
                if (keyCode >= (int)Keys.A && keyCode <= (int)Keys.Z)
                {
                    typed.Append(Char.ToLowerInvariant((char)keyCode));
                    if (typed.Length == 4)
                    {
                        bool correct = typed.ToString() == "cook";
                        typed.Clear();
                        if (Application.OpenForms.Count > 0)
                            Application.OpenForms[0].BeginInvoke(correct ? accepted : rejected);
                    }
                }
                return (IntPtr)1;
            }
            return CallNextHookEx(hook, nCode, wParam, lParam);
        }

        public void Dispose()
        {
            if (disposed)
                return;
            disposed = true;
            if (hook != IntPtr.Zero)
            {
                UnhookWindowsHookEx(hook);
                hook = IntPtr.Zero;
            }
            GC.SuppressFinalize(this);
        }

        private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint threadId);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll")]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string moduleName);
    }
}
