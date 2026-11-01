// ==============================================================================
// RICHIFY - Discord Custom Rich Presence Manager
// Developed by SasynDev (https://github.com/SasynDev)
// Copyright (c) 2026 SasynDev. All rights reserved.
// Licensed under the MIT License.
// ==============================================================================

using System.Drawing;
using System.Windows.Forms;

namespace RICHIFY
{
    /// <summary>
    /// Color scheme based on current mode.
    /// </summary>
    public static class CurrentColors
    {
        public static Color BgInactive => Properties.Settings.Default.darkMode ? Color.FromArgb(12, 16, 28) : Color.FromArgb(242, 243, 245);
        public static Color BgHover => Properties.Settings.Default.darkMode ? Color.FromArgb(26, 34, 56) : Color.FromArgb(220, 225, 235);
        public static Color BgActive => Properties.Settings.Default.darkMode ? Color.FromArgb(36, 46, 74) : Color.FromArgb(200, 210, 225);
        public static Color CheckBg => Color.FromArgb(237, 66, 69);
        public static Color CheckHover => Color.FromArgb(250, 85, 90);
        public static Color TextInactive => Properties.Settings.Default.darkMode ? Color.FromArgb(135, 145, 170) : Color.FromArgb(142, 146, 151);
        public static Color SeparatorColor => Properties.Settings.Default.darkMode ? Color.FromArgb(30, 255, 255, 255) : Color.FromArgb(215, 220, 235);
        public static Color GlassBorder => Properties.Settings.Default.darkMode ? Color.FromArgb(38, 255, 255, 255) : Color.FromArgb(205, 215, 230);
        public static Color BgColor { get; private set; }
        public static Color BgTextFields { get; private set; }
        public static Color BgTextFieldsSuccess { get; private set; }
        public static Color BgTextFieldsError { get; private set; }
        public static Color TextColor { get; private set; }

        static CurrentColors()
        {
            Update();
        }

        /// <summary>
        /// Updates colors according to currently set dark mode setting.
        /// </summary>
        public static void Update()
        {
            if (Properties.Settings.Default.darkMode)
            {
                // Frosted Glassmorphism Theme (deep obsidian with luminous nebulas)
                BgColor = Color.FromArgb(10, 13, 22);
                BgTextFields = Color.FromArgb(16, 21, 36);
                BgTextFieldsSuccess = Color.FromArgb(18, 56, 34);
                BgTextFieldsError = Color.FromArgb(64, 20, 28);
                TextColor = Color.FromArgb(245, 248, 255);
            }
            else
            {
                BgColor = Color.FromArgb(246, 248, 252);
                BgTextFields = Color.FromArgb(255, 255, 255);
                BgTextFieldsSuccess = Color.FromArgb(215, 245, 225);
                BgTextFieldsError = Color.FromArgb(250, 220, 225);
                TextColor = Color.FromArgb(28, 32, 45);
            }
        }
    }

    internal class DarkModeColorTable : ProfessionalColorTable
    {
        public override Color MenuItemSelected => CurrentColors.BgHover;
        public override Color MenuItemPressedGradientBegin => CurrentColors.BgActive;
        public override Color MenuItemPressedGradientEnd => MenuItemPressedGradientBegin;
        public override Color MenuItemPressedGradientMiddle => MenuItemPressedGradientBegin;
        public override Color MenuItemSelectedGradientBegin => CurrentColors.BgHover;
        public override Color MenuItemSelectedGradientEnd => MenuItemSelectedGradientBegin;
        public override Color ImageMarginGradientBegin => Color.FromArgb(14, 18, 30);
        public override Color ImageMarginGradientMiddle => ImageMarginGradientBegin;
        public override Color ImageMarginGradientEnd => ImageMarginGradientBegin;
        public override Color CheckSelectedBackground => Color.Transparent;
        public override Color CheckBackground => Color.Transparent;
        public override Color MenuItemBorder => CurrentColors.BgActive;
        public override Color MenuBorder => Color.FromArgb(38, 48, 76);
        public override Color ToolStripDropDownBackground => Color.FromArgb(14, 18, 30);
        public override Color SeparatorDark => Color.FromArgb(32, 42, 65);
        public override Color SeparatorLight => Color.Transparent;
    }

    internal class DarkModeRenderer : ToolStripProfessionalRenderer
    {
        public DarkModeRenderer() : base(new DarkModeColorTable()) { }

        protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)
        {
            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            using (var pen = new Pen(Color.FromArgb(42, 54, 84), 1f))
            {
                e.Graphics.DrawRectangle(pen, 0, 0, e.ToolStrip.Width - 1, e.ToolStrip.Height - 1);
            }
        }

        protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
        {
            e.Graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
            e.TextColor = e.Item.Selected ? Color.White : (e.Item.ForeColor == Control.DefaultForeColor ? CurrentColors.TextColor : e.Item.ForeColor);

            base.OnRenderItemText(e);
        }

        protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e)
        {
            using (var brush = new SolidBrush(Color.FromArgb(14, 18, 30)))
            {
                e.Graphics.FillRectangle(brush, e.AffectedBounds);
            }
        }

        protected override void OnRenderItemCheck(ToolStripItemImageRenderEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

            int size = 16;
            int x = e.ImageRectangle.X + (e.ImageRectangle.Width - size) / 2;
            int y = e.ImageRectangle.Y + (e.ImageRectangle.Height - size) / 2;
            var boxRect = new Rectangle(x, y, size, size);

            using (var brush = new SolidBrush(Color.FromArgb(237, 66, 69)))
            using (var pen = new Pen(Color.FromArgb(255, 100, 105), 1f))
            {
                var path = new System.Drawing.Drawing2D.GraphicsPath();
                int r = 3;
                int d = r * 2;
                path.AddArc(boxRect.X, boxRect.Y, d, d, 180, 90);
                path.AddArc(boxRect.Right - d, boxRect.Y, d, d, 270, 90);
                path.AddArc(boxRect.Right - d, boxRect.Bottom - d, d, d, 0, 90);
                path.AddArc(boxRect.X, boxRect.Bottom - d, d, d, 90, 90);
                path.CloseFigure();

                g.FillPath(brush, path);
                g.DrawPath(pen, path);
                path.Dispose();
            }

            using (var checkPen = new Pen(Color.White, 2f))
            {
                checkPen.StartCap = System.Drawing.Drawing2D.LineCap.Round;
                checkPen.EndCap = System.Drawing.Drawing2D.LineCap.Round;
                g.DrawLines(checkPen, new Point[] {
                    new Point(boxRect.X + 3, boxRect.Y + 8),
                    new Point(boxRect.X + 6, boxRect.Y + 12),
                    new Point(boxRect.X + 12, boxRect.Y + 4)
                });
            }
        }

        protected override void OnRenderItemImage(ToolStripItemImageRenderEventArgs e)
        {
            base.OnRenderItemImage(e);

            if (e.Item.Tag == null)
                return;

            if (e.Item.Image != null && (e.Item.Image.Tag == null || (string)e.Item.Image.Tag == "light"))
            {
                e.Item.Image = Properties.Resources.globe_white;
                e.Item.Image.Tag = "dark";
            }
        }

        protected override void OnRenderArrow(ToolStripArrowRenderEventArgs e)
        {
            e.ArrowColor = Color.FromArgb(210, 220, 240);
            base.OnRenderArrow(e);
        }
    }

    internal class LightModeRenderer : ToolStripProfessionalRenderer
    {
        protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e)
        {
            e.ToolStrip.BackColor = Color.FromArgb(242, 243, 245);

            base.OnRenderToolStripBackground(e);
        }

        protected override void OnRenderItemImage(ToolStripItemImageRenderEventArgs e)
        {
            base.OnRenderItemImage(e);

            if (e.Item.Tag == null)
                return;

            if (e.Item.Image.Tag == null || (string)e.Item.Image.Tag == "dark")
            {
                e.Item.Image = Properties.Resources.globe;
                e.Item.Image.Tag = "light";
            }
        }
    }
}
