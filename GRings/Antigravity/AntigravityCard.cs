using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using DeepSeekBalance;

namespace AntigravityQuotaBar {
    sealed class AntigravityCard : Form {
        [DllImport("dwmapi.dll")] static extern int DwmFlush();
        [DllImport("user32.dll")] static extern bool SetWindowPos(IntPtr hwnd, IntPtr insertAfter, int x, int y, int cx, int cy, uint flags);
        static readonly Color Surface = Color.FromArgb(28, 28, 30);
        static readonly Color Muted = Color.FromArgb(172, 178, 188);
        Color outerColor = Color.FromArgb(249, 136, 179);
        Color innerColor = Color.FromArgb(226, 210, 174);
        static readonly Color WarnAmber = Color.FromArgb(237, 165, 66);

        readonly Label plan, status, primaryTitle, primaryRemaining, primaryReset, secondaryTitle, secondaryRemaining, secondaryReset, thirdPartyValue, argonValue;
        readonly ProgressLine primaryBar, secondaryBar;
        readonly Button actionButton;
        readonly Font titleFont = CardFont(20, FontStyle.Bold);
        readonly Font sectionFont = CardFont(20, FontStyle.Bold);
        readonly Font bodyFont = CardFont(16);
        readonly Font smallFont = CardFont(14);
        readonly Action onAction;
        PanelMotion motion;
        Bitmap cachedMotionFrame;
        string lastMotionKey;

        public bool IsOpen { get { return motion == null ? Visible : motion.TargetVisible; } }
        public void HideImmediately() { if (motion != null) { motion.Dispose(); motion = null; } Hide(); }
        public void ShowAnimated(Point anchor) {
            try {
                if (motion == null) motion = new PanelMotion(this, CaptureMotionFrame, RevealBehindMotion);
                motion.Show(anchor);
            } catch {
                if (motion != null) { motion.Dispose(); motion = null; }
                Show();
            }
        }
        public void HideAnimated(Point anchor) {
            try {
                if (motion == null) { Hide(); return; }
                motion.Hide(anchor);
            } catch {
                if (motion != null) { motion.Dispose(); motion = null; }
                Hide();
            }
        }
        void InvalidateMotionCache() { if (cachedMotionFrame != null) { cachedMotionFrame.Dispose(); cachedMotionFrame = null; } }
        void RevealBehindMotion(IntPtr motionHandle) {
            if (!Visible) Show();
            const uint showFlags = 0x0010 | 0x0040 | 0x0200;
            const uint keepFlags = 0x0001 | 0x0002 | 0x0010 | 0x0200;
            if (motionHandle != IntPtr.Zero) {
                SetWindowPos(motionHandle, new IntPtr(-1), 0, 0, 0, 0, keepFlags);
                SetWindowPos(Handle, motionHandle, Left, Top, Width, Height, showFlags);
            } else {
                SetWindowPos(Handle, new IntPtr(-1), Left, Top, Width, Height, showFlags);
            }
            Update();
            if (motionHandle != IntPtr.Zero) try { DwmFlush(); } catch { }
        }
        Bitmap CaptureMotionFrame() {
            if (cachedMotionFrame != null) return (Bitmap)cachedMotionFrame.Clone();
            bool wasVisible = Visible;
            Point originalLocation = Location;
            Bitmap raw = null;
            try {
                if (!wasVisible) { Location = new Point(-32000, -32000); Show(); }
                raw = new Bitmap(Width, Height, System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
                DrawToBitmap(raw, new Rectangle(0, 0, Width, Height));
            } finally {
                if (!wasVisible) { Hide(); Location = originalLocation; }
            }
            var frame = new Bitmap(Width, Height, System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
            using (var g = Graphics.FromImage(frame))
            using (var clip = RoundedShape(Width, Height, 24)) {
                g.Clear(Color.Transparent);
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.SetClip(clip);
                g.DrawImageUnscaled(raw, 0, 0);
            }
            raw.Dispose();
            cachedMotionFrame = (Bitmap)frame.Clone();
            return frame;
        }
        static GraphicsPath RoundedShape(int w, int h, int d) {
            var p = new GraphicsPath();
            p.AddArc(0, 0, d, d, 180, 90);
            p.AddArc(w - d, 0, d, d, 270, 90);
            p.AddArc(w - d, h - d, d, d, 0, 90);
            p.AddArc(0, h - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }
        protected override void OnSizeChanged(EventArgs e) {
            base.OnSizeChanged(e);
            if (Width >= 24 && Height >= 24) using (var p = RoundedShape(Width, Height, 24)) Region = new Region(p);
            InvalidateMotionCache();
        }

        protected override bool ShowWithoutActivation { get { return true; } }

        public AntigravityCard(Action onSetupAction) {
            onAction = onSetupAction;
            Text = "Antigravity 用量卡片";
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            TopMost = true;
            AutoScaleMode = AutoScaleMode.None;
            ClientSize = new Size(468, 404);
            BackColor = Surface;
            ForeColor = Color.White;
            Font = bodyFont;

            AddText("Antigravity", 12, 18, 240, 32, titleFont);
            status = AddText("", 12, 54, 240, 24, color: Muted);
            plan = AddText("", 254, 54, 202, 24, color: Muted, alignment: ContentAlignment.MiddleRight);

            primaryTitle = AddText("Gemini · 5 小时", 12, 108, 444, 34, sectionFont);
            primaryBar = AddLine(148, outerColor);
            primaryRemaining = AddText("— 剩余", 12, 164, 120, 28);
            primaryReset = AddText("等待读取", 132, 164, 324, 28, color: Muted, alignment: ContentAlignment.MiddleRight);

            secondaryTitle = AddText("Gemini · 本周", 12, 208, 444, 34, sectionFont);
            secondaryBar = AddLine(248, innerColor);
            secondaryRemaining = AddText("— 剩余", 12, 264, 120, 28);
            secondaryReset = AddText("等待读取", 132, 264, 324, 28, color: Muted, alignment: ContentAlignment.MiddleRight);

            AddText("Claude / GPT", 12, 306, 155, 26, color: Muted);
            thirdPartyValue = AddText("—", 167, 306, 289, 26, color: Muted, alignment: ContentAlignment.MiddleRight);

            AddText("Gemini 4 Argon", 12, 338, 155, 26, color: Muted);
            argonValue = AddText("—", 167, 338, 289, 26, color: Muted, alignment: ContentAlignment.MiddleRight);

            actionButton = new Button {
                Text = "安装 / 登录官方 agy CLI ▸",
                Bounds = new Rectangle(12, 368, 240, 28),
                Font = smallFont,
                TextAlign = ContentAlignment.MiddleLeft,
                FlatStyle = FlatStyle.Flat,
                ForeColor = outerColor,
                BackColor = Surface,
                Cursor = Cursors.Hand
            };
            actionButton.FlatAppearance.BorderSize = 0;
            actionButton.Click += (s, e) => { if (onAction != null) onAction(); };
            Controls.Add(actionButton);
        }

        static Font CardFont(float pixels, FontStyle style = FontStyle.Regular) {
            return new Font("Microsoft YaHei UI", pixels, style, GraphicsUnit.Pixel);
        }

        Label AddText(string text, int x, int y, int width, int height, Font font = null,
            Color? color = null, ContentAlignment alignment = ContentAlignment.MiddleLeft) {
            var label = new Label {
                Text = text,
                Bounds = new Rectangle(x, y, width, height),
                TextAlign = alignment,
                ForeColor = color ?? ForeColor,
                AutoEllipsis = true
            };
            if (font != null) label.Font = font;
            Controls.Add(label);
            return label;
        }

        ProgressLine AddLine(int y, Color activeColor) {
            var line = new ProgressLine(activeColor) { Bounds = new Rectangle(13, y, 442, 9) };
            Controls.Add(line);
            return line;
        }

        public void SetColors(Color outer, Color inner) {
            outerColor = outer;
            innerColor = inner;
            primaryBar.SetColor(outer);
            secondaryBar.SetColor(inner);
            actionButton.ForeColor = outer;
            InvalidateMotionCache();
        }

        public void UpdateQuota(AntigravitySnapshot snapshot, bool refreshing, string error) {
            DateTimeOffset now = DateTimeOffset.Now;
            bool current = snapshot != null && snapshot.IsCurrent(now);
            plan.Text = snapshot != null && !String.IsNullOrWhiteSpace(snapshot.Plan) ? snapshot.Plan : "Google Antigravity";

            if (current) {
                TimeSpan age = now - snapshot.Fetched;
                status.Text = age.TotalMinutes < 1 ? "刚刚更新" : ((int)age.TotalMinutes) + " 分钟前更新";
                status.ForeColor = Muted;
            } else if (refreshing) {
                status.Text = "正在刷新…";
                status.ForeColor = Muted;
            } else if (!String.IsNullOrEmpty(error)) {
                status.Text = error;
                status.ForeColor = WarnAmber;
            } else {
                status.Text = "等待最新额度";
                status.ForeColor = Muted;
            }

            UpdateRow(snapshot != null ? snapshot.Primary : null, "Gemini · 5 小时", current, now, false, primaryTitle, primaryRemaining, primaryReset, primaryBar);
            UpdateRow(snapshot != null ? snapshot.Secondary : null, "Gemini · 本周", current, now, true, secondaryTitle, secondaryRemaining, secondaryReset, secondaryBar);

            var p3Five = snapshot != null ? snapshot.FindById("3p-5h") : null;
            var p3Week = snapshot != null ? snapshot.FindById("3p-weekly") : null;
            if (current && (p3Five != null || p3Week != null)) {
                thirdPartyValue.Text = String.Format("5h {0}  ·  本周 {1}", p3Five != null ? p3Five.Percent : "—", p3Week != null ? p3Week.Percent : "—");
                thirdPartyValue.ForeColor = Color.White;
            } else {
                thirdPartyValue.Text = "—";
                thirdPartyValue.ForeColor = Muted;
            }

            var argon = snapshot != null ? snapshot.FindById("gemini-argon-wtu-weekly") : null;
            if (current && argon != null) {
                argonValue.Text = "本周 " + argon.Percent + " 剩余";
                argonValue.ForeColor = Color.White;
            } else {
                argonValue.Text = "—";
                argonValue.ForeColor = Muted;
            }

            bool needSetup = !current && !String.IsNullOrEmpty(error);
            actionButton.Visible = needSetup;
            actionButton.Text = AntigravityClient.FindAgyExe() == null ? "安装官方 agy CLI ▸" : "打开终端登录 agy ▸";
            string key = plan.Text + "|" + status.Text + "|" + primaryRemaining.Text + "|" + primaryReset.Text + "|" + secondaryRemaining.Text + "|" + secondaryReset.Text + "|" + thirdPartyValue.Text + "|" + argonValue.Text + "|" + needSetup;
            if (key != lastMotionKey) { lastMotionKey = key; InvalidateMotionCache(); }
        }

        static void UpdateRow(QuotaPool pool, string fallbackTitle, bool current, DateTimeOffset now, bool isWeekly,
            Label title, Label remaining, Label reset, ProgressLine bar) {
            title.Text = pool != null && !String.IsNullOrWhiteSpace(pool.Name) ? pool.Name : fallbackTitle;
            bool valid = current && pool != null && pool.Remaining.HasValue;
            remaining.Text = valid ? pool.Percent + " 剩余" : "— 剩余";
            reset.Text = pool == null ? "等待数据" : pool.ResetText(now, isWeekly);
            bar.SetValue(valid ? pool.Remaining.Value : 0);
        }

        protected override void Dispose(bool disposing) {
            base.Dispose(disposing);
            if (!disposing) return;
            InvalidateMotionCache();
            if (motion != null) { motion.Dispose(); motion = null; }
            titleFont.Dispose();
            sectionFont.Dispose();
            bodyFont.Dispose();
            smallFont.Dispose();
        }

        sealed class ProgressLine : Control {
            Color normalColor;
            double value;
            public ProgressLine(Color color) {
                normalColor = color;
                DoubleBuffered = true;
                BackColor = Surface;
            }
            public void SetColor(Color color) {
                normalColor = color;
                Invalidate();
            }
            public void SetValue(double next) {
                if (value == next) return;
                value = next;
                Invalidate();
            }
            protected override void OnPaint(PaintEventArgs e) {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                Fill(e.Graphics, ClientRectangle, Color.FromArgb(62, 62, 66));
                if (value > 0) {
                    Fill(e.Graphics, new RectangleF(0, 0, Math.Max(3, Width * (float)(value / 100.0)), Height),
                        value <= 10 ? WarnAmber : normalColor);
                }
            }
            static void Fill(Graphics graphics, RectangleF area, Color color) {
                if (area.Width <= 0 || area.Height <= 0) return;
                float diameter = Math.Min(area.Width, area.Height);
                using (var path = new GraphicsPath())
                using (var brush = new SolidBrush(color)) {
                    path.AddArc(area.Left, area.Top, diameter, area.Height, 90, 180);
                    path.AddArc(area.Right - diameter, area.Top, diameter, area.Height, 270, 180);
                    path.CloseFigure();
                    graphics.FillPath(brush, path);
                }
            }
        }
    }
}
