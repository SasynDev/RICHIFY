// ==============================================================================
// RICHIFY - Discord Custom Rich Presence Manager
// Developed by SasynDev (https://github.com/SasynDev)
// Copyright (c) 2026 SasynDev. All rights reserved.
// Licensed under the MIT License.
// ==============================================================================

using System.Windows.Forms;

namespace RICHIFY
{
    public partial class About : Form
    {
        public About()
        {
            InitializeComponent();

            Icon = Properties.Resources.favicon;
            pictureBoxLogo.Image = Properties.Resources.logo;

            WinApi.UseImmersiveDarkMode(Handle);

            BackColor = CurrentColors.BgColor;
            ForeColor = CurrentColors.TextColor;

            buttonClose.FlatStyle = FlatStyle.Flat;
            buttonClose.BackColor = System.Drawing.Color.FromArgb(237, 66, 69);
            buttonClose.ForeColor = System.Drawing.Color.White;
            buttonClose.FlatAppearance.BorderColor = System.Drawing.Color.FromArgb(255, 95, 100);
            buttonClose.FlatAppearance.MouseOverBackColor = System.Drawing.Color.FromArgb(250, 85, 90);
            buttonClose.Cursor = Cursors.Hand;

            labelVersion.Text = VersionHelper.GetVersionString(Application.ProductVersion);
#if DEBUG
            labelVersion.Text += " (DEV)";
#endif

            labelTitle.Text = "RICHIFY";
            labelMadeBy.Text = "Build by: SasynDev";
            linkLabelWebsite.Text = "github.com/SasynDev/RICHIFY";

            labelTitle.Left = 118 + (259 - labelTitle.Width) / 2;
            labelTitle.Top = 12 + (66 - labelTitle.Height) / 2;
            labelMadeBy.Left = 118 + (259 - labelMadeBy.Width) / 2;
        }

        private void OpenWebsite(object sender, LinkLabelLinkClickedEventArgs e)
        {
            Utils.OpenInBrowser("https://github.com/SasynDev/RICHIFY");
        }
    }
}
