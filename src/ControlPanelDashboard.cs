using System;
using System.Drawing;
using System.Reflection;
using System.Windows.Forms;

namespace TimCookGuard
{
    internal sealed class ControlPanelForm : Form
    {
        private static readonly Color WindowColor = Color.FromArgb(7, 12, 23);
        private static readonly Color CardColor = Color.FromArgb(17, 25, 40);
        private static readonly Color CardBorder = Color.FromArgb(39, 52, 73);
        private static readonly Color TextPrimary = Color.FromArgb(240, 245, 252);
        private static readonly Color TextMuted = Color.FromArgb(148, 163, 184);
        private static readonly Color Green = Color.FromArgb(43, 214, 123);
        private static readonly Color Blue = Color.FromArgb(44, 132, 255);

        private readonly GuardSettings settings;
        private readonly Action saveCallback;
        private readonly Action armCallback;
        private readonly Image logo;
        private readonly NumericUpDown idle = new NumericUpDown();
        private readonly NumericUpDown challenge = new NumericUpDown();
        private readonly NumericUpDown shutdown = new NumericUpDown();
        private readonly NumericUpDown video = new NumericUpDown();
        private readonly CheckBox wrongReaction = new CheckBox();
        private readonly CheckBox photo = new CheckBox();
        private readonly CheckBox videoEnabled = new CheckBox();
        private readonly CheckBox shutdownEnabled = new CheckBox();
        private readonly CheckBox discordEnabled = new CheckBox();
        private readonly TextBox webhook = new TextBox();
        private readonly CheckBox showWebhook = new CheckBox();
        private readonly CheckBox startup = new CheckBox();
        private readonly CheckBox automaticArming = new CheckBox();
        private readonly Label status = new Label();
        private Panel modePill;
        private Label modeLabel;
        private bool armedState;

        internal ControlPanelForm(GuardSettings current, Action save, Action arm, Image appLogo)
        {
            settings = current;
            saveCallback = save;
            armCallback = arm;
            logo = appLogo;
            Text = "Tim Cook Guard";
            Icon = Icon.ExtractAssociatedIcon(Assembly.GetExecutingAssembly().Location);
            ClientSize = new Size(920, 650);
            MinimumSize = new Size(936, 689);
            MaximumSize = new Size(936, 689);
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            BackColor = WindowColor;
            ForeColor = TextPrimary;
            Font = new Font("Segoe UI", 9F);
            BuildDashboard();
            LoadValues();
        }

        private void BuildDashboard()
        {
            Panel header = new Panel();
            header.Dock = DockStyle.Top;
            header.Height = 112;
            header.BackColor = Color.FromArgb(10, 18, 33);
            Controls.Add(header);

            PictureBox mark = new PictureBox();
            mark.Image = logo;
            mark.SizeMode = PictureBoxSizeMode.Zoom;
            mark.BackColor = Color.Transparent;
            mark.Location = new Point(25, 16);
            mark.Size = new Size(80, 80);
            header.Controls.Add(mark);

            header.Controls.Add(MakeLabel("TIM COOK GUARD", 122, 20, 410, 35, 21F, FontStyle.Bold, TextPrimary));
            header.Controls.Add(MakeLabel("Presence protection · evidence capture · response controls", 124, 58, 520, 24, 9.5F, FontStyle.Regular, TextMuted));

            modePill = new Panel();
            modePill.BackColor = Color.FromArgb(13, 66, 46);
            modePill.Location = new Point(704, 36);
            modePill.Size = new Size(180, 40);
            modeLabel = MakeLabel("●  MANUAL MODE", 0, 0, 180, 40, 9.5F, FontStyle.Bold, Green);
            modeLabel.TextAlign = ContentAlignment.MiddleCenter;
            modePill.Controls.Add(modeLabel);
            header.Controls.Add(modePill);

            Panel armCard = MakeCard(24, 132, 422, 368);
            Controls.Add(armCard);
            armCard.Controls.Add(MakeLabel("ARMING", 22, 18, 170, 25, 10F, FontStyle.Bold, TextMuted));
            armCard.Controls.Add(MakeLabel("Ready when you are", 22, 48, 320, 34, 19F, FontStyle.Bold, TextPrimary));
            armCard.Controls.Add(MakeLabel("Arm it now, or let automatic mode watch the idle timer.", 22, 84, 375, 22, 9F, FontStyle.Regular, TextMuted));

            ConfigureCheck(automaticArming, "Automatic mode", 22, 108, 185);
            automaticArming.CheckedChanged += delegate { UpdateModeVisual(); };
            armCard.Controls.Add(automaticArming);
            idle.Minimum = 1;
            idle.Maximum = 120;
            idle.Location = new Point(270, 108);
            idle.Size = new Size(62, 25);
            idle.BackColor = Color.FromArgb(8, 14, 25);
            idle.ForeColor = TextPrimary;
            idle.BorderStyle = BorderStyle.FixedSingle;
            armCard.Controls.Add(idle);
            armCard.Controls.Add(MakeLabel("idle min", 340, 112, 60, 22, 8.5F, FontStyle.Regular, TextMuted));

            Button armButton = MakeButton("ARM GUARD NOW", Green, Color.FromArgb(5, 25, 18));
            armButton.Location = new Point(22, 148);
            armButton.Size = new Size(378, 54);
            armButton.Click += delegate
            {
                armCallback();
                status.ForeColor = Green;
                status.Text = "Guard armed. The next input starts the corner challenge.";
                Hide();
            };
            armCard.Controls.Add(armButton);

            Label shortcut = MakeLabel("CTRL  +  ALT  +  T", 22, 211, 378, 30, 10F, FontStyle.Bold, Color.FromArgb(189, 206, 229));
            shortcut.TextAlign = ContentAlignment.MiddleCenter;
            shortcut.BackColor = Color.FromArgb(10, 17, 29);
            armCard.Controls.Add(shortcut);

            AddSettingRow(armCard, "Corner gesture", "seconds", challenge, 252, 1, 10);
            AddSettingRow(armCard, "Shutdown deadline", "seconds", shutdown, 292, 5, 120);
            AddSettingRow(armCard, "Evidence video", "seconds", video, 332, 1, 30);

            Panel responseCard = MakeCard(468, 132, 428, 368);
            Controls.Add(responseCard);
            responseCard.Controls.Add(MakeLabel("TRIGGER RESPONSE", 22, 18, 240, 25, 10F, FontStyle.Bold, TextMuted));
            responseCard.Controls.Add(MakeLabel("What happens after a miss", 22, 48, 360, 34, 19F, FontStyle.Bold, TextPrimary));

            ConfigureCheck(wrongReaction, "Replay “wow” for each incorrect four-letter code", 22, 92, 370);
            ConfigureCheck(photo, "Take a webcam photo at the deadline", 22, 124, 370);
            ConfigureCheck(videoEnabled, "Record the final webcam video", 22, 156, 370);
            ConfigureCheck(shutdownEnabled, "Force Windows shutdown after the deadline", 22, 188, 370);
            responseCard.Controls.Add(wrongReaction);
            responseCard.Controls.Add(photo);
            responseCard.Controls.Add(videoEnabled);
            responseCard.Controls.Add(shutdownEnabled);

            Panel divider = new Panel();
            divider.BackColor = CardBorder;
            divider.Location = new Point(22, 224);
            divider.Size = new Size(384, 1);
            responseCard.Controls.Add(divider);

            ConfigureCheck(discordEnabled, "Send incident evidence to Discord", 22, 236, 370);
            responseCard.Controls.Add(discordEnabled);
            responseCard.Controls.Add(MakeLabel("Webhook URL", 22, 270, 150, 20, 8.5F, FontStyle.Bold, TextMuted));

            webhook.Location = new Point(22, 294);
            webhook.Size = new Size(316, 26);
            webhook.BackColor = Color.FromArgb(8, 14, 25);
            webhook.ForeColor = TextPrimary;
            webhook.BorderStyle = BorderStyle.FixedSingle;
            webhook.UseSystemPasswordChar = true;
            responseCard.Controls.Add(webhook);

            ConfigureCheck(showWebhook, "Show", 344, 297, 62);
            showWebhook.CheckedChanged += delegate { webhook.UseSystemPasswordChar = !showWebhook.Checked; };
            responseCard.Controls.Add(showWebhook);
            responseCard.Controls.Add(MakeLabel("Stored with Windows account encryption", 22, 331, 330, 20, 8F, FontStyle.Regular, TextMuted));

            Panel footer = MakeCard(24, 520, 872, 104);
            Controls.Add(footer);
            ConfigureCheck(startup, "Launch Tim Cook Guard when I sign into Windows", 22, 17, 390);
            footer.Controls.Add(startup);
            footer.Controls.Add(MakeLabel("Startup keeps whichever arming mode you selected.", 43, 48, 390, 22, 8.5F, FontStyle.Regular, TextMuted));

            Button saveButton = MakeButton("SAVE DASHBOARD", Blue, Color.White);
            saveButton.Location = new Point(676, 22);
            saveButton.Size = new Size(172, 46);
            saveButton.Click += SaveClicked;
            footer.Controls.Add(saveButton);

            status.Location = new Point(426, 25);
            status.Size = new Size(235, 42);
            status.ForeColor = TextMuted;
            status.TextAlign = ContentAlignment.MiddleRight;
            status.Text = "Settings ready";
            footer.Controls.Add(status);
        }

        private static Panel MakeCard(int x, int y, int width, int height)
        {
            Panel card = new Panel();
            card.Location = new Point(x, y);
            card.Size = new Size(width, height);
            card.BackColor = CardColor;
            card.BorderStyle = BorderStyle.FixedSingle;
            return card;
        }

        private static Label MakeLabel(string text, int x, int y, int width, int height, float size, FontStyle style, Color color)
        {
            Label label = new Label();
            label.Text = text;
            label.Location = new Point(x, y);
            label.Size = new Size(width, height);
            label.Font = new Font("Segoe UI", size, style);
            label.ForeColor = color;
            label.BackColor = Color.Transparent;
            return label;
        }

        private static Button MakeButton(string text, Color background, Color foreground)
        {
            Button button = new Button();
            button.Text = text;
            button.BackColor = background;
            button.ForeColor = foreground;
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderSize = 0;
            button.Font = new Font("Segoe UI Semibold", 10F, FontStyle.Bold);
            button.Cursor = Cursors.Hand;
            return button;
        }

        private static void ConfigureCheck(CheckBox box, string text, int x, int y, int width)
        {
            box.Text = text;
            box.Location = new Point(x, y);
            box.Size = new Size(width, 24);
            box.ForeColor = TextPrimary;
            box.BackColor = Color.Transparent;
            box.FlatStyle = FlatStyle.Standard;
            box.Cursor = Cursors.Hand;
        }

        private static void AddSettingRow(Control card, string labelText, string unit, NumericUpDown input, int y, int minimum, int maximum)
        {
            card.Controls.Add(MakeLabel(labelText, 22, y + 4, 190, 24, 9F, FontStyle.Regular, TextPrimary));
            input.Minimum = minimum;
            input.Maximum = maximum;
            input.Location = new Point(242, y);
            input.Size = new Size(74, 25);
            input.BackColor = Color.FromArgb(8, 14, 25);
            input.ForeColor = TextPrimary;
            input.BorderStyle = BorderStyle.FixedSingle;
            card.Controls.Add(input);
            card.Controls.Add(MakeLabel(unit, 326, y + 4, 72, 24, 8.5F, FontStyle.Regular, TextMuted));
        }

        private void LoadValues()
        {
            automaticArming.Checked = settings.AutomaticArmingEnabled;
            idle.Value = settings.IdleMinutes;
            challenge.Value = settings.ChallengeSeconds;
            shutdown.Value = settings.ShutdownSeconds;
            video.Value = Math.Min(settings.VideoSeconds, settings.ShutdownSeconds);
            wrongReaction.Checked = settings.WrongCodeReaction;
            photo.Checked = settings.CapturePhoto;
            videoEnabled.Checked = settings.CaptureVideo;
            shutdownEnabled.Checked = settings.ShutdownEnabled;
            discordEnabled.Checked = settings.DiscordEnabled;
            webhook.Text = settings.DiscordWebhook;
            startup.Checked = StartupManager.IsEnabled();
            UpdateModeVisual();
        }

        private void UpdateModeVisual()
        {
            if (armedState)
            {
                modeLabel.Text = "●  ARMED";
                modeLabel.ForeColor = Color.White;
                modePill.BackColor = Color.FromArgb(24, 145, 87);
                return;
            }
            bool automatic = automaticArming.Checked;
            idle.Enabled = automatic;
            modeLabel.Text = automatic ? "●  AUTO MODE" : "●  MANUAL MODE";
            modeLabel.ForeColor = automatic ? Color.FromArgb(255, 193, 77) : Green;
            modePill.BackColor = automatic ? Color.FromArgb(78, 52, 14) : Color.FromArgb(13, 66, 46);
        }

        internal void SetArmedState(bool armed)
        {
            armedState = armed;
            UpdateModeVisual();
            status.ForeColor = armed ? Green : TextMuted;
            status.Text = armed ? "Guard is armed" : "Settings ready";
        }

        private void SaveClicked(object sender, EventArgs e)
        {
            string enteredWebhook = webhook.Text.Trim();
            if (discordEnabled.Checked && !String.IsNullOrEmpty(enteredWebhook) && !DiscordUploader.IsValidWebhook(enteredWebhook))
            {
                status.ForeColor = Color.FromArgb(255, 105, 105);
                status.Text = "Invalid Discord webhook";
                return;
            }
            settings.AutomaticArmingEnabled = automaticArming.Checked;
            settings.IdleMinutes = (int)idle.Value;
            settings.ChallengeSeconds = (int)challenge.Value;
            settings.ShutdownSeconds = (int)shutdown.Value;
            settings.VideoSeconds = Math.Min((int)video.Value, settings.ShutdownSeconds);
            video.Value = settings.VideoSeconds;
            settings.WrongCodeReaction = wrongReaction.Checked;
            settings.CapturePhoto = photo.Checked;
            settings.CaptureVideo = videoEnabled.Checked;
            settings.ShutdownEnabled = shutdownEnabled.Checked;
            settings.DiscordEnabled = discordEnabled.Checked && !String.IsNullOrEmpty(enteredWebhook);
            settings.DiscordWebhook = enteredWebhook;
            StartupManager.SetEnabled(startup.Checked);
            saveCallback();
            status.ForeColor = Green;
            status.Text = settings.AutomaticArmingEnabled ? "Saved · auto mode active" : "Saved · manual mode active";
        }
    }
}
