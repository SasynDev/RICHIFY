// ==============================================================================
// RICHIFY - Discord Custom Rich Presence Manager
// Developed by SasynDev (https://github.com/SasynDev)
// Copyright (c) 2026 SasynDev. All rights reserved.
// Licensed under the MIT License.
// ==============================================================================

using System;
using System.Drawing;
using System.Windows.Forms;

namespace RICHIFY
{
    public partial class UpdatePrompt : Form
    {
        public UpdatePrompt(Version current, Version latest, string changelog)
        {
            InitializeComponent();

            Icon = Properties.Resources.favicon;
            pictureBoxLogo.Image = Properties.Resources.logo;

            WinApi.UseImmersiveDarkMode(Handle);

            BackColor = Color.FromArgb(12, 16, 26);
            ForeColor = Color.FromArgb(245, 248, 255);

            htmlPanelChangelog.BackColor = Color.FromArgb(16, 21, 34);
            htmlPanelChangelog.BaseStylesheet = @"
                * { font-family: 'Segoe UI', Tahoma, sans-serif; font-size: 13px; color: #EDF2FA; }
                body { background-color: #101524; color: #EDF2FA; margin: 8px; padding: 4px; }
                h1, h2, h3, h4 { color: #ED4245; font-weight: bold; margin-top: 10px; margin-bottom: 4px; }
                ul, ol { margin-left: 18px; color: #D5DEEC; }
                li { margin-bottom: 3px; color: #D5DEEC; }
                a { color: #58A6FF; text-decoration: none; }
                p { margin-bottom: 6px; }
                code { background-color: #1D253A; color: #FF758F; padding: 2px 4px; border-radius: 3px; }
            ";

            labelVersionsText.ForeColor = Color.FromArgb(180, 195, 220);
            labelVersions.ForeColor = Color.White;
            labelQuestion.ForeColor = Color.FromArgb(240, 244, 252);
            checkBoxNeverNotify.ForeColor = Color.FromArgb(200, 210, 230);

            buttonUpdate.FlatStyle = buttonNotNow.FlatStyle = buttonSkipUpdate.FlatStyle = FlatStyle.Flat;
            buttonUpdate.Cursor = buttonNotNow.Cursor = buttonSkipUpdate.Cursor = Cursors.Hand;

            buttonUpdate.BackColor = Color.FromArgb(237, 66, 69);
            buttonUpdate.ForeColor = Color.White;
            buttonUpdate.FlatAppearance.BorderColor = Color.FromArgb(255, 95, 100);
            buttonUpdate.FlatAppearance.MouseOverBackColor = Color.FromArgb(250, 85, 90);

            buttonNotNow.BackColor = Color.FromArgb(28, 36, 56);
            buttonNotNow.ForeColor = Color.FromArgb(230, 235, 248);
            buttonNotNow.FlatAppearance.BorderColor = Color.FromArgb(50, 68, 105);
            buttonNotNow.FlatAppearance.MouseOverBackColor = Color.FromArgb(40, 52, 80);

            buttonSkipUpdate.BackColor = Color.FromArgb(20, 26, 42);
            buttonSkipUpdate.ForeColor = Color.FromArgb(160, 175, 205);
            buttonSkipUpdate.FlatAppearance.BorderColor = Color.FromArgb(40, 50, 80);
            buttonSkipUpdate.FlatAppearance.MouseOverBackColor = Color.FromArgb(32, 40, 65);

            string currentStr = VersionHelper.GetVersionString(current);
            string latestStr = VersionHelper.GetVersionString(latest);

            labelVersions.Text = currentStr + "\r\n" + latestStr;

            htmlPanelChangelog.Text = changelog;

            System.Media.SystemSounds.Beep.Play();
            Activate();
        }

        /// <summary>
        /// Called when changing the state of <see cref="checkBoxNeverNotify"/>.
        /// </summary>
        private void ToggleUpdates(object sender, EventArgs e)
        {
            var settings = Properties.Settings.Default;

            settings.checkUpdates = !((CheckBox)sender).Checked;
            Utils.SaveSettings();
        }
    }
}
