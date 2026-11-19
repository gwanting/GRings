using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using QuotaData;

namespace DualQuotaBar {
    sealed class QuotaCard : Form {
        static readonly Color Surface = Color.FromArgb(28, 28, 30);
        static readonly Color Muted = Color.FromArgb(172, 178, 188);
        static readonly Color Accent = Color.FromArgb(58, 205, 215);
        readonly Label plan, status, shortRemaining, shortReset, weeklyRemaining, weeklyReset, resetCardsValue, paceText;
        readonly ProgressLine shortBar, weeklyBar;
        readonly Button pace;
        readonly Font titleFont = CardFont(20, FontStyle.Bold);
        readonly Font sectionFont = CardFont(20, FontStyle.Bold);
        readonly Font bodyFont = CardFont(16);
        readonly Font budgetFont = CardFont(15);
        int collapsedHeight;
        bool expanded;

        protected override bool ShowWithoutActivation { get { return true; } }

        public QuotaCard() {
            Text = "Codex 用量卡片";
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            TopMost = true;
            AutoScaleMode = AutoScaleMode.None;
            ClientSize = new Size(468, 384);
            BackColor = Surface;
            ForeColor = Color.White;
            Font = bodyFont;

            AddText("Codex", 10, 18, 230, 32, titleFont);
            status = AddText("", 10, 54, 200, 24, color: Muted);
            plan = AddText("", 220, 54, 238, 24, color: Muted, alignment: ContentAlignment.MiddleRight);
            AddText("5 小时", 10, 110, 445, 34, sectionFont);
            shortBar = AddLine(150);
            shortRemaining = AddText("", 10, 166, 153, 29);
            shortReset = AddText("", 163, 166, 295, 29, color: Muted, alignment: ContentAlignment.MiddleRight);
            AddText("本周", 10, 211, 445, 34, sectionFont);
            weeklyBar = AddLine(251);
            weeklyRemaining = AddText("", 10, 267, 153, 29);
            weeklyReset = AddText("", 163, 267, 295, 29, color: Muted, alignment: ContentAlignment.MiddleRight);
            AddText("重置卡", 10, 308, 153, 28, color: Muted);
            resetCardsValue = AddText("—", 163, 308, 295, 28, color: Muted, alignment: ContentAlignment.MiddleRight);
            pace = new Button { Text = "›   按节奏预算", Bounds = new Rectangle(8, 342, 200, 30), Font = budgetFont,
                TextAlign = ContentAlignment.MiddleLeft, FlatStyle = FlatStyle.Flat, ForeColor = Muted, BackColor = Surface };
            pace.FlatAppearance.BorderSize = 0;
            pace.Click += (sender, args) => TogglePace();
            Controls.Add(pace);
            paceText = AddText("", 10, 372, 445, 52, budgetFont, Muted);
            paceText.Visible = false;
        }

        static Font CardFont(float pixels, FontStyle style = FontStyle.Regular) {
            return new Font("Microsoft YaHei UI", pixels, style, GraphicsUnit.Pixel);
        }

        Label AddText(string text, int x, int y, int width, int height, Font font = null,
            Color? color = null, ContentAlignment alignment = ContentAlignment.MiddleLeft) {
            var label = new Label { Text = text, Bounds = new Rectangle(x, y, width, height),
                TextAlign = alignment, ForeColor = color ?? ForeColor, AutoEllipsis = true };
            if (font != null) label.Font = font;
            Controls.Add(label);
            return label;
        }

        protected override void Dispose(bool disposing) {
            base.Dispose(disposing);
            if (!disposing) return;
            titleFont.Dispose();
            sectionFont.Dispose();
            bodyFont.Dispose();
            budgetFont.Dispose();
        }

        ProgressLine AddLine(int y) {
            var line = new ProgressLine { Bounds = new Rectangle(13, y, 442, 9) };
            Controls.Add(line);
            return line;
        }

        void TogglePace() {
            if (!expanded) collapsedHeight = ClientSize.Height;
            expanded = !expanded;
            pace.Text = expanded ? "⌄   按节奏预算" : "›   按节奏预算";
            int newHeight = expanded ? paceText.Bottom + 10 : collapsedHeight;
            int bottom = Bottom;
            ClientSize = new Size(ClientSize.Width, newHeight);
            Top = Math.Max(Screen.FromRectangle(Bounds).WorkingArea.Top + 8, bottom - Height);
            paceText.Visible = expanded;
        }

        public void UpdateQuota(Snapshot snapshot, bool refreshing, bool failed) {
            DateTimeOffset now = DateTimeOffset.Now;
            var shortWindow = snapshot == null ? null : snapshot.Windows.Find(w => w.Minutes == 300);
            var weeklyWindow = snapshot == null ? null : snapshot.Windows.Find(w => w.Minutes == 10080);
            bool shortCurrent = snapshot != null && snapshot.IsCurrent(shortWindow, now);
            bool weeklyCurrent = snapshot != null && snapshot.IsCurrent(weeklyWindow, now);
            plan.Text = snapshot == null ? "" : PlanText(snapshot.Plan);
            if (shortCurrent || weeklyCurrent) {
                TimeSpan age = now - snapshot.Fetched;
                status.Text = age.TotalMinutes < 1 ? "刚刚更新" : ((int)age.TotalMinutes) + " 分钟前更新";
            } else status.Text = refreshing ? "正在刷新…" : failed ? "刷新失败，等待重试" : "等待最新额度";

            UpdateRow(shortWindow, shortCurrent, now, shortRemaining, shortReset, shortBar);
            UpdateRow(weeklyWindow, weeklyCurrent, now, weeklyRemaining, weeklyReset, weeklyBar);
            if (snapshot != null && snapshot.ResetCredits.HasValue) {
                int count = snapshot.ResetCredits.Value;
                resetCardsValue.Text = count + " 张可用";
                resetCardsValue.ForeColor = count > 0 ? Accent : Muted;
            } else {
                resetCardsValue.Text = "—";
                resetCardsValue.ForeColor = Muted;
            }
            paceText.Text = "5 小时  " + PaceText(shortWindow, shortCurrent, now) + "\n本周     " + PaceText(weeklyWindow, weeklyCurrent, now);
        }

        static string PlanText(string value) {
            if (String.IsNullOrWhiteSpace(value)) return "";
            switch (value.Trim().ToLowerInvariant()) {
                case "free": return "ChatGPT Free";
                case "plus": return "ChatGPT Plus";
                case "pro": return "ChatGPT Pro";
                case "team": return "ChatGPT Team";
                case "business": return "ChatGPT Business";
                case "enterprise": return "ChatGPT Enterprise";
                case "edu": return "ChatGPT Edu";
                default: return value;
            }
        }

        static void UpdateRow(Window window, bool current, DateTimeOffset now, Label remaining, Label reset, ProgressLine bar) {
            remaining.Text = current ? window.Percent + " 剩余" : "— 剩余";
            reset.Text = window == null || !window.Reset.HasValue ? "重置时间未知" : ResetText(window.Reset.Value - now);
            bar.SetValue(current ? window.Remaining.Value : 0);
        }

        static string ResetText(TimeSpan time) {
            if (time <= TimeSpan.Zero) return "等待重置确认";
            if (time.TotalDays >= 1) return String.Format("{0} 天 {1} 小时后重置", (int)time.TotalDays, time.Hours);
            if (time.TotalMinutes < 1) return "不到 1 分钟后重置";
            return String.Format("{0} 小时 {1} 分钟后重置", time.Hours, time.Minutes);
        }

        static string PaceText(Window window, bool current, DateTimeOffset now) {
            if (!current) return "暂无数据";
            var time = window.TimeRemaining(now);
            if (!time.HasValue) return "暂无重置时间";
            double margin = window.Remaining.Value - time.Value;
            return Math.Abs(margin) < 1 ? "用量与时间进度接近" :
                String.Format("消耗{0}约 {1:0} 个百分点", margin < 0 ? "领先时间进度" : "慢于时间进度", Math.Abs(margin));
        }

        sealed class ProgressLine : Control {
            double value;
            public ProgressLine() { DoubleBuffered = true; BackColor = Surface; }
            public void SetValue(double next) { if (value == next) return; value = next; Invalidate(); }
            protected override void OnPaint(PaintEventArgs e) {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                Fill(e.Graphics, ClientRectangle, Color.FromArgb(62, 62, 66));
                if (value > 0) Fill(e.Graphics, new RectangleF(0, 0, Math.Max(3, Width * (float)(value / 100)), Height),
                    value <= 10 ? Color.FromArgb(237, 165, 66) : Color.FromArgb(75, 123, 240));
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
