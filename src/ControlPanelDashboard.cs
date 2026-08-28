using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
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
        private readonly CheckBox hotkeyCtrl = new CheckBox();
        private readonly CheckBox hotkeyAlt = new CheckBox();
        private readonly CheckBox hotkeyShift = new CheckBox();
        private readonly ComboBox hotkeyKey = new ComboBox();
        private readonly TextBox unlockCode = new TextBox();
        private readonly CheckBox showCode = new CheckBox();
        private readonly Label status = new Label();
        private Panel modePill;
        private Label modeLabel;
        private bool armedState;
        private IncidentHistoryForm incidentHistory;

        internal ControlPanelForm(GuardSettings current, Action save, Action arm, Image appLogo)
        {
            settings = current;
            saveCallback = save;
            armCallback = arm;
            logo = appLogo;
            Text = "Tim Cook Guard";
            Icon = Icon.ExtractAssociatedIcon(Assembly.GetExecutingAssembly().Location);
            ClientSize = new Size(920, 720);
            MinimumSize = new Size(936, 759);
            MaximumSize = new Size(936, 759);
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

            Panel armCard = MakeCard(24, 132, 422, 438);
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

            armCard.Controls.Add(MakeLabel("Arm keybind", 22, 216, 100, 24, 9F, FontStyle.Regular, TextPrimary));
            ConfigureCheck(hotkeyCtrl, "Ctrl", 122, 212, 52);
            ConfigureCheck(hotkeyAlt, "Alt", 174, 212, 48);
            ConfigureCheck(hotkeyShift, "Shift", 222, 212, 60);
            armCard.Controls.Add(hotkeyCtrl);
            armCard.Controls.Add(hotkeyAlt);
            armCard.Controls.Add(hotkeyShift);
            hotkeyKey.DropDownStyle = ComboBoxStyle.DropDownList;
            hotkeyKey.Location = new Point(296, 212);
            hotkeyKey.Size = new Size(104, 25);
            hotkeyKey.BackColor = Color.FromArgb(8, 14, 25);
            hotkeyKey.ForeColor = TextPrimary;
            for (char key = 'A'; key <= 'Z'; key++) hotkeyKey.Items.Add(key.ToString());
            for (int key = 1; key <= 12; key++) hotkeyKey.Items.Add("F" + key);
            armCard.Controls.Add(hotkeyKey);

            armCard.Controls.Add(MakeLabel("Unlock code", 22, 256, 100, 24, 9F, FontStyle.Regular, TextPrimary));
            unlockCode.Location = new Point(142, 252);
            unlockCode.Size = new Size(192, 25);
            unlockCode.BackColor = Color.FromArgb(8, 14, 25);
            unlockCode.ForeColor = TextPrimary;
            unlockCode.BorderStyle = BorderStyle.FixedSingle;
            unlockCode.UseSystemPasswordChar = true;
            armCard.Controls.Add(unlockCode);
            ConfigureCheck(showCode, "Show", 340, 252, 60);
            showCode.CheckedChanged += delegate { unlockCode.UseSystemPasswordChar = !showCode.Checked; };
            armCard.Controls.Add(showCode);

            AddSettingRow(armCard, "Corner gesture", "seconds", challenge, 292, 1, 10);
            AddSettingRow(armCard, "Response deadline", "seconds", shutdown, 332, 5, 120);
            AddSettingRow(armCard, "Evidence video", "seconds", video, 372, 1, 30);

            Panel responseCard = MakeCard(468, 132, 428, 438);
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

            Panel footer = MakeCard(24, 590, 872, 104);
            Controls.Add(footer);
            ConfigureCheck(startup, "Launch Tim Cook Guard when I sign into Windows", 22, 17, 390);
            footer.Controls.Add(startup);
            footer.Controls.Add(MakeLabel("Startup keeps whichever arming mode you selected.", 43, 48, 390, 22, 8.5F, FontStyle.Regular, TextMuted));

            Button saveButton = MakeButton("SAVE DASHBOARD", Blue, Color.White);
            saveButton.Location = new Point(676, 22);
            saveButton.Size = new Size(172, 46);
            saveButton.Click += SaveClicked;
            footer.Controls.Add(saveButton);

            Button incidentsButton = MakeButton("VIEW INCIDENTS", Color.FromArgb(35, 48, 68), TextPrimary);
            incidentsButton.Location = new Point(470, 22);
            incidentsButton.Size = new Size(190, 46);
            incidentsButton.Click += delegate { ShowIncidentHistory(); };
            footer.Controls.Add(incidentsButton);

            status.Location = new Point(430, 73);
            status.Size = new Size(418, 22);
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
            hotkeyCtrl.Checked = (settings.HotkeyModifiers & HotkeyFormatter.Control) != 0;
            hotkeyAlt.Checked = (settings.HotkeyModifiers & HotkeyFormatter.Alt) != 0;
            hotkeyShift.Checked = (settings.HotkeyModifiers & HotkeyFormatter.Shift) != 0;
            hotkeyKey.SelectedItem = settings.HotkeyKey.ToString();
            if (hotkeyKey.SelectedIndex < 0) hotkeyKey.SelectedItem = "T";
            unlockCode.Text = settings.UnlockCode;
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
            int modifiers = (hotkeyCtrl.Checked ? HotkeyFormatter.Control : 0)
                | (hotkeyAlt.Checked ? HotkeyFormatter.Alt : 0)
                | (hotkeyShift.Checked ? HotkeyFormatter.Shift : 0);
            if (modifiers == 0 || hotkeyKey.SelectedItem == null)
            {
                status.ForeColor = Color.FromArgb(255, 105, 105);
                status.Text = "Choose a modifier and key";
                return;
            }
            string enteredCode = unlockCode.Text.Trim().ToLowerInvariant();
            if (!GuardSettings.IsValidUnlockCode(enteredCode))
            {
                status.ForeColor = Color.FromArgb(255, 105, 105);
                status.Text = "Code: 4–12 letters only";
                return;
            }
            Keys selectedKey = (Keys)Enum.Parse(typeof(Keys), Convert.ToString(hotkeyKey.SelectedItem), true);
            int oldModifiers = settings.HotkeyModifiers;
            Keys oldKey = settings.HotkeyKey;
            string oldCode = settings.UnlockCode;
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
            settings.HotkeyModifiers = modifiers;
            settings.HotkeyKey = selectedKey;
            settings.UnlockCode = enteredCode;
            StartupManager.SetEnabled(startup.Checked);
            try
            {
                saveCallback();
            }
            catch (Exception error)
            {
                settings.HotkeyModifiers = oldModifiers;
                settings.HotkeyKey = oldKey;
                settings.UnlockCode = oldCode;
                status.ForeColor = Color.FromArgb(255, 105, 105);
                status.Text = error.Message;
                return;
            }
            status.ForeColor = Green;
            status.Text = "Saved · " + HotkeyFormatter.Format(settings.HotkeyModifiers, settings.HotkeyKey);
        }

        private void ShowIncidentHistory()
        {
            if (incidentHistory == null || incidentHistory.IsDisposed)
                incidentHistory = new IncidentHistoryForm();
            incidentHistory.Show();
            incidentHistory.WindowState = FormWindowState.Normal;
            incidentHistory.Activate();
            incidentHistory.RefreshIncidents();
        }
    }

    internal sealed class IncidentHistoryForm : Form
    {
        private readonly ListView incidents = new ListView();
        private readonly PictureBox preview = new PictureBox();
        private readonly Label details = new Label();
        private Button openEvidence;
        private readonly Label emptyState = new Label();

        internal IncidentHistoryForm()
        {
            Text = "Tim Cook Guard · Incidents";
            Icon = Icon.ExtractAssociatedIcon(Assembly.GetExecutingAssembly().Location);
            ClientSize = new Size(900, 560);
            MinimumSize = new Size(760, 500);
            StartPosition = FormStartPosition.CenterParent;
            BackColor = Color.FromArgb(7, 12, 23);
            ForeColor = Color.FromArgb(240, 245, 252);
            Font = new Font("Segoe UI", 9F);
            BuildViewer();
            RefreshIncidents();
        }

        private void BuildViewer()
        {
            Label title = new Label();
            title.Text = "INCIDENT HISTORY";
            title.Font = new Font("Segoe UI Semibold", 18F, FontStyle.Bold);
            title.Location = new Point(22, 18);
            title.Size = new Size(360, 38);
            Controls.Add(title);

            Label subtitle = new Label();
            subtitle.Text = "Evidence saved locally in Downloads";
            subtitle.ForeColor = Color.FromArgb(148, 163, 184);
            subtitle.Location = new Point(25, 57);
            subtitle.Size = new Size(320, 24);
            Controls.Add(subtitle);

            Button refresh = MakeViewerButton("REFRESH");
            refresh.Location = new Point(656, 24);
            refresh.Click += delegate { RefreshIncidents(); };
            Controls.Add(refresh);

            Button downloads = MakeViewerButton("OPEN DOWNLOADS");
            downloads.Location = new Point(756, 24);
            downloads.Size = new Size(122, 34);
            downloads.Click += delegate { OpenPath(IncidentLog.DownloadsPath); };
            Controls.Add(downloads);

            incidents.Location = new Point(24, 94);
            incidents.Size = new Size(505, 438);
            incidents.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left;
            incidents.View = View.Details;
            incidents.FullRowSelect = true;
            incidents.HideSelection = false;
            incidents.MultiSelect = false;
            incidents.BackColor = Color.FromArgb(17, 25, 40);
            incidents.ForeColor = Color.FromArgb(240, 245, 252);
            incidents.BorderStyle = BorderStyle.FixedSingle;
            incidents.Columns.Add("When", 150);
            incidents.Columns.Add("Result", 145);
            incidents.Columns.Add("Evidence", 185);
            incidents.SelectedIndexChanged += delegate { ShowSelection(); };
            incidents.DoubleClick += delegate { OpenSelectedEvidence(); };
            Controls.Add(incidents);

            Panel previewCard = new Panel();
            previewCard.Location = new Point(548, 94);
            previewCard.Size = new Size(330, 438);
            previewCard.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            previewCard.BackColor = Color.FromArgb(17, 25, 40);
            previewCard.BorderStyle = BorderStyle.FixedSingle;
            Controls.Add(previewCard);

            preview.Location = new Point(14, 14);
            preview.Size = new Size(300, 278);
            preview.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            preview.BackColor = Color.Black;
            preview.SizeMode = PictureBoxSizeMode.Zoom;
            previewCard.Controls.Add(preview);

            emptyState.Text = "Select an incident to inspect its evidence.";
            emptyState.ForeColor = Color.FromArgb(148, 163, 184);
            emptyState.TextAlign = ContentAlignment.MiddleCenter;
            emptyState.Location = preview.Location;
            emptyState.Size = preview.Size;
            emptyState.BackColor = Color.Black;
            previewCard.Controls.Add(emptyState);
            emptyState.BringToFront();

            details.Location = new Point(14, 307);
            details.Size = new Size(300, 65);
            details.ForeColor = Color.FromArgb(189, 206, 229);
            details.AutoEllipsis = true;
            previewCard.Controls.Add(details);

            openEvidence = MakeViewerButton("OPEN SELECTED EVIDENCE");
            openEvidence.Location = new Point(14, 384);
            openEvidence.Size = new Size(300, 36);
            openEvidence.Enabled = false;
            openEvidence.Click += delegate { OpenSelectedEvidence(); };
            previewCard.Controls.Add(openEvidence);
        }

        private static Button MakeViewerButton(string text)
        {
            Button button = new Button();
            button.Text = text;
            button.Size = new Size(92, 34);
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderSize = 0;
            button.BackColor = Color.FromArgb(35, 48, 68);
            button.ForeColor = Color.White;
            button.Cursor = Cursors.Hand;
            return button;
        }

        internal void RefreshIncidents()
        {
            incidents.BeginUpdate();
            incidents.Items.Clear();
            try
            {
                if (File.Exists(IncidentLog.LogPath))
                {
                    string[] lines = File.ReadAllLines(IncidentLog.LogPath);
                    for (int index = lines.Length - 1; index >= 0; index--)
                    {
                        IncidentRecord record = ParseLine(lines[index]);
                        if (record == null)
                            continue;
                        ListViewItem item = new ListViewItem(record.When.ToString("MMM d, yyyy  h:mm:ss tt"));
                        item.SubItems.Add(record.Outcome.Replace('-', ' '));
                        item.SubItems.Add(record.Files.Count == 0 ? "None" : String.Join(", ", record.Files.ConvertAll(Path.GetExtension).ToArray()));
                        item.Tag = record;
                        incidents.Items.Add(item);
                    }
                }
            }
            catch (Exception error)
            {
                details.Text = "Could not load incidents: " + error.Message;
            }
            finally
            {
                incidents.EndUpdate();
            }
            if (incidents.Items.Count == 0)
            {
                emptyState.Text = "No incidents recorded yet.";
                emptyState.Visible = true;
            }
            else
            {
                incidents.Items[0].Selected = true;
            }
        }

        private static IncidentRecord ParseLine(string line)
        {
            string[] pieces = line.Split(new char[] { '|' }, 3);
            DateTime when;
            if (pieces.Length < 2 || !DateTime.TryParse(pieces[0].Trim(), out when))
                return null;
            IncidentRecord record = new IncidentRecord();
            record.When = when;
            record.Outcome = pieces[1].Trim();
            if (pieces.Length == 3 && !String.IsNullOrWhiteSpace(pieces[2]))
            {
                foreach (string name in pieces[2].Split(','))
                {
                    string fileName = name.Trim();
                    if (!String.IsNullOrEmpty(fileName))
                        record.Files.Add(Path.Combine(IncidentLog.DownloadsPath, fileName));
                }
            }
            return record;
        }

        private void ShowSelection()
        {
            DisposePreview();
            if (incidents.SelectedItems.Count == 0)
            {
                emptyState.Visible = true;
                openEvidence.Enabled = false;
                return;
            }
            IncidentRecord record = incidents.SelectedItems[0].Tag as IncidentRecord;
            if (record == null)
                return;
            List<string> existing = record.Files.FindAll(File.Exists);
            details.Text = record.When.ToString("F") + Environment.NewLine + record.Outcome.Replace('-', ' ') + Environment.NewLine
                + (existing.Count == 0 ? "No evidence file" : String.Join(", ", existing.ConvertAll(Path.GetFileName).ToArray()));
            openEvidence.Enabled = existing.Count > 0;
            string imagePath = existing.Find(delegate(string path)
            {
                string extension = Path.GetExtension(path);
                return extension.Equals(".jpg", StringComparison.OrdinalIgnoreCase) || extension.Equals(".png", StringComparison.OrdinalIgnoreCase);
            });
            if (imagePath != null)
            {
                try
                {
                    using (FileStream stream = File.OpenRead(imagePath))
                    using (Image source = Image.FromStream(stream))
                        preview.Image = new Bitmap(source);
                    emptyState.Visible = false;
                    return;
                }
                catch
                {
                }
            }
            emptyState.Text = existing.Count > 0 ? "Video evidence available.\nUse Open Selected Evidence." : "Evidence file is missing.";
            emptyState.Visible = true;
        }

        private void OpenSelectedEvidence()
        {
            if (incidents.SelectedItems.Count == 0)
                return;
            IncidentRecord record = incidents.SelectedItems[0].Tag as IncidentRecord;
            if (record == null)
                return;
            string path = record.Files.Find(File.Exists);
            if (path != null)
                OpenPath(path);
        }

        private static void OpenPath(string path)
        {
            try
            {
                Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
            }
            catch
            {
            }
        }

        private void DisposePreview()
        {
            if (preview.Image != null)
            {
                Image old = preview.Image;
                preview.Image = null;
                old.Dispose();
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
                DisposePreview();
            base.Dispose(disposing);
        }

        private sealed class IncidentRecord
        {
            internal DateTime When;
            internal string Outcome = String.Empty;
            internal readonly List<string> Files = new List<string>();
        }
    }
}
