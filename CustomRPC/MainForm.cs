// ==============================================================================
// RICHIFY - Discord Custom Rich Presence Manager
// Developed by SasynDev (https://github.com/SasynDev)
// Copyright (c) 2026 SasynDev. All rights reserved.
// Licensed under the MIT License.
// ==============================================================================

using CommonMark;
using DiscordRPC;
using DiscordRPC.Helper;
using Microsoft.AppCenter.Analytics;
using Microsoft.AppCenter.Crashes;
using Octokit;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using System.Xml.Serialization;
using Application = System.Windows.Forms.Application;
using Button = System.Windows.Forms.Button;
using DButton = DiscordRPC.Button;
using Timer = System.Timers.Timer;

namespace RICHIFY
{
    /*
     * TODO: profiles not as external files
     */

    /// <summary>
    /// A struct for handling preset importing/exporting.
    /// </summary>
    [Serializable]
    public struct Preset
    {
        public string ID;
        public int Type;
        public int Display;
        public string Name;
        public string Details;
        public string DetailsURL;
        public string State;
        public string StateURL;
        public int PartySize;
        public int PartyMax;
        public int Timestamps;
        public DateTime CustomTimestamp;
        public bool CustomTimestampEndEnabled;
        public DateTime CustomTimestampEnd;
        public string LargeKey;
        public string LargeText;
        public string LargeURL;
        public string SmallKey;
        public string SmallText;
        public string SmallURL;
        public string Button1Text;
        public string Button1URL;
        public string Button2Text;
        public string Button2URL;
    }

    /// <summary>
    /// A struct for getting available image assets for current application.
    /// </summary>
    public struct ImageAssets
    {
        public string ID;
        public string Type;
        public string Name;
    }

    /// <summary>
    /// A struct describing activity types to use in a type selection combobox.
    /// </summary>
    public struct PresenceType
    {
        public string Name { get; private set; }
        public ActivityType Type { get; private set; }

        public PresenceType(string name, ActivityType type)
        {
            Name = name;
            Type = type;
        }
    }

    /// <summary>
    /// A struct describing display types to use in a display selection combobox.
    /// </summary>
    public struct DisplayType
    {
        public string Name { get; private set; }
        public StatusDisplayType Type { get; private set; }

        public DisplayType(StatusDisplayType type)
        {
            Name = type.ToString();
            Type = type;
        }
    }

    /// <summary>
    /// An enum for the timestamp setting.
    /// </summary>
    public enum TimestampType
    {
        SinceLastConnection = 0,
        SinceStartup = 1,
        LocalTime = 2,
        Custom = 3, // These are hardcoded for backwards compatibility
        SincePresenceUpdate
    }

    public partial class MainForm : Form
    {
        /// <summary>
        /// Discord RPC Client.
        /// </summary>
        DiscordRpcClient client;

        /// <summary>
        /// List of presence buttons.
        /// </summary>
        List<DButton> buttonsList = new List<DButton>();

        /// <summary>
        /// Prevents some event handlers from executing while the app is loading.
        /// </summary>
        bool loading = true;
        /// <summary>
        /// Avoids recursion in <see cref="OnlyNumbers"/>.
        /// </summary>
        /// <remarks>
        /// ...This is stupid.
        /// </remarks>
        bool toAvoidRecursion = false;

        /// <summary>
        /// This bool makes sure the "The app will run in the tray" tooltip shows only once per session
        /// </summary>
        bool wasTooltipShown = false;

        /// <summary>
        /// A timer for automatic restart on connection error. Currently set to 10 seconds.
        /// </summary>
        Timer restartTimer = new Timer(10 * 1000);
        /// <summary>
        /// Limit for the amount of restart tries.
        /// </summary>
        int restartAttempts = 30;
        /// <summary>
        /// Counter for restart attempts left.
        /// </summary>
        int restartAttemptsLeft = -1;

        /// <summary>
        /// A timer that updates presence at midnight.
        /// </summary>
        Timer localTimeTimer = new Timer();

        /// <summary>
        /// Settings of the application. Self-explanatory.
        /// </summary>
        Properties.Settings settings = Properties.Settings.Default;

        /// <summary>
        /// GitHub client used for fetching all the releases of the app.
        /// </summary>
        GitHubClient githubClient = new GitHubClient(new ProductHeaderValue("RICHIFY"));
        /// <summary>
        /// Latest release of the app available for downloading.
        /// </summary>
        Release latestRelease;

        /// <summary>
        /// A DateTime object showing since what moment you can make an API request in <see cref="FetchAssets(object, EventArgs)"/>.
        /// </summary>
        DateTime nextAssetCheck = DateTime.Now;
        /// <summary>
        /// A string showing what application ID was checked last in <see cref="FetchAssets(object, EventArgs)"/>.
        /// </summary>
        string lastIDChecked = "";

        /// <summary>
        /// A part of the URL path for docs.customrp.xyz links used in the app.
        /// Has the form of empty string if the app is in English or the docs aren't translated to the current UI language, "v/[locale]/" or "v/[locale]-[country]/" otherwise.
        /// </summary>
        string localeUrl = "";

        /// <summary>
        /// Timestamp of when was the last time app was connected to Discord.
        /// </summary>
        DateTime timestampConnected = DateTime.UtcNow;

        /// <summary>
        /// Timestamp of when the app started.
        /// </summary>
        readonly DateTime timestampStarted = DateTime.UtcNow;

        /// <summary>
        /// Path to the autorun link file.
        /// </summary>
        readonly string linkPath = Environment.GetFolderPath(Environment.SpecialFolder.Startup) + @"\RICHIFY" + (Program.IsSecondInstance ? " 2" : "") + ".lnk";

        /// <summary>
        /// Resource manager. Yes I know, very descriptive.
        /// </summary>
        readonly System.ComponentModel.ComponentResourceManager res = new System.ComponentModel.ComponentResourceManager(typeof(MainForm));

        /// <summary>
        /// List of locales docs.customrp.xyz is translated to.
        /// </summary>
        readonly List<string> translatedWikiLocales = new List<string> { "de", "es", "fi", "fr", "hi", "ko", "nl", "pl", "ro", "ru", "th", "uk", "vi" };

        /// <summary>
        /// List of available activity types.
        /// </summary>
        readonly List<PresenceType> presenceTypes = new List<PresenceType>
            {
                new PresenceType(Strings.presenceTypePlaying, ActivityType.Playing),
                new PresenceType(Strings.presenceTypeListening, ActivityType.Listening),
                new PresenceType(Strings.presenceTypeWatching, ActivityType.Watching),
                new PresenceType(Strings.presenceTypeCompeting, ActivityType.Competing),
            };

        /// <summary>
        /// List of available display types.
        /// </summary>
        readonly List<DisplayType> displayTypes = new List<DisplayType>
            {
                new DisplayType(StatusDisplayType.Name),
                new DisplayType(StatusDisplayType.Details),
                new DisplayType(StatusDisplayType.State),
            };

        /// <summary>
        /// Unicode character "No-Break Space" (" ").
        /// </summary>
        readonly string U00A0 = "\u00A0";
        /// <summary>
        /// Unicode character "Zero-Width Space" (invisible).
        /// </summary>
        readonly string U200B = "\u200B";

        /// <summary>
        /// Default ID to connect with if the user doesn't provide any.
        /// </summary>
        readonly string defaultID = "896771305108553788";

        /// <summary>
        /// The constructor of the form.
        /// </summary>
        /// <param name="preset">File location of a preset to load on startup or <see langword="null"/>.</param>
        public MainForm(string preset)
        {
            InitializeComponent();
            DoubleBuffered = true;

            // Populating language related menu items
            Utils.LanguagesSetup(translatorsToolStripMenuItem, OpenPersonsPage, languageToolStripMenuItem, ChangeLanguage);

            // Populating supporters menu items
            Utils.SupportersSetup(supportersToolStripMenuItem, OpenPersonsPage);

            // Setting up dark/light mode
            ThemeSetup();

            // Setting up startup link for current user (enabled by default)
#if !DEBUG
            StartupSetup();
#endif

            // Setting up a restart timer
            restartTimer.AutoReset = false;
            restartTimer.Elapsed += RestartTimer_Elapsed;

            // Setting up a midnight presence update timer
            localTimeTimer.AutoReset = false;
            localTimeTimer.Elapsed += LocalTimeTimer_Elapsed;

            // If we supply a preset file to import on load, load it right away
            if (preset is string)
                LoadPreset(preset);

            // Setting up checkboxes because apparently property binding doesn't work
            runOnStartupToolStripMenuItem.Checked = settings.runOnStartup;
            startMinimizedToolStripMenuItem.Checked = settings.startMinimized;
            autoconnectToolStripMenuItem.Checked = settings.autoconnect;
            checkUpdatesToolStripMenuItem.Checked = settings.checkUpdates;
            allowAnalyticsToolStripMenuItem.Checked = Analytics.IsEnabledAsync().Result;
            settings.darkMode = true;

            InitWindowAnimation();
            InitButtonMotion();
            AdjustCustomLayout();

            // Helper function that recursively gets all DropDownItems
            IEnumerable<ToolStripItem> GetAllDropDownItems(ToolStripDropDownItem item)
            {
                foreach (ToolStripItem subItem in item.DropDownItems)
                {
                    yield return subItem;

                    if (subItem is ToolStripDropDownItem subContainer)
                    {
                        foreach (ToolStripItem nestedItem in GetAllDropDownItems(subContainer))
                        {
                            yield return nestedItem;
                        }
                    }
                }
            }

            // Checks the chosen language setting
            foreach (var toolStripItemObj in GetAllDropDownItems(languageToolStripMenuItem))
            {
                if (toolStripItemObj is ToolStripSeparator)
                    continue;

                var langItem = (ToolStripMenuItem)toolStripItemObj;

                if ((string)langItem.Tag == settings.language)
                {
                    langItem.Checked = true;
                    break;
                }
            }

            // Setting up activity type combobox
            comboBoxType.DisplayMember = "Name";
            comboBoxType.ValueMember = "Type";
            comboBoxType.DataSource = presenceTypes;
            comboBoxType.SelectedValue = (ActivityType)settings.type;

            // Setting up display combobox
            comboBoxDisplay.DisplayMember = "Name";
            comboBoxDisplay.ValueMember = "Type";
            comboBoxDisplay.DataSource = displayTypes;
            comboBoxDisplay.SelectedValue = (StatusDisplayType)settings.display;

            // Set up tags for the radio buttons
            radioButtonLastConnection.Tag = TimestampType.SinceLastConnection;
            radioButtonStartTime.Tag = TimestampType.SinceStartup;
            radioButtonPresence.Tag = TimestampType.SincePresenceUpdate;
            radioButtonLocalTime.Tag = TimestampType.LocalTime;
            radioButtonCustom.Tag = TimestampType.Custom;

            // Checks the needed timestamp radiobuttons because settings binding can't do that
            switch ((TimestampType)settings.timestamps)
            {
                case TimestampType.SinceLastConnection: radioButtonLastConnection.Checked = true; break;
                case TimestampType.SincePresenceUpdate: radioButtonPresence.Checked = true; break;
                case TimestampType.SinceStartup: radioButtonStartTime.Checked = true; break;
                case TimestampType.LocalTime: radioButtonLocalTime.Checked = true; break;
                case TimestampType.Custom: radioButtonCustom.Checked = true; break;
            }

            // Enable or disable the date and time pickers depending on whether a custom timestamp setting is chosen
            tableLayoutPanelCustomTimestamps.Enabled = radioButtonCustom.Checked;

            // Enable or disable End timestamp picker
            dateTimePickerTimestampEnd.Enabled = checkBoxTimestampEnd.Checked;

            // Change the date and time pickers' format according to system's culture
            dateTimePickerTimestampStart.CustomFormat = dateTimePickerTimestampEnd.CustomFormat =
                CultureInfo.CurrentCulture.DateTimeFormat.ShortDatePattern + " "
                + CultureInfo.CurrentCulture.DateTimeFormat.LongTimePattern;

            // If the app was launched for the first time (including since update), set the default time to current one
            if (settings.customTimestamp.CompareTo(new DateTime(1969, 1, 1, 0, 0, 0)) == 0)
                settings.customTimestamp = DateTime.Now;
            if (settings.customTimestampEnd.CompareTo(new DateTime(1969, 1, 1, 0, 0, 0)) == 0)
                settings.customTimestampEnd = DateTime.Now;

            // Change the earliest date user can choose (1 in unix seconds) according to user's timezone
            dateTimePickerTimestampStart.MinDate = dateTimePickerTimestampEnd.MinDate =
                new DateTime(1970, 1, 1, 0, 0, 1, DateTimeKind.Utc).ToLocalTime();

            // Change the latest date user can choose (99999999999 in unix seconds) according to user's timezone
            // Umm al-Qura calendar only supports dates up to 2077-11-16, in case there are others this is generalized
            DateTime maxDate = new DateTime(5138, 11, 16, 9, 46, 39, DateTimeKind.Utc).ToLocalTime();
            Calendar calendar = CultureInfo.CurrentCulture.Calendar;

            dateTimePickerTimestampStart.MaxDate = dateTimePickerTimestampEnd.MaxDate =
                (calendar.MaxSupportedDateTime < maxDate) ? calendar.MaxSupportedDateTime : maxDate;

            // Localize the header of the tooltip because Visual Studio can't do that for some reason
            toolTipInfo.ToolTipTitle = Strings.information;

            // Localize the Disconnect button in the tray menu, unless it is already localized
            if (trayMenuDisconnect.Text == res.GetString("trayMenuDisconnect.Text", CultureInfo.GetCultureInfo("en")))
                trayMenuDisconnect.Text = res.GetString("buttonDisconnect.Text");

            // Localize the statusbar text in case the autoconnect is disabled
            toolStripStatusLabelStatus.Text = Strings.statusDisconnected;

            // Add version info to main window title
            var execVer = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
            Text = "RICHIFY " + execVer.Major + "." + execVer.Minor;
#if DEBUG
            Text += " DEV";
#endif

            // Slightly changing the name of the tray tooptip and main window title for a second instance of the app
            if (Program.IsSecondInstance)
            {
                trayIcon.Text += " (2)";
                Text += " (2)";
            }

            // Set up a localeUrl variable if docs are translated to the current UI language.
            if (translatedWikiLocales.FindLast(localePredicate) is string locale && locale != "")
                localeUrl = "v/" + locale + '/';

            bool localePredicate(string loc)
            {
                var currentLocale = CultureInfo.CurrentUICulture.Name;
                return loc == currentLocale || loc == currentLocale.Split('-')[0];
            }

            // Set up shortcuts for certain menu elements
            newPresetToolStripMenuItem.ShortcutKeys = Keys.Control | Keys.N;
            loadPresetToolStripMenuItem.ShortcutKeys = Keys.Control | Keys.O;
            savePresetToolStripMenuItem.ShortcutKeys = Keys.Control | Keys.S;
            uploadAssetsToolStripMenuItem.ShortcutKeys = Keys.Control | Keys.U;
            openTheManualToolStripMenuItem.ShortcutKeys = Keys.F1;

            loading = false;

            // Always smoothly show window on startup
            SmoothShow();

            // Ensure UI input fields are completely clean and empty (no pre-filled defaults, no placeholders).
            // Background presence automatically sends default RICHIFY branding when fields are left blank.
            if (settings.name == "RICHIFY") { settings.name = ""; textBoxName.Text = ""; }
            if (settings.details == "RICHIFY") { settings.details = ""; textBoxDetails.Text = ""; }
            if (settings.state == "Build by SasynDev") { settings.state = ""; textBoxState.Text = ""; }
            if (settings.largeText == "RICHIFY") { settings.largeText = ""; textBoxLargeText.Text = ""; }
            if (settings.smallText == "Build by SasynDev") { settings.smallText = ""; textBoxSmallText.Text = ""; }
            if (settings.button1Text == "SasynDev") { settings.button1Text = ""; textBoxButton1Text.Text = ""; }
            if (settings.button1URL != null && settings.button1URL.Contains("github.com/SasynDev")) { settings.button1URL = ""; textBoxButton1URL.Text = ""; }
            if (settings.button2Text == "Discord") { settings.button2Text = ""; textBoxButton2Text.Text = ""; }
            if (settings.button2URL != null && settings.button2URL.Contains("discord.gg")) { settings.button2URL = ""; textBoxButton2URL.Text = ""; }
            if (settings.largeKey != null && settings.largeKey.Contains("logo.png")) { settings.largeKey = ""; comboBoxLargeKey.Text = ""; }
            if (settings.id == "896771305108553788") { settings.id = ""; textBoxID.Text = ""; }

            if (settings.firstStart)
            {
                settings.id = "";
                textBoxID.Text = "";
                settings.name = "";
                textBoxName.Text = "";
                settings.details = "";
                textBoxDetails.Text = "";
                settings.state = "";
                textBoxState.Text = "";
                settings.largeText = "";
                textBoxLargeText.Text = "";
                settings.smallText = "";
                textBoxSmallText.Text = "";
                settings.button1Text = "";
                textBoxButton1Text.Text = "";
                settings.button1URL = "";
                textBoxButton1URL.Text = "";
                settings.button2Text = "";
                textBoxButton2Text.Text = "";
                settings.button2URL = "";
                textBoxButton2URL.Text = "";
                settings.largeKey = "";
                comboBoxLargeKey.Text = "";
                settings.smallKey = "";
                comboBoxSmallKey.Text = "";
                settings.firstStart = false;
                Utils.SaveSettings();
            }

            Icon = Properties.Resources.favicon;
            trayIcon.Icon = Properties.Resources.favicon;

            toolStripStatusLabelUsername.Image = Properties.Resources.logo;
            toolStripStatusLabelUsername.TextImageRelation = TextImageRelation.ImageBeforeText;
            toolStripStatusLabelUsername.ImageAlign = ContentAlignment.MiddleLeft;

            if ((settings.changedLanguage && settings.wasConnected) || (settings.autoconnect && !settings.changedLanguage))
                Connect();

            CheckIfCrashed();

            if (settings.checkUpdates)
                CheckForUpdates();

            settings.changedLanguage = false;
        }

        /// <summary>
        /// Handles communication between instances.
        /// </summary>
        protected override void WndProc(ref Message message)
        {
            if (message.Msg == Program.WM_SHOWFIRSTINSTANCE)
            {
                MaximizeFromTray();
            }
            else if (message.Msg == Program.WM_IMPORTPRESET)
            {
                LoadPreset(File.ReadAllText(Program.IPCPath));
            }
            else if (message.Msg == 0x0016) // WM_ENDSESSION
            {
                Utils.SaveSettings();
                Application.Exit();
            }

            base.WndProc(ref message);
        }

        private System.Windows.Forms.Timer fadeTimer;
        private double targetOpacity = 1.0;
        private bool isClosingOrHiding = false;
        private bool isExiting = false;

        private void InitWindowAnimation()
        {
            if (fadeTimer != null) return;
            fadeTimer = new System.Windows.Forms.Timer();
            fadeTimer.Interval = 12; // ~80 FPS ultra-smooth transition
            fadeTimer.Tick += (s, e) =>
            {
                if (isClosingOrHiding)
                {
                    if (Opacity > 0.08)
                    {
                        Opacity -= 0.12;
                    }
                    else
                    {
                        fadeTimer.Stop();
                        Opacity = 0;
                        if (isExiting)
                        {
                            Application.Exit();
                        }
                        else
                        {
                            Hide();
                            WindowState = FormWindowState.Minimized;
                            Opacity = 1.0;
                            isClosingOrHiding = false;
                            trayIcon.ShowBalloonTip(1000, "RICHIFY", "RICHIFY background mein chal raha hai. Discord presence active hai!", ToolTipIcon.Info);
                        }
                    }
                }
                else
                {
                    if (Opacity < targetOpacity)
                    {
                        Opacity = Math.Min(targetOpacity, Opacity + 0.10);
                    }
                    else
                    {
                        Opacity = targetOpacity;
                        fadeTimer.Stop();
                    }
                }
            };
        }

        public void SmoothShow()
        {
            isClosingOrHiding = false;
            Opacity = 1.0;
            Show();
            WindowState = FormWindowState.Normal;
            BringToFront();
            Activate();
            Invalidate();
        }

        public void SmoothHide(bool exit = false)
        {
            if (isClosingOrHiding) return;
            isClosingOrHiding = true;
            isExiting = exit;
            targetOpacity = 0.0;
            fadeTimer?.Start();
        }

        private bool buttonMotionInitialized = false;

        private void InitButtonMotion()
        {
            if (buttonMotionInitialized) return;
            buttonMotionInitialized = true;

            SetupButtonMotion(buttonUpdatePresence, 
                Color.FromArgb(237, 66, 69), 
                Color.FromArgb(255, 78, 83), 
                Color.FromArgb(205, 45, 50), 
                Color.FromArgb(255, 105, 110), 
                Color.FromArgb(255, 160, 165));

            SetupButtonMotion(buttonConnect, 
                Color.FromArgb(18, 26, 44), 
                Color.FromArgb(24, 62, 46), 
                Color.FromArgb(14, 38, 28), 
                Color.FromArgb(46, 66, 110), 
                Color.FromArgb(46, 204, 113));

            SetupButtonMotion(buttonDisconnect, 
                Color.FromArgb(18, 26, 44), 
                Color.FromArgb(62, 24, 34), 
                Color.FromArgb(38, 14, 20), 
                Color.FromArgb(46, 66, 110), 
                Color.FromArgb(237, 66, 69));
        }

        private void SetupButtonMotion(Button btn, Color normalBg, Color hoverBg, Color pressBg, Color normalBorder, Color hoverBorder)
        {
            btn.FlatStyle = FlatStyle.Flat;
            btn.FlatAppearance.BorderSize = 1;
            btn.FlatAppearance.BorderColor = normalBorder;
            btn.BackColor = normalBg;
            btn.Cursor = Cursors.Hand;
            btn.Font = new Font("Segoe UI", 9F, FontStyle.Bold);

            bool isHovered = false;
            bool isPressed = false;

            btn.MouseEnter += (s, e) =>
            {
                isHovered = true;
                if (btn.Enabled)
                {
                    btn.BackColor = hoverBg;
                    btn.FlatAppearance.BorderColor = hoverBorder;
                    btn.Invalidate();
                }
            };

            btn.MouseLeave += (s, e) =>
            {
                isHovered = false;
                isPressed = false;
                if (btn.Enabled)
                {
                    btn.BackColor = normalBg;
                    btn.FlatAppearance.BorderColor = normalBorder;
                    btn.Invalidate();
                }
            };

            btn.MouseDown += (s, e) =>
            {
                if (e.Button == MouseButtons.Left && btn.Enabled)
                {
                    isPressed = true;
                    btn.BackColor = pressBg;
                    btn.Invalidate();
                }
            };

            btn.MouseUp += (s, e) =>
            {
                isPressed = false;
                if (btn.Enabled)
                {
                    btn.BackColor = isHovered ? hoverBg : normalBg;
                    btn.FlatAppearance.BorderColor = isHovered ? hoverBorder : normalBorder;
                    btn.Invalidate();
                }
            };

            btn.EnabledChanged += (s, e) =>
            {
                if (!btn.Enabled)
                {
                    isHovered = false;
                    isPressed = false;
                }
                else
                {
                    btn.BackColor = normalBg;
                    btn.FlatAppearance.BorderColor = normalBorder;
                }
                btn.Invalidate();
            };

            btn.Paint += (s, e) =>
            {
                if (!btn.Enabled) return;

                var g = e.Graphics;
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

                if (isPressed)
                    g.TranslateTransform(0, 1);

                var rect = new Rectangle(0, 0, btn.Width - 1, btn.Height - 1);
                using (var path = GetRoundedRectPath(rect, 6))
                {
                    if (isHovered)
                    {
                        using (var glowPen = new Pen(Color.FromArgb(90, hoverBorder), 2f))
                        {
                            g.DrawPath(glowPen, path);
                        }
                    }

                    using (var specPen = new Pen(Color.FromArgb(50, 255, 255, 255), 1f))
                    {
                        g.DrawLine(specPen, 6, 1, btn.Width - 7, 1);
                    }
                }
            };
        }

        /// <summary>
        /// Sets up color scheme of the application.
        /// </summary>
        private void ThemeSetup()
        {
            if (WinApi.UseImmersiveDarkMode(Handle))
            {
                // Force redraw title bar for immersive dark frame
                Opacity = 0.99;
                Opacity = 1;
            }

            CurrentColors.Update();

            BackColor = CurrentColors.BgColor;
            ForeColor = CurrentColors.TextColor;

            var allControls = Controls.Cast<Control>()
                .Concat(flowLayoutPanelParty.Controls.Cast<Control>())
                .Concat(panelTimestamps.Controls.Cast<Control>())
                .Concat(tableLayoutPanelCustomTimestamps.Controls.Cast<Control>());

            foreach (Control ctrl in allControls)
            {
                if (ctrl is TextBox tb)
                {
                    tb.BackColor = CurrentColors.BgTextFields;
                    tb.ForeColor = CurrentColors.TextColor;
                    tb.BorderStyle = BorderStyle.FixedSingle;
                }
                else if (ctrl is ComboBox cb)
                {
                    cb.BackColor = CurrentColors.BgTextFields;
                    cb.ForeColor = CurrentColors.TextColor;
                    cb.FlatStyle = FlatStyle.Flat;
                }
                else if (ctrl is NumericUpDown nud)
                {
                    nud.BackColor = CurrentColors.BgTextFields;
                    nud.ForeColor = CurrentColors.TextColor;
                    nud.BorderStyle = BorderStyle.FixedSingle;
                }
                else if (ctrl is Panel panel && panel.Name.StartsWith("panelSeparator"))
                {
                    panel.Visible = false; // Hide legacy line separators to eliminate overlaps
                }
                else if (ctrl is System.Windows.Forms.Label || ctrl is RadioButton || ctrl is CheckBox || ctrl is Panel || ctrl is FlowLayoutPanel || ctrl is TableLayoutPanel)
                {
                    ctrl.BackColor = Color.Transparent;
                    ctrl.ForeColor = CurrentColors.TextColor;
                }
            }

            textBoxID.BackColor = CurrentColors.BgTextFields;

            ToolStripManager.Renderer = new DarkModeRenderer();
            menuStrip.BackColor = Color.FromArgb(10, 13, 22);
            menuStrip.ForeColor = CurrentColors.TextColor;
            statusStrip.BackColor = Color.FromArgb(8, 10, 18);
            statusStrip.ForeColor = Color.FromArgb(180, 195, 225);

            InitButtonMotion();
            AdjustCustomLayout();

            Invalidate();
        }

        private bool inCustomLayout = false;
        private void AdjustCustomLayout()
        {
            if (inCustomLayout || tableLayoutPanelButtons == null) return;
            try
            {
                inCustomLayout = true;
                if (ClientSize.Width != 480 || ClientSize.Height != 642)
                {
                    ClientSize = new Size(480, 642);
                }
                var targetLoc = new Point(8, 564);
                var targetSize = new Size(ClientSize.Width - 16, 46);
                if (tableLayoutPanelButtons.Location != targetLoc)
                    tableLayoutPanelButtons.Location = targetLoc;
                if (tableLayoutPanelButtons.Size != targetSize)
                    tableLayoutPanelButtons.Size = targetSize;
                var targetPad = new Padding(10, 0, 10, 0);
                if (tableLayoutPanelButtons.Padding != targetPad)
                    tableLayoutPanelButtons.Padding = targetPad;
            }
            finally
            {
                inCustomLayout = false;
            }
        }

        protected override void OnLayout(LayoutEventArgs levent)
        {
            base.OnLayout(levent);
            AdjustCustomLayout();
        }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ExStyle |= 0x02000000; // WS_EX_COMPOSITED: hardware double-buffering via DWM
                return cp;
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            var g = e.Graphics;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
            g.CompositingQuality = System.Drawing.Drawing2D.CompositingQuality.HighQuality;
            g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;

            // 1. Deep frosted obsidian backdrop
            using (var bgBrush = new SolidBrush(Color.FromArgb(7, 10, 18)))
            {
                g.FillRectangle(bgBrush, ClientRectangle);
            }

            // 2. High-Quality Frosted Ambient Nebulas (Atmospheric Multilayered Glows)
            // Luminous Electric Cyan glow at top-left
            using (var path1 = new System.Drawing.Drawing2D.GraphicsPath())
            {
                path1.AddEllipse(-90, -70, 460, 340);
                using (var pgb1 = new System.Drawing.Drawing2D.PathGradientBrush(path1))
                {
                    pgb1.CenterColor = Color.FromArgb(70, 0, 190, 255);
                    pgb1.SurroundColors = new Color[] { Color.Transparent };
                    g.FillPath(pgb1, path1);
                }
            }

            // Vibrant Neon Ultraviolet glow at center-right
            using (var path2 = new System.Drawing.Drawing2D.GraphicsPath())
            {
                path2.AddEllipse(ClientSize.Width - 290, 120, 390, 340);
                using (var pgb2 = new System.Drawing.Drawing2D.PathGradientBrush(path2))
                {
                    pgb2.CenterColor = Color.FromArgb(75, 140, 45, 255);
                    pgb2.SurroundColors = new Color[] { Color.Transparent };
                    g.FillPath(pgb2, path2);
                }
            }

            // Glowing Discord Crimson Red beacon near bottom-right / action buttons
            using (var path3 = new System.Drawing.Drawing2D.GraphicsPath())
            {
                path3.AddEllipse(ClientSize.Width - 250, ClientSize.Height - 190, 300, 260);
                using (var pgb3 = new System.Drawing.Drawing2D.PathGradientBrush(path3))
                {
                    pgb3.CenterColor = Color.FromArgb(70, 237, 66, 69);
                    pgb3.SurroundColors = new Color[] { Color.Transparent };
                    g.FillPath(pgb3, path3);
                }
            }

            // Royal Indigo ambient glow at bottom-left
            using (var path4 = new System.Drawing.Drawing2D.GraphicsPath())
            {
                path4.AddEllipse(-70, ClientSize.Height - 270, 340, 270);
                using (var pgb4 = new System.Drawing.Drawing2D.PathGradientBrush(path4))
                {
                    pgb4.CenterColor = Color.FromArgb(55, 45, 105, 245);
                    pgb4.SurroundColors = new Color[] { Color.Transparent };
                    g.FillPath(pgb4, path4);
                }
            }

            // Subtle luminous center core
            using (var path5 = new System.Drawing.Drawing2D.GraphicsPath())
            {
                path5.AddEllipse(40, 160, 400, 300);
                using (var pgb5 = new System.Drawing.Drawing2D.PathGradientBrush(path5))
                {
                    pgb5.CenterColor = Color.FromArgb(30, 80, 140, 255);
                    pgb5.SurroundColors = new Color[] { Color.Transparent };
                    g.FillPath(pgb5, path5);
                }
            }

            // 3. Draw Translucent Frosted Glass Card Containers behind sections
            // Card 1: ID, Type, Display (Y: 28 to 65)
            DrawGlassCard(g, new Rectangle(8, 28, ClientSize.Width - 16, 38), 8);

            // Card 2: Name, Details, State, Party (Y: 71 to 189)
            DrawGlassCard(g, new Rectangle(8, 71, ClientSize.Width - 16, 118), 8);

            // Card 3: Timestamps (Y: 195 to 359)
            DrawGlassCard(g, new Rectangle(8, 195, ClientSize.Width - 16, 164), 8);

            // Card 4: Large & Small Image (Y: 365 to 468)
            DrawGlassCard(g, new Rectangle(8, 365, ClientSize.Width - 16, 103), 8);

            // Card 5: Buttons 1 & 2 Inputs (Y: 473 to 553 - comfortably holds URL inputs)
            DrawGlassCard(g, new Rectangle(8, 473, ClientSize.Width - 16, 80), 8);

            // Card 6: Action Floating Dock (Y: 564 to 610 - 11px clear gap from Card 5)
            DrawGlassCard(g, new Rectangle(8, 564, ClientSize.Width - 16, 46), 8);
        }

        private static void DrawGlassCard(Graphics g, Rectangle rect, int radius)
        {
            using (var path = GetRoundedRectPath(rect, radius))
            {
                // Soft elevation ambient shadow
                using (var shadowPath = GetRoundedRectPath(new Rectangle(rect.X, rect.Y + 2, rect.Width, rect.Height), radius))
                using (var shadowBrush = new SolidBrush(Color.FromArgb(32, 0, 0, 0)))
                {
                    g.FillPath(shadowBrush, shadowPath);
                }

                // Top-to-bottom frosted glass dual-tone gradient fill
                using (var lgb = new System.Drawing.Drawing2D.LinearGradientBrush(
                    rect,
                    Color.FromArgb(28, 255, 255, 255),
                    Color.FromArgb(9, 255, 255, 255),
                    System.Drawing.Drawing2D.LinearGradientMode.Vertical))
                {
                    g.FillPath(lgb, path);
                }

                // 1px Specular perimeter border
                using (var borderPen = new Pen(Color.FromArgb(38, 255, 255, 255), 1f))
                {
                    g.DrawPath(borderPen, path);
                }

                // Crisp specular top refraction line
                using (var specPen = new Pen(Color.FromArgb(75, 255, 255, 255), 1f))
                {
                    g.DrawLine(specPen, rect.X + radius, rect.Y + 1, rect.Right - radius, rect.Y + 1);
                }
            }
        }

        private static System.Drawing.Drawing2D.GraphicsPath GetRoundedRectPath(Rectangle r, int radius)
        {
            var path = new System.Drawing.Drawing2D.GraphicsPath();
            int d = radius * 2;
            path.AddArc(r.X, r.Y, d, d, 180, 90);
            path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        /// <summary>
        /// Will be called 10 seconds after a failed connection to try and reconnect.
        /// </summary>
        private void RestartTimer_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            if (restartAttemptsLeft == 0)
            {
                restartAttemptsLeft = -1;
                Invoke(new MethodInvoker(() => Disconnect()));
                return;
            }

            if (restartAttemptsLeft == -1)
                restartAttemptsLeft = restartAttempts;

            restartAttemptsLeft--;

            Invoke(new MethodInvoker(() => Connect()));
        }

        /// <summary>
        /// Will be called at midnight to update presence.
        /// </summary>
        private void LocalTimeTimer_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            Invoke(new MethodInvoker(() => SetPresence()));
        }

        /// <summary>
        /// Checks if the app has crashed in the previous session.
        /// </summary>
        private async void CheckIfCrashed()
        {
            if (!await Crashes.HasCrashedInLastSessionAsync())
                return;

            var report = await Crashes.GetLastSessionCrashReportAsync();

            new ErrorReportViewer(report.StackTrace).ShowDialog();
        }

        /// <summary>
        /// Checks for updates.
        /// </summary>
        /// <param name="manual"><see langword="True"/> if the user requested the check, <see langword="false"/> otherwise.</param>
        private async void CheckForUpdates(bool manual = false)
        {
            IReadOnlyList<Release> releases;

            // Fetching all releases from SasynDev/RICHIFY
            try
            {
                releases = await githubClient.Repository.Release.GetAll("SasynDev", "RICHIFY");
                if (releases == null || releases.Count == 0)
                {
                    if (manual)
                        MessageBox.Show(this, Strings.noUpdatesFound, Strings.information, MessageBoxButtons.OK, MessageBoxIcon.Information, MessageBoxDefaultButton.Button1);
                    return;
                }
                latestRelease = releases[0];
            }
            catch (Exception e)
            {
                // If there's no internet or Github is down, do nothing, unless it's a user requested update check
                if (manual)
                    MessageBox.Show(this, e.Message, Strings.error, MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            string latestStr = latestRelease.TagName;

            if (latestStr == settings.ignoreVersion && !manual)
                return; // The user ignored this version; this gets ignored if the user requested the update check manually, maybe they changed their mind?

            Version current = VersionHelper.GetVersion(Application.ProductVersion);
            Version latest = VersionHelper.GetVersion(latestStr);

            if (current.CompareTo(latest) < 0) // If update is available...
            {
                var changelogBuilder = new System.Text.StringBuilder(); // ...build the changelog...

                foreach (var release in releases)
                {
                    Version releaseVer = VersionHelper.GetVersion(release.TagName);

                    if (releaseVer.Equals(current))
                        break;

                    var releaseBody = release.Body ?? "";
                    if (releaseBody.Contains("Changes:"))
                    {
                        var releaseBodyArr = releaseBody.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
                        if (releaseBodyArr.Length > 4)
                        {
                            releaseBody = "";
                            for (int i = 1; i < releaseBodyArr.Length - 2; i++)
                            {
                                releaseBody += releaseBodyArr[i] + "\r\n";
                            }
                        }
                    }

                    changelogBuilder
                        .Append("<h3>" + (release.Name ?? release.TagName) + "</h3>")
                        .Append(CommonMarkConverter.Convert(releaseBody.Trim()));
                }

                string changelog = changelogBuilder.ToString();

                downloadUpdateToolStripMenuItem.Visible = true; // ...activate the "Download update" button...
                MaximizeFromTray(); // ...make sure the app window is shown if it was minimized...

                var messageBox = new UpdatePrompt(current, latest, changelog).ShowDialog(); // ...and show a dialog box telling there's an update

                if (messageBox == DialogResult.Yes)
                {
                    DownloadAndInstallUpdate();
                    downloadUpdateToolStripMenuItem.Enabled = false;
                }
                else if (messageBox == DialogResult.Ignore)
                {
                    settings.ignoreVersion = latestStr;
                    Analytics.TrackEvent("Ignored an update", new Dictionary<string, string> {
                        { "Version", latestStr }
                    });
                }

                checkUpdatesToolStripMenuItem.Checked = settings.checkUpdates;

                if (!settings.checkUpdates || messageBox == DialogResult.Ignore)
                    downloadUpdateToolStripMenuItem.Visible = false; // If user doesn't want update notifications, let's not bother them
            }
            else if (manual) // If there's no update available and it was a user initiated update check, notify them about it
                MessageBox.Show(this, Strings.noUpdatesFound, Strings.information, MessageBoxButtons.OK, MessageBoxIcon.Information, MessageBoxDefaultButton.Button1);
        }

        /// <summary>
        /// Downloading and installing latest update from GitHub cleanly in-place without batch/cmd scripts.
        /// </summary>
        public async void DownloadAndInstallUpdate()
        {
            if (latestRelease == null)
            {
                Utils.OpenInBrowser("https://github.com/SasynDev/RICHIFY/releases");
                return;
            }

            // Find an exe asset or construct the standard release download url
            string downloadUrl = null;
            if (latestRelease.Assets != null)
            {
                foreach (var a in latestRelease.Assets)
                {
                    if (a.Name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                    {
                        downloadUrl = a.BrowserDownloadUrl;
                        break;
                    }
                }
            }

            if (string.IsNullOrEmpty(downloadUrl))
            {
                downloadUrl = $"https://github.com/SasynDev/RICHIFY/releases/download/{latestRelease.TagName}/RICHIFY.exe";
            }

            var wc = new WebClient();
            string tempExec = Path.Combine(Path.GetTempPath(), $"RICHIFY_update_{Guid.NewGuid():N}.exe");

            wc.DownloadProgressChanged += DownloadProgress;

            downloadUpdateToolStripMenuItem.Enabled = false;
            toolStripStatusLabelStatus.Text = "Downloading update...";

            while (true)
            {
                try
                {
                    if (File.Exists(tempExec))
                    {
                        try { File.Delete(tempExec); } catch { }
                    }

                    await wc.DownloadFileTaskAsync(new Uri(downloadUrl), tempExec);

                    // Validate downloaded binary size
                    var fi = new FileInfo(tempExec);
                    if (!fi.Exists || fi.Length < 100000)
                    {
                        throw new IOException("Downloaded file is incomplete or invalid.");
                    }

                    // Seamless in-place replacement (no batch files, no cmd window popup)
                    string currentExe = Application.ExecutablePath;
                    string oldExe = currentExe + ".old";

                    if (File.Exists(oldExe))
                    {
                        try { File.Delete(oldExe); } catch { }
                    }

                    // Rename current running exe to .old (Windows NTFS allows renaming running files)
                    File.Move(currentExe, oldExe);

                    // Move newly downloaded binary to current exe path
                    File.Move(tempExec, currentExe);

                    // Launch the newly updated executable
                    Process.Start(new ProcessStartInfo()
                    {
                        FileName = currentExe,
                        WorkingDirectory = Path.GetDirectoryName(currentExe),
                        UseShellExecute = true
                    });

                    // Release mutex and exit application smoothly
                    Program.AppMutex?.Close();
                    Environment.Exit(0);
                    break;
                }
                catch (Exception ex)
                {
                    try
                    {
                        if (File.Exists(tempExec))
                            File.Delete(tempExec);
                    }
                    catch
                    {
                    }

                    var result = MessageBox.Show(this, $"{Strings.errorUpdateFailed}\r\n({ex.Message})", Strings.error, MessageBoxButtons.YesNoCancel, MessageBoxIcon.Error, MessageBoxDefaultButton.Button1);

                    if (result == DialogResult.Yes)
                        continue;
                    else if (result == DialogResult.No)
                        Utils.OpenInBrowser(downloadUrl);

                    downloadUpdateToolStripMenuItem.Enabled = true;
                    downloadUpdateToolStripMenuItem.Text = res.GetString("downloadUpdateToolStripMenuItem.Text");
                    toolStripStatusLabelStatus.Text = Strings.statusDisconnected;

                    break;
                }
            }
        }

        /// <summary>
        /// Provides visual feedback for downloading.
        /// </summary>
        private void DownloadProgress(object sender, DownloadProgressChangedEventArgs e)
        {
            downloadUpdateToolStripMenuItem.Text = $"Updating: {e.ProgressPercentage}%";
            toolStripStatusLabelStatus.Text = $"Downloading update: {e.ProgressPercentage}%";
        }

        /// <summary>
        /// Initializes connection to the Discord API.
        /// </summary>
        /// <returns><see langword="False"/> if ID isn't set or something happened when establishing the connection, otherwise <see langword="true"/>.</returns>
        private bool Init()
        {
            string id = settings.id != "" ? settings.id : defaultID;

            if (client != null && !client.IsDisposed)
                client.Dispose(); // This stuff needs proper disposal

            client = new DiscordRpcClient(id, (int)settings.pipe); // Assigning the ID
            client.OnPresenceUpdate += ClientOnPresenceUpdate;
            client.OnError += ClientOnError;
            client.OnConnectionFailed += ClientOnConnFailed;
            client.OnReady += ClientOnReady;

            try
            {
                string logDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "RICHIFY", "logs");
                client.Logger = new TimestampFileLogger(logDir);
            }
            catch { }

            return client.Initialize();
        }

        /// <summary>
        /// Will be called if successfully connected and sent the presence payload.
        /// </summary>
        private void ClientOnPresenceUpdate(object sender, DiscordRPC.Message.PresenceMessage args)
        {
            var presence = client.CurrentPresence;

            ConnectionManager.State = ConnectionState.Connected;

            Invoke(new MethodInvoker(() =>
            {
                textBoxID.BackColor = CurrentColors.BgTextFields;
                toolStripStatusLabelStatus.Text = Strings.statusConnected;
            }));

            // This only tracks whether or not the presence has those parameters set, not their content
            Analytics.TrackEvent("Updated presence", new Dictionary<string, string> {
                { "Party", presence.HasParty().ToString() },
                { "Timestamp", ((TimestampType)settings.timestamps).ToString() },
                { "Big image", (!string.IsNullOrEmpty(presence.Assets?.LargeImageID)).ToString() },
                { "Small image", (!string.IsNullOrEmpty(presence.Assets?.SmallImageID)).ToString() },
                { "Buttons", buttonsList.Count.ToString() }
            });

            restartTimer.Stop();
        }

        /// <summary>
        /// Will be called if failed connecting (due to bad app id or anything else).
        /// </summary>
        private void ClientOnError(object sender, DiscordRPC.Message.ErrorMessage args)
        {
            ConnectionManager.State = ConnectionState.Error;

            Invoke(new MethodInvoker(() =>
            {
                if (buttonConnect.Enabled) // Ignore if the user disconnected before connection was established
                    return;

                textBoxID.BackColor = CurrentColors.BgTextFields;
                toolStripStatusLabelStatus.Text = Strings.statusError;
                if (args.Code != DiscordRPC.Message.ErrorCode.UnknownError)
                    MessageBox.Show(this, args.Message, Strings.error, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }));

            if (ConnectionManager.HasChanged()) // Ignore repeated calls caused by auto reconnect
                Analytics.TrackEvent("Connection error");

            restartTimer.Start();
        }

        /// <summary>
        /// Will be called if failed connecting (usually due to Discord being closed).
        /// </summary>
        private void ClientOnConnFailed(object sender, DiscordRPC.Message.ConnectionFailedMessage args)
        {
            Invoke(new MethodInvoker(() =>
            {
                if (buttonConnect.Enabled) // Ignore if the user disconnected before connection was established
                    return;

                textBoxID.BackColor = CurrentColors.BgTextFields;
                toolStripStatusLabelStatus.Text = Strings.statusConnectionFailed;
            }));

            ConnectionManager.State = ConnectionState.Error;

            if (ConnectionManager.HasChanged()) // Ignore repeated calls caused by auto reconnect
                Analytics.TrackEvent("Connection failed");

            restartTimer.Start();
        }

        /// <summary>
        /// Will be called as soon as CustomRP connects to Discord.
        /// </summary>
        private void ClientOnReady(object sender, DiscordRPC.Message.ReadyMessage args)
        {
            Invoke(new MethodInvoker(() =>
            {
                toolStripStatusLabelUsername.Image = Properties.Resources.logo;
                toolStripStatusLabelUsername.TextImageRelation = TextImageRelation.ImageBeforeText;
                toolStripStatusLabelUsername.ImageAlign = ContentAlignment.MiddleLeft;
                toolStripStatusLabelUsername.Text = " " + client.CurrentUser.Username;
                trayIcon.Text = $"{res.GetString("trayIcon.Text")}{(Program.IsSecondInstance ? " (2)" : "")}\n{client.CurrentUser.Username}";

                buttonUpdatePresence.Enabled = true;

                ConnectionManager.State = ConnectionState.UpdatingPresence;
                toolStripStatusLabelStatus.Text = Strings.statusUpdatingPresence;

                SetPresence();
            }));
        }

        /// <summary>
        /// Sets up new presence from the settings.
        /// </summary>
        /// <returns><see langword="True"/> if presence was successfully set, <see langword="false"/> otherwise.</returns>
        private bool SetPresence()
        {
            if (client == null || client.IsDisposed)
                return false;

            localTimeTimer.Stop();

            // Add ZWS character if details or state textboxes start a no-break space character
            foreach (var paramBox in new[] { textBoxDetails, textBoxState })
            {
                if (paramBox.Text.StartsWith(U00A0) && paramBox.Text.Length < paramBox.MaxLength)
                    paramBox.Text = paramBox.Text.Insert(0, U200B);
                // In case it doesn't fit but there's at least 2 space symbols, we can replace one of them with the zws
                else if (paramBox.Text.StartsWith(U00A0 + U00A0))
                    paramBox.Text = U200B + paramBox.Text.Substring(1);
                // In case it still doesn't fit, why would you even use spaces then, 128 characters is a long string
            }

            if (settings.partySize > settings.partyMax)
                settings.partyMax = settings.partySize;

            settings.detailsURL = settings.detailsURL.Trim();
            settings.stateURL = settings.stateURL.Trim();
            settings.largeKey = settings.largeKey.Trim();
            settings.largeURL = settings.largeURL.Trim();
            settings.smallKey = settings.smallKey.Trim();
            settings.smallURL = settings.smallURL.Trim();
            settings.button1URL = settings.button1URL.Trim();
            settings.button2URL = settings.button2URL.Trim();

            // Check if user has entered ANY custom field at all
            bool hasCustomInput = !string.IsNullOrWhiteSpace(settings.name)
                || !string.IsNullOrWhiteSpace(settings.details)
                || !string.IsNullOrWhiteSpace(settings.state)
                || !string.IsNullOrWhiteSpace(settings.largeKey)
                || !string.IsNullOrWhiteSpace(settings.smallKey)
                || !string.IsNullOrWhiteSpace(settings.button1Text)
                || !string.IsNullOrWhiteSpace(settings.button2Text)
                || settings.partySize > 0;

            var rp = new RichPresence()
            {
                Name = !string.IsNullOrWhiteSpace(settings.name) ? settings.name : (hasCustomInput ? "" : "RICHIFY"),
                Type = (ActivityType)settings.type,
                StatusDisplay = (StatusDisplayType)settings.display,
                Details = !string.IsNullOrWhiteSpace(settings.details) ? settings.details : (hasCustomInput ? "" : "RICHIFY"),
                State = !string.IsNullOrWhiteSpace(settings.state) ? settings.state : (hasCustomInput ? "" : "Build by SasynDev"),
                Party = new Party()
                {
                    ID = (settings.partySize > 0 && settings.partyMax > 0) ? "RICHIFY" : "",
                    Size = (int)settings.partySize,
                    Max = (int)settings.partyMax
                },
            };

            Uri tempUri;

            /* Unused
            string GetProcessedURL(Uri origUri)
            {
                if (!origUri.Host.Contains("discordapp"))
                    return origUri.AbsoluteUri;

                var newUri = new UriBuilder(origUri.AbsoluteUri);
                var newQuery = origUri.ParseQueryString();

                newQuery.Remove("ex");
                newQuery.Remove("is");
                newQuery.Remove("hm");
                newUri.Query = newQuery.ToString();

                return newUri.Uri.AbsoluteUri;
            }
            */

            string ProcessURL(string url, int maxLength)
            {
                if (string.IsNullOrEmpty(url))
                    return url;

                if (!url.Contains("://"))
                    url = "https://" + url;

                try
                {
                    if (Uri.TryCreate(url, UriKind.Absolute, out tempUri))
                        url = tempUri.AbsoluteUri.Replace(tempUri.Host, tempUri.IdnHost);
                }
                catch
                {
                    // Sometimes TryCreate throws errors, even when it's not supposed to, so we can just let it fail quietly
                }

                return url.Substring(0, Math.Min(maxLength, url.Length));
            }

            settings.detailsURL = ProcessURL(settings.detailsURL, textBoxDetailsURL.MaxLength);
            settings.stateURL = ProcessURL(settings.stateURL, textBoxStateURL.MaxLength);

            try
            {
                rp.DetailsUrl = settings.detailsURL;
                rp.StateUrl = settings.stateURL;
            }
            catch (Exception e)
            {
                MessageBox.Show(e.Message, Strings.error, MessageBoxButtons.OK, MessageBoxIcon.Error, MessageBoxDefaultButton.Button1);

                return false;
            }

            string Proxify(string key)
            {
                return key;
            }

            try
            {
                // Thank you Discord, very cool
                bool IsMpExternalStringOverLimit(Uri uri)
                {
                    return $"mp:external/43 characters that probably represent an id/{Uri.EscapeDataString(uri.Query)}/{uri.Scheme}/{(uri.IdnHost == "media.discordapp.net" ? "cdn.discordapp.com" : uri.IdnHost)}{uri.AbsolutePath}".Length > 256;
                }

                if (Uri.TryCreate(settings.smallKey, UriKind.Absolute, out tempUri))
                {
                    if (IsMpExternalStringOverLimit(tempUri))
                        throw new ArgumentException("Small");

                    settings.smallKey = tempUri.AbsoluteUri.Replace(tempUri.Host, tempUri.IdnHost);
                }

                string keyToUse = !string.IsNullOrWhiteSpace(settings.largeKey)
                    ? settings.largeKey
                    : (hasCustomInput ? null : "https://raw.githubusercontent.com/SasynDev/RICHIFY/main/CustomRPC/Resources/logo.png");

                if (!string.IsNullOrEmpty(keyToUse) && Uri.TryCreate(keyToUse, UriKind.Absolute, out tempUri))
                {
                    if (IsMpExternalStringOverLimit(tempUri))
                        throw new ArgumentException("Large");

                    keyToUse = tempUri.AbsoluteUri.Replace(tempUri.Host, tempUri.IdnHost);
                }

                settings.largeURL = ProcessURL(settings.largeURL, textBoxLargeURL.MaxLength);
                settings.smallURL = ProcessURL(settings.smallURL, textBoxSmallURL.MaxLength);

                if (!string.IsNullOrEmpty(keyToUse) || !string.IsNullOrEmpty(settings.smallKey))
                {
                    rp.Assets = new Assets()
                    {
                        LargeImageKey = keyToUse,
                        LargeImageText = !string.IsNullOrWhiteSpace(settings.largeText) ? settings.largeText : (hasCustomInput ? "" : "RICHIFY"),
                        LargeImageUrl = settings.largeURL,
                        SmallImageKey = Proxify(settings.smallKey),
                        SmallImageText = !string.IsNullOrWhiteSpace(settings.smallText) ? settings.smallText : (hasCustomInput ? "" : "Build by SasynDev"),
                        SmallImageUrl = settings.smallURL,
                    };
                }
                else
                {
                    rp.Assets = null;
                }
            }
            catch (Exception e)
            {
                if (e is ArgumentException)
                    MessageBox.Show(Strings.errorInvalidImageURL + " (" + res.GetString("label" + e.Message + ".Text") + ")", Strings.error, MessageBoxButtons.OK, MessageBoxIcon.Error, MessageBoxDefaultButton.Button1);
                else
                    MessageBox.Show(e.Message, Strings.error, MessageBoxButtons.OK, MessageBoxIcon.Error, MessageBoxDefaultButton.Button1);

                return false;
            }
            finally
            {
                Utils.SaveSettings();
            }

            buttonsList.Clear();

            settings.button1URL = ProcessURL(settings.button1URL, textBoxButton1URL.MaxLength);
            settings.button2URL = ProcessURL(settings.button2URL, textBoxButton2URL.MaxLength);

            Utils.SaveSettings();

            // This try block technically isn't necessary, except if you screw up URL field processing (ask me how I know)
            try
            {
                if (!string.IsNullOrWhiteSpace(settings.button1Text) && !string.IsNullOrWhiteSpace(settings.button1URL))
                    buttonsList.Add(new DButton()
                    {
                        Label = settings.button1Text,
                        Url = settings.button1URL
                    });

                if (!string.IsNullOrWhiteSpace(settings.button2Text) && !string.IsNullOrWhiteSpace(settings.button2URL))
                    buttonsList.Add(new DButton()
                    {
                        Label = settings.button2Text,
                        Url = settings.button2URL
                    });

                // If user entered NO custom inputs at all, send default SasynDev & Discord buttons
                if (!hasCustomInput && buttonsList.Count == 0)
                {
                    buttonsList.Add(new DButton()
                    {
                        Label = "SasynDev",
                        Url = "https://github.com/SasynDev/RICHIFY"
                    });
                    buttonsList.Add(new DButton()
                    {
                        Label = "Discord",
                        Url = "https://discord.gg/urD8Dkbewj"
                    });
                }
            }
            catch
            {
                MessageBox.Show(Strings.errorInvalidURL, Strings.error, MessageBoxButtons.OK, MessageBoxIcon.Error, MessageBoxDefaultButton.Button1);
                return false;
            }

            rp.Buttons = buttonsList.Count > 0 ? buttonsList.ToArray() : null;

            switch ((TimestampType)settings.timestamps)
            {
                case TimestampType.SinceLastConnection: rp.Timestamps = new Timestamps(timestampConnected); break;
                case TimestampType.SinceStartup: rp.Timestamps = new Timestamps(timestampStarted); break;
                case TimestampType.SincePresenceUpdate: rp.Timestamps = Timestamps.Now; break;
                case TimestampType.LocalTime:
                    rp.Timestamps = new Timestamps(DateTime.UtcNow.Subtract(new TimeSpan(DateTime.Now.Hour, DateTime.Now.Minute, DateTime.Now.Second)));
                    localTimeTimer.Interval = DateTime.Today.AddDays(1).AddSeconds(5).Subtract(DateTime.Now).TotalMilliseconds;
                    break;
                case TimestampType.Custom:
                    DateTime customTimestampStart = dateTimePickerTimestampStart.Value.ToUniversalTime();
                    DateTime customTimestampEnd = dateTimePickerTimestampEnd.Value.ToUniversalTime();
                    /* Not needed anymore
                    /// I must apologize preemptively for this monster of an if-statement
                    /// Timestamps before 2001-09-09 01:46:40 UTC only work if you have a "dumb" presence (the one that only has ID,
                    /// timestamp and small image fields set)
                    /// Technically, it doesn't even matter what date you put in the rich presence timestamp, since it only shows the hours
                    /// since/to the timestamp
                    if (customTimestampStart.CompareTo(new DateTime(2001, 9, 9, 1, 46, 40, DateTimeKind.Utc)) < 0 &&
                        !(string.IsNullOrEmpty(settings.details) && string.IsNullOrEmpty(settings.state) && settings.partySize == 0 &&
                        settings.partyMax == 0 && string.IsNullOrEmpty(settings.largeKey) && string.IsNullOrEmpty(settings.largeText) &&
                        string.IsNullOrEmpty(settings.smallText) && string.IsNullOrEmpty(settings.button1Text) &&
                        string.IsNullOrEmpty(settings.button1URL) && string.IsNullOrEmpty(settings.button2Text) &&
                        string.IsNullOrEmpty(settings.button2URL)))
                    {
                        customTimestampStart = new DateTime(2002, 1, 1, customTimestampStart.Hour, customTimestampStart.Minute, customTimestampStart.Second, DateTimeKind.Utc);
                    */
                    if (checkBoxTimestampEnd.Checked)
                        rp.Timestamps = new Timestamps(customTimestampStart, customTimestampEnd);
                    else
                        rp.Timestamps = customTimestampStart.CompareTo(DateTime.UtcNow) < 0
                            ? new Timestamps(customTimestampStart) : new Timestamps(DateTime.UtcNow, customTimestampStart);
                    break;
            }

            try
            {
                client.SetPresence(rp);
                ConnectionManager.State = ConnectionState.UpdatingPresence;
                toolStripStatusLabelStatus.Text = Strings.statusUpdatingPresence;
            }
            catch (Exception e)
            {
                MessageBox.Show(e.Message, Strings.error, MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }

            if ((TimestampType)settings.timestamps == TimestampType.LocalTime)
                localTimeTimer.Start();

            return true;
        }

        /// <summary>
        /// Sets up the startup link for the app.
        /// </summary>
        private void StartupSetup()
        {
            try
            {
                if (settings.runOnStartup && !File.Exists(linkPath)) // If run on startup is enabled and the link isn't in the Startup folder
                {
                    IWshRuntimeLibrary.WshShell wsh = new IWshRuntimeLibrary.WshShell();
                    IWshRuntimeLibrary.IWshShortcut shortcut = wsh.CreateShortcut(linkPath) as IWshRuntimeLibrary.IWshShortcut;
                    shortcut.Description = "Discord Custom Rich Presence Manager";
                    shortcut.TargetPath = Application.ExecutablePath;
                    shortcut.WorkingDirectory = Environment.CurrentDirectory + @"\";
                    if (Program.IsSecondInstance)
                        shortcut.Arguments = "--second-instance";
                    shortcut.Save();
                }
                else if (!settings.runOnStartup && File.Exists(linkPath)) // If run on startup is disabled and the link is in the Startup folder
                    File.Delete(linkPath);
            }
            catch (Exception e)
            {
                // I *think* this would only happen if an antivirus would intervene saving/deleting a file in a user folder,
                // therefore I'm just allowing the user to quickly try changing the option again
                runOnStartupToolStripMenuItem.Checked = !settings.runOnStartup;
                MessageBox.Show($"{Strings.errorStartupShortcut} {e.Message}", Strings.error, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        /// <summary>
        /// Called when you drag a file into app's window.
        /// </summary>
        private void DragDropEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
                e.Effect = DragDropEffects.All;
            else
                e.Effect = DragDropEffects.None;
        }

        /// <summary>
        /// Called upon dropping a file.
        /// </summary>
        private void DragDropHandler(object sender, DragEventArgs e)
        {
            if (e.Data.GetData(DataFormats.FileDrop) is string[] files && files.Length > 0)
                LoadPreset(files[0]); // If multiple files are passed, only the first one gets imported
        }

        /// <summary>
        /// Called when you close the main window with the X button.
        /// </summary>
        private void MinimizeToTray(object sender, FormClosingEventArgs e)
        {
            if (e.CloseReason == CloseReason.UserClosing) // Checks if it was closed by user and not by system in a shutdown, for example
            {
                // Smoothly fade out to tray
                e.Cancel = true;
                SmoothHide(false);
            }
        }

        /// <summary>
        /// Called when you click the tray icon, balloon tip, or the "Open" option in the tray menu.
        /// </summary>
        private void MaximizeFromTray(object sender, EventArgs e)
        {
            if (e is MouseEventArgs ev && ev.Button != MouseButtons.Left)
                return;

            MaximizeFromTray();
        }

        /// <summary>
        /// Maximizes the window.
        /// </summary>
        private void MaximizeFromTray()
        {
            switch (ConnectionManager.State) // Because invoking doesn't work while the form is hidden
            {
                case ConnectionState.Disconnected:
                    textBoxID.BackColor = CurrentColors.BgTextFields;
                    toolStripStatusLabelStatus.Text = Strings.statusDisconnected;
                    break;
                case ConnectionState.Connecting:
                    textBoxID.BackColor = CurrentColors.BgTextFields;
                    toolStripStatusLabelStatus.Text = Strings.statusConnecting;
                    break;
                case ConnectionState.UpdatingPresence:
                    textBoxID.BackColor = CurrentColors.BgTextFields;
                    toolStripStatusLabelStatus.Text = Strings.statusUpdatingPresence;
                    break;
                case ConnectionState.Connected:
                    textBoxID.BackColor = CurrentColors.BgTextFields;
                    toolStripStatusLabelStatus.Text = Strings.statusConnected;
                    break;
                case ConnectionState.Error:
                    textBoxID.BackColor = CurrentColors.BgTextFields;
                    toolStripStatusLabelStatus.Text = Strings.statusError;
                    break;
            }

            if (WindowState == FormWindowState.Minimized)
                WindowState = FormWindowState.Normal;

            SmoothShow();
        }

        /// <summary>
        /// Called when you press New Preset button.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void NewPreset(object sender, EventArgs e)
        {
            if (MessageBox.Show(this, Strings.newPresetConfirmation, Application.ProductName, MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.No)
                return;

            if (ConnectionManager.State != ConnectionState.Disconnected)
                Disconnect();

            textBoxID.Text = textBoxName.Text =
                textBoxDetails.Text = textBoxDetailsURL.Text =
                textBoxState.Text = textBoxStateURL.Text =
                comboBoxLargeKey.Text = comboBoxSmallKey.Text =
                textBoxLargeText.Text = textBoxSmallText.Text =
                textBoxLargeURL.Text = textBoxSmallURL.Text =
                textBoxButton1Text.Text = textBoxButton1URL.Text =
                textBoxButton2Text.Text = textBoxButton2URL.Text = "";
            comboBoxType.SelectedValue = ActivityType.Playing;
            comboBoxDisplay.SelectedValue = StatusDisplayType.Name;
            numericUpDownPartySize.Value = numericUpDownPartyMax.Value = 0;
            radioButtonLastConnection.Checked = true;
            checkBoxTimestampEnd.Checked = false;
        }

        /// <summary>
        /// Base function for loading a preset.
        /// </summary>
        /// <param name="file">A file stream of the preset file.</param>
        private void LoadPreset(Stream file)
        {
            try
            {
                var xs = new XmlSerializer(typeof(Preset)); // Using XML here because... why not? Settings are already saved in XML
                var preset = (Preset)xs.Deserialize(file);

                bool wasConnected = buttonDisconnect.Enabled;
                bool isNewID = settings.id != preset.ID;

                settings.id = preset.ID ?? "";
                settings.type = preset.Type;
                settings.display = preset.Display;
                settings.name = preset.Name ?? "";
                settings.details = preset.Details ?? "";
                settings.detailsURL = preset.DetailsURL ?? "";
                settings.state = preset.State ?? "";
                settings.stateURL = preset.StateURL ?? "";
                settings.partySize = preset.PartySize;
                settings.partyMax = preset.PartyMax;
                settings.timestamps = preset.Timestamps;
                settings.customTimestamp = preset.CustomTimestamp;
                settings.customTimestampEndEnabled = preset.CustomTimestampEndEnabled;
                settings.customTimestampEnd = preset.CustomTimestampEnd;
                settings.largeKey = preset.LargeKey ?? "";
                settings.largeText = preset.LargeText ?? "";
                settings.largeURL = preset.LargeURL ?? "";
                settings.smallKey = preset.SmallKey ?? "";
                settings.smallText = preset.SmallText ?? "";
                settings.smallURL = preset.SmallURL ?? "";
                settings.button1Text = preset.Button1Text ?? "";
                settings.button1URL = preset.Button1URL ?? "";
                settings.button2Text = preset.Button2Text ?? "";
                settings.button2URL = preset.Button2URL ?? "";
                Utils.SaveSettings();

                comboBoxType.SelectedValue = (ActivityType)settings.type;
                comboBoxDisplay.SelectedValue = (StatusDisplayType)settings.display;

                switch ((TimestampType)settings.timestamps)
                {
                    case TimestampType.SinceLastConnection: radioButtonLastConnection.Checked = true; break;
                    case TimestampType.SincePresenceUpdate: radioButtonPresence.Checked = true; break;
                    case TimestampType.SinceStartup: radioButtonStartTime.Checked = true; break;
                    case TimestampType.LocalTime: radioButtonLocalTime.Checked = true; break;
                    case TimestampType.Custom: radioButtonCustom.Checked = true; break;
                }

                Analytics.TrackEvent("Loaded a preset");

                if (!wasConnected)
                    return;

                if (isNewID || ConnectionManager.State != ConnectionState.Connected)
                    Reconnect();
                else
                    SetPresence();
            }
            catch
            {
                MessageBox.Show(Strings.errorInvalidPresetFile, Strings.error, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                file.Close();
            }
        }

        /// <summary>
        /// Called when you press Load Preset button.
        /// </summary>
        private void LoadPreset(object sender, EventArgs e)
        {
            var presetFile = new OpenFileDialog()
            {
                Filter = "RICHIFY Preset|*.crp"
            };

            if (presetFile.ShowDialog() != DialogResult.OK)
                return;

            try
            {
                LoadPreset(presetFile.OpenFile());
            }
            catch
            {
                MessageBox.Show(Strings.errorInvalidPresetFile, Strings.error, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        /// <summary>
        /// Loads preset from a file.
        /// </summary>
        /// <param name="filePath">The path to the preset file.</param>
        private void LoadPreset(string filePath)
        {
            try
            {
                LoadPreset(File.OpenRead(filePath));
            }
            catch
            {
                MessageBox.Show(Strings.errorInvalidPresetFile, Strings.error, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        /// <summary>
        /// Called when you press Save Preset button.
        /// </summary>
        private void SavePreset(object sender, EventArgs e)
        {
            var xs = new XmlSerializer(typeof(Preset));
            var presetFile = new SaveFileDialog()
            {
                Filter = "RICHIFY Preset|*.crp"
            };

            if (presetFile.ShowDialog() != DialogResult.OK || presetFile.FileNames.Length == 0)
                return;

            while (true)
            {
                try
                {
                    using (var file = presetFile.OpenFile())
                    {
                        xs.Serialize(file, new Preset()
                        {
                            ID = settings.id,
                            Type = settings.type,
                            Display = settings.display,
                            Name = settings.name,
                            Details = settings.details,
                            DetailsURL = settings.detailsURL,
                            State = settings.state,
                            StateURL = settings.stateURL,
                            PartySize = (int)settings.partySize,
                            PartyMax = (int)settings.partyMax,
                            Timestamps = settings.timestamps,
                            CustomTimestamp = settings.customTimestamp,
                            CustomTimestampEndEnabled = settings.customTimestampEndEnabled,
                            CustomTimestampEnd = settings.customTimestampEnd,
                            LargeKey = settings.largeKey,
                            LargeText = settings.largeText,
                            LargeURL = settings.largeURL,
                            SmallKey = settings.smallKey,
                            SmallText = settings.smallText,
                            SmallURL = settings.smallURL,
                            Button1Text = settings.button1Text,
                            Button1URL = settings.button1URL,
                            Button2Text = settings.button2Text,
                            Button2URL = settings.button2URL,
                        });
                    }

                    Analytics.TrackEvent("Saved a preset");

                    return;
                }
                catch (Exception ex)
                {
                    if (MessageBox.Show(ex.Message, Strings.error, MessageBoxButtons.RetryCancel, MessageBoxIcon.Error) == DialogResult.Cancel)
                        return;
                }
            }
        }

        /// <summary>
        /// Called when you press Upload Assets button.
        /// </summary>
        private void OpenDiscordSite(object sender, EventArgs e)
        {
            if (settings.id == "")
            {
                MessageBox.Show(Strings.errorNoID, Strings.error, MessageBoxButtons.OK, MessageBoxIcon.Error, MessageBoxDefaultButton.Button1);
                return;
            }

            Utils.OpenInBrowser("https://discord.com/developers/applications/" + settings.id + "/rich-presence/assets");
        }

        /// <summary>
        /// Called when you click File -> Quit or right-click on the tray icon and choose Quit.
        /// </summary>
        private void Quit(object sender, EventArgs e)
        {
            if (client != null)
                client.Dispose();

            Utils.SaveSettings();
            SmoothHide(true);
        }

        /// <summary>
        /// Called when you press anything in Settings submenu of menu strip.
        /// </summary>
        /// <exception cref="NotImplementedException">In case I forgot something.</exception>
        private void SaveMenuSettings(object sender, EventArgs e)
        {
            if (loading)
                return;

            var setting = (ToolStripMenuItem)sender;
            var properName = setting.Name.Replace("ToolStripMenuItem", "");

            switch (properName)
            {
                case "runOnStartup":
                    // Apparently property binding doesn't work either for checkboxes or for bool variables
                    settings.runOnStartup = setting.Checked;
                    StartupSetup();
                    break;
                case "startMinimized":
                    settings.startMinimized = setting.Checked;
                    break;
                case "autoconnect":
                    settings.autoconnect = setting.Checked;
                    break;
                case "checkUpdates":
                    settings.checkUpdates = setting.Checked;
                    if (setting.Checked)
                        CheckForUpdates();
                    break;
                case "allowAnalytics":
                    settings.analytics = setting.Checked;
                    Analytics.SetEnabledAsync(setting.Checked);
                    break;
                default:
                    throw new NotImplementedException(properName);
            }

            Utils.SaveSettings();
        }

        /// <summary>
        /// Called when you change the language.
        /// </summary>
        private void ChangeLanguage(object sender, EventArgs e)
        {
            var lang = (ToolStripMenuItem)sender;

            settings.language = (string)lang.Tag;
            settings.changedLanguage = true;
            settings.wasConnected = buttonDisconnect.Enabled;
            Utils.SaveSettings();
            Program.AppMutex.Close();
            Application.Restart();
        }

        /// <summary>
        /// Called when you press on menu items that open websites.
        /// </summary>
        private void OpenSite(object sender, EventArgs e)
        {
            var item = (ToolStripMenuItem)sender;
            var url = (string)item.Tag;

            if (string.IsNullOrEmpty(url))
                return;

            Utils.OpenInBrowser(url);
        }

        /// <summary>
        /// Called when you press on a translator's or supporter's nickname.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OpenPersonsPage(object sender, EventArgs e)
        {
            var personItem = (ToolStripMenuItem)sender;

            if (personItem.Tag == null)
                return;

            var (personType, personUrl) = (ValueTuple<string, string>)personItem.Tag;

            if (string.IsNullOrWhiteSpace(personUrl))
                return;

            string personName = personItem.Text;

            if (personType == "supporter")
                personName = personName.Replace(" - ", "|").Split('|')[0]; // Doing this replacement thing just in case someone will have "-" in their nickname

            Analytics.TrackEvent("Clicked on a " + personType, new Dictionary<string, string> {
                { "Name", personName },
                { "URL", personUrl }
            });

            Utils.OpenInBrowser(personUrl);
        }

        /// <summary>
        /// Called when you press Check for updates... button.
        /// </summary>
        private void CheckForUpdates(object sender, EventArgs e) => CheckForUpdates(true);

        /// <summary>
        /// Called when you press About... button.
        /// </summary>
        private void ShowAbout(object sender, EventArgs e)
        {
            Analytics.TrackEvent("Opened about window");
            new About().ShowDialog(this);
        }

        /// <summary>
        /// Called when you press Download Update button.
        /// </summary>
        private void DownloadUpdate(object sender, EventArgs e)
        {
            downloadUpdateToolStripMenuItem.Enabled = false;
            DownloadAndInstallUpdate();
        }

        /// <summary>
        /// Called when you input into the ID textbox.
        /// </summary>
        /// <remarks>
        /// This is overcomplicated isn't it, but hey, at least it works with pasting as well!
        /// At least it used to. Huh.
        /// </remarks>
        private void OnlyNumbers(object sender, EventArgs e)
        {
            if (toAvoidRecursion || textBoxID.Text == "")
                return;

            textBoxID.ReadOnly = true; // Not sure if I need it?
            toAvoidRecursion = true; // Just so this handler doesn't get called again on...

            int sel = textBoxID.SelectionStart; // Current cursor position
            int changed = 0;

            string newline = "";

            foreach (var symbol in textBoxID.Text)
            {
                if (char.IsDigit(symbol))
                    newline += symbol;
                else
                    changed++;
            }

            if (changed > 0)
            {
                textBoxID.Text = newline; // ...this line
                textBoxID.SelectionStart = sel - changed;
                textBoxID.SelectionLength = 0;
            }

            textBoxID.ReadOnly = false;
            toAvoidRecursion = false;
        }

        /// <summary>
        /// Called when a presence type is changed in comboBoxType.
        /// </summary>
        private void PresenceTypeChanged(object sender, EventArgs e)
        {
            if (comboBoxType.Items.Count == 0 || comboBoxType.SelectedItem == null)
                return;

            ActivityType type = (ActivityType)comboBoxType.SelectedValue;

            if (!loading)
            {
                settings.type = (int)type;
                Utils.SaveSettings();
            }

            bool canHaveParty = true, canHaveTimestamps = true;

            if (type != ActivityType.Playing)
                canHaveParty = false;

            if (type == ActivityType.Competing)
                canHaveTimestamps = false;

            flowLayoutPanelParty.Enabled = canHaveParty;
            panelTimestamps.Enabled = canHaveTimestamps;
        }

        /// <summary>
        /// Called when a display type is changed in comboBoxDisplay.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void DisplayTypeChanged(object sender, EventArgs e)
        {
            if (comboBoxDisplay.Items.Count == 0 || comboBoxDisplay.SelectedItem == null)
                return;

            StatusDisplayType display = (StatusDisplayType)comboBoxDisplay.SelectedValue;

            if (!loading)
            {
                settings.display = (int)display;
                Utils.SaveSettings();
            }
        }

        /// <summary>
        /// Called on Leave event for all text fields except ID.
        /// </summary>
        private void TrimTextBoxes(object sender, EventArgs e)
        {
            Control box = (Control)sender;
            if (box.Text.StartsWith(U00A0))
                return;
            box.Text = box.Text.Trim();
        }

        /// <summary>
        /// Called on Validating event for all text fields except ID.
        /// </summary>
        private void LengthValidationFocus(object sender, System.ComponentModel.CancelEventArgs e)
        {
            dynamic box = sender;

            bool useBytes = box.Name.EndsWith("Button1Text") || box.Name.EndsWith("Button2Text");

            if (box.Text.Length == 1 || useBytes && !StringTools.WithinLength(box.Text, box.MaxLength))
            {
                e.Cancel = true;
                System.Media.SystemSounds.Beep.Play();
            }
        }

        /// <summary>
        /// Called on TextChanged event for all text fields except ID.
        /// </summary>
        private void LengthValidation(object sender, EventArgs e)
        {
            dynamic box = sender;

            bool useBytes = box.Name.EndsWith("Button1Text") || box.Name.EndsWith("Button2Text");

            box.BackColor = (box.Text.Length == 1 || useBytes && !StringTools.WithinLength(box.Text, box.MaxLength)) ? CurrentColors.BgTextFieldsError : CurrentColors.BgTextFields;
        }

        /// <summary>
        /// Shows the full content of most editable fields if the content is too big for the field.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ShowFieldToolTip(object sender, EventArgs e)
        {
            if (!(sender is Control control) || string.IsNullOrEmpty(control.Text))
                return;

            // Measure the exact text width based on the control's font
            Size textSize = TextRenderer.MeasureText(control.Text, control.Font, control.ClientSize);
            int visibleWidth = control.ClientSize.Width;

            if (control is ComboBox comboBox)
                // Subtract the width of the dropdown arrow button
                visibleWidth -= SystemInformation.HorizontalScrollBarThumbWidth;
            else if (control is TextBox)
                // Apparently textSize is 5 pixels too long than the actual measurement, but when extracting the length
                // of the ComboBox's arrow, it accounts for that, but for TextBox you need to manually do that 
                visibleWidth += 5;

            // Toggle tooltip based on text clipping
            if (textSize.Width > visibleWidth)
                toolTipFieldContent.SetToolTip(control, control.Text);
            else
                toolTipFieldContent.SetToolTip(control, string.Empty);
        }

        /// <summary>
        /// Called on Validating event to validate party size values.
        /// </summary>
        private void PartySizeValidation(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (sender == numericUpDownPartyMax && numericUpDownPartyMax.Value == 0)
                numericUpDownPartySize.Value = 0;
            else if (numericUpDownPartySize.Value > numericUpDownPartyMax.Value)
            {
                numericUpDownPartyMax.Value = numericUpDownPartySize.Value;
                // If user sets max value less than current value, play error sound, but not if user sets current value more than max
                if (sender == numericUpDownPartyMax) System.Media.SystemSounds.Beep.Play();
            }
        }

        /// <summary>
        /// Called when a timestamp radiobutton changed.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void TimestampsChanged(object sender, EventArgs e)
        {
            if (loading)
                return;

            RadioButton btn = (RadioButton)sender;

            if (!btn.Checked)
                return;

            // settings.timestamps = btn.TabIndex; // I mean... it's a great container for int values
            // It was, until I needed to add a new type in the middle of the list
            settings.timestamps = (int)btn.Tag;
            Utils.SaveSettings();

            tableLayoutPanelCustomTimestamps.Enabled = (TimestampType)settings.timestamps == TimestampType.Custom;
            dateTimePickerTimestampEnd.Enabled = checkBoxTimestampEnd.Checked;
        }

        /// <summary>
        /// Called when End timestamp checkbox changed.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void TimestampEndChanged(object sender, EventArgs e)
        {
            dateTimePickerTimestampEnd.Enabled = checkBoxTimestampEnd.Checked;
        }

        /// <summary>
        /// Called on DropDown event for <see cref="comboBoxLargeKey"/> and <see cref="comboBoxSmallKey"/>.
        /// </summary>
        private void FetchAssets(object sender, EventArgs e)
        {
            if (settings.id == "" || (lastIDChecked == settings.id && nextAssetCheck.CompareTo(DateTime.Now) > 0))
                return;

            lastIDChecked = settings.id;

            comboBoxLargeKey.Items.Clear();
            comboBoxSmallKey.Items.Clear();

            using (var client = new HttpClient())
            {
                client.Timeout = new TimeSpan(0, 0, 15);

                try
                {
                    ServicePointManager.Expect100Continue = true;
                    ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;

                    var res = client.GetAsync($"https://discord.com/api/oauth2/applications/{settings.id}/assets").Result;

                    if (res.IsSuccessStatusCode)
                    {
                        var resList = res.Content.ReadAsAsync<List<ImageAssets>>().Result;

                        resList.ForEach(asset =>
                        {
                            comboBoxLargeKey.Items.Add(asset.Name);
                            comboBoxSmallKey.Items.Add(asset.Name);
                        });

                        nextAssetCheck = DateTime.Now.Add(new TimeSpan(0, 1, 0));
                    }
                }
                catch
                {
                    MessageBox.Show(Strings.errorNoInternet, Strings.error, MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        /// <summary>
        /// Called on Paint event for some controls that can be disabled.
        /// </summary>
        private void DisabedTextPaint(object sender, PaintEventArgs e)
        {
            Control ctrl = (Control)sender;
            if (ctrl.Enabled)
                return;

            var newClipRect = new Rectangle(e.ClipRectangle.Location, e.ClipRectangle.Size);
            if (sender is CheckBox)
                newClipRect.Location = new Point(7, -1);
            if (sender is RadioButton)
                newClipRect.Location = new Point(7, 0);

            TextFormatFlags flags = TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter;
            TextRenderer.DrawText(e.Graphics, ctrl.Text, ctrl.Font, newClipRect, CurrentColors.TextInactive, flags);
        }

        /// <summary>
        /// Called when you press the Connect button or right-click on the tray icon and choose Reconnect.
        /// </summary>
        private void Connect(object sender, EventArgs e)
        {
#if DEBUG
            if (ModifierKeys == (Keys.Control | Keys.Alt) && sender is Button)
                Crashes.GenerateTestCrash();
#endif
            if (ModifierKeys == (Keys.Control | Keys.Shift) && sender is Button)
            {
                Analytics.TrackEvent("Opened pipe selector window");

                new PipeSelector().ShowDialog(this);
                return;
            }

            Connect();
        }

        /// <summary>
        /// Connects to Discord and makes UI changes.
        /// </summary>
        private void Connect()
        {
            Utils.SaveSettings();

            if (Init()) // If successfully initialized...
            {
                if (ConnectionManager.State == ConnectionState.Disconnected)
                    Analytics.TrackEvent("Connected"); // Only send analytics if connect function was called from disconnected state

                ConnectionManager.State = ConnectionState.Connecting;

                timestampConnected = DateTime.UtcNow;

                buttonConnect.Enabled = false; // ...disable Connect button...
                buttonDisconnect.Enabled = true; // ...enable Disconnect button...
                trayMenuDisconnect.Enabled = true; // ...enable Disconnect button in tray menu...
                toolStripStatusLabelStatus.Text = Strings.statusConnecting; // and update the connection status label
            }
        }

        /// <summary>
        /// Called when you press the Disconnect button.
        /// </summary>
        private void Disconnect(object sender, EventArgs e) => Disconnect();

        /// <summary>
        /// Disconnects from Discord and makes UI changes.
        /// </summary>
        private void Disconnect()
        {
            buttonConnect.Enabled = true;
            buttonDisconnect.Enabled = false;
            trayMenuDisconnect.Enabled = false;
            buttonUpdatePresence.Enabled = false;
            toolStripStatusLabelUsername.Text = "";
            toolStripStatusLabelStatus.Text = Strings.statusDisconnected;
            trayIcon.Text = $"{res.GetString("trayIcon.Text")}{(Program.IsSecondInstance ? " (2)" : "")}";

            textBoxID.BackColor = CurrentColors.BgTextFields;
            ConnectionManager.State = ConnectionState.Disconnected;

            restartTimer.Stop();
            localTimeTimer.Stop();

            client.Dispose();

            Analytics.TrackEvent("Disconnected");
        }

        /// <summary>
        /// Disconnects from Discord and instantly connects back.
        /// </summary>
        private void Reconnect()
        {
            // Quick disconnect
            restartTimer.Stop();
            localTimeTimer.Stop();
            client.Dispose();
            textBoxID.BackColor = CurrentColors.BgTextFields;

            // Quick connect
            Utils.SaveSettings();
            if (Init())
            {
                ConnectionManager.State = ConnectionState.Connecting;
                toolStripStatusLabelStatus.Text = Strings.statusConnecting;
            }
            else
                Disconnect(); // In case something goes wrong, disconnect fully.
        }

        /// <summary>
        /// Called when you press the Update Presence button.
        /// </summary>
        private void Update(object sender, EventArgs e)
        {
            Utils.SaveSettings();
            if (client.ApplicationID == (string.IsNullOrEmpty(settings.id) ? defaultID : settings.id))
            SetPresence();
            else
                Reconnect();
        }
    }
}