using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using QuotaTray;

namespace AntigravityQuotaBar {
    static class Program {
        [DllImport("user32.dll")] static extern bool SetProcessDpiAwarenessContext(IntPtr context);
        [STAThread]
        static void Main() {
            try { SetProcessDpiAwarenessContext(new IntPtr(-4)); } catch { }
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            bool fresh;
            using (var mutex = new Mutex(true, "Local\\Wanting.AntigravityQuotaBar", out fresh)) {
                if (!fresh) return;
                Application.Run(new Bar());
            }
        }
    }

    sealed class Bar : Form {
        Color outerColor = Color.FromArgb(249, 136, 179); // Warm Pink (#F988B3)
        Color innerColor = Color.FromArgb(226, 210, 174); // Light Khaki (#E2D2AE)
        static readonly string ThemeFile = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GRings", "theme.txt");

        readonly RingClicks ringClicks;
        AntigravityCard card;
        bool blackDigits;
        FullscreenVisibility fullscreen;
        volatile bool fullscreenHidden;
        AntigravitySnapshot snapshot;
        bool busy, closing;
        string error;
        readonly ToolTip tip = new ToolTip();
        readonly System.Windows.Forms.Timer refresh, position;
        DateTime fetched;
        string statusText;
        IntPtr mouseHook;
        MouseHookCallback mouseCallback;
        bool ringPressed, ringRightPressed;

        [StructLayout(LayoutKind.Sequential)] public struct Rect { public int Left, Top, Right, Bottom; }
        [StructLayout(LayoutKind.Sequential)] struct NativePoint { public int X, Y; public NativePoint(int x, int y) { X = x; Y = y; } }
        [StructLayout(LayoutKind.Sequential)] struct NativeSize { public int Width, Height; public NativeSize(int w, int h) { Width = w; Height = h; } }
        [StructLayout(LayoutKind.Sequential, Pack = 1)] struct Blend { public byte Op, Flags, Alpha, Format; }
        [StructLayout(LayoutKind.Sequential)] struct MouseEvent { public NativePoint Point; public uint MouseData, Flags, Time; public IntPtr ExtraInfo; }
        delegate IntPtr MouseHookCallback(int code, IntPtr message, IntPtr data);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern IntPtr FindWindow(string cls, string title);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern IntPtr FindWindowEx(IntPtr parent, IntPtr after, string cls, string title);
        [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr handle, out Rect rect);
        [DllImport("user32.dll")] static extern bool SetWindowPos(IntPtr handle, IntPtr after, int x, int y, int width, int height, uint flags);
        [DllImport("user32.dll")] static extern bool ShowWindow(IntPtr handle, int command);
        [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr handle);
        [DllImport("user32.dll")] static extern bool SetForegroundWindow(IntPtr handle);
        [DllImport("user32.dll")] static extern IntPtr SetWindowsHookEx(int id, MouseHookCallback callback, IntPtr module, uint thread);
        [DllImport("user32.dll")] static extern bool UnhookWindowsHookEx(IntPtr hook);
        [DllImport("user32.dll")] static extern IntPtr CallNextHookEx(IntPtr hook, int code, IntPtr message, IntPtr data);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] static extern IntPtr GetModuleHandle(string name);
        [DllImport("user32.dll")] static extern IntPtr GetDC(IntPtr handle);
        [DllImport("user32.dll")] static extern int ReleaseDC(IntPtr handle, IntPtr dc);
        [DllImport("gdi32.dll")] static extern IntPtr CreateCompatibleDC(IntPtr dc);
        [DllImport("gdi32.dll")] static extern IntPtr SelectObject(IntPtr dc, IntPtr obj);
        [DllImport("gdi32.dll")] static extern bool DeleteObject(IntPtr obj);
        [DllImport("gdi32.dll")] static extern bool DeleteDC(IntPtr dc);
        [DllImport("user32.dll")] static extern bool UpdateLayeredWindow(IntPtr window, IntPtr screen, ref NativePoint destination, ref NativeSize size, IntPtr source, ref NativePoint origin, uint key, ref Blend blend, uint flags);

        protected override bool ShowWithoutActivation { get { return true; } }
        protected override CreateParams CreateParams {
            get {
                var p = base.CreateParams;
                p.ExStyle |= 0x08000000 | 0x80 | 0x80000;
                return p;
            }
        }

        public Bar() {
            Text = "Antigravity 双额度任务栏";
            AutoScaleMode = AutoScaleMode.None;
            StartPosition = FormStartPosition.Manual;
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            TopMost = true;
            DoubleBuffered = true;
            ClientSize = new Size(60, 60);
            BackColor = Color.FromArgb(25, 32, 40);
            Cursor = Cursors.Hand;
            using (var outline = new GraphicsPath()) {
                outline.AddEllipse(0, 0, 60, 60);
                Region = new Region(outline);
            }
            tip.SetToolTip(this, "Antigravity 双额度圆环\n正在读取额度…");

            LoadTheme();
            var menu = new ContextMenuStrip { Font = new Font("Microsoft YaHei UI", 14, FontStyle.Regular, GraphicsUnit.Pixel) };
            menu.Items.Add("刷新额度", null, (s, e) => Fetch());
            var themeMenu = new ToolStripMenuItem("切换配色");
            themeMenu.DropDownItems.Add("粉色 + 浅卡其色", null, (s, e) => ApplyTheme(Color.FromArgb(249, 136, 179), Color.FromArgb(226, 210, 174)));
            themeMenu.DropDownItems.Add("粉色 + 米白色", null, (s, e) => ApplyTheme(Color.FromArgb(249, 136, 179), Color.FromArgb(245, 236, 215)));
            themeMenu.DropDownItems.Add("暖杏金 + 朱红", null, (s, e) => ApplyTheme(Color.FromArgb(253, 186, 116), Color.FromArgb(239, 68, 68)));
            themeMenu.DropDownItems.Add("蜜桃粉 + 琥珀橙", null, (s, e) => ApplyTheme(Color.FromArgb(251, 113, 133), Color.FromArgb(245, 158, 11)));
            themeMenu.DropDownItems.Add("落日红 + 香槟黄", null, (s, e) => ApplyTheme(Color.FromArgb(244, 63, 94), Color.FromArgb(253, 224, 71)));
            themeMenu.DropDownItems.Add("赤陶橙 + 奶油黄", null, (s, e) => ApplyTheme(Color.FromArgb(234, 88, 12), Color.FromArgb(254, 240, 138)));
            themeMenu.DropDownItems.Add("橘橙 + 暖玫紫", null, (s, e) => ApplyTheme(Color.FromArgb(255, 122, 24), Color.FromArgb(217, 70, 239)));
            themeMenu.DropDownItems.Add(new ToolStripSeparator());
            themeMenu.DropDownItems.Add("自定义外环颜色…", null, (s, e) => PickCustomColor(true));
            themeMenu.DropDownItems.Add("自定义内环颜色…", null, (s, e) => PickCustomColor(false));
            menu.Items.Add(themeMenu);
            menu.Items.Add("安装官方 agy CLI（PowerShell）", null, (s, e) => LaunchInstallAgy());
            menu.Items.Add("打开 agy 登录终端", null, (s, e) => LaunchLoginAgy());
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("关于", null, (s, e) => MessageBox.Show(this,
                "Antigravity 用量圆环\n\n外环：Gemini 5 小时剩余额度\n内环：Gemini 本周剩余额度\n数据源：官方 agy CLI (/usage)\n\n单击展开卡片，双击切换黑白数字，右键可切换配色。",
                "关于 Antigravity 用量圆环", MessageBoxButtons.OK, MessageBoxIcon.Information));
            menu.Items.Add("关闭此圆环", null, (s, e) => Close());
            menu.Items.Add("退出全部三环", null, (s, e) => QuotaRings.Program.ExitAll());
            ContextMenuStrip = menu;

            ringClicks = new RingClicks(TogglePanel, () => { blackDigits = !blackDigits; Render(); });
            fullscreen = new FullscreenVisibility(this, FullscreenChanged);

            refresh = new System.Windows.Forms.Timer { Interval = 60000 };
            refresh.Tick += (s, e) => Fetch();
            refresh.Start();

            position = new System.Windows.Forms.Timer { Interval = 1000 };
            position.Tick += (s, e) => { Place(); UpdateStatus(); Render(); };
            position.Start();

            Shown += (s, e) => {
                fullscreen.Start();
                Place();
                Render();
                Fetch();
                mouseCallback = OnMouse;
                mouseHook = SetWindowsHookEx(14, mouseCallback, GetModuleHandle(null), 0);
            };
            MouseEnter += (s, e) => {
                if (snapshot == null || DateTime.Now - fetched > TimeSpan.FromSeconds(15)) Fetch();
            };
            FormClosed += (s, e) => {
                closing = true;
                ringClicks.Dispose();
                fullscreen.Dispose();
                if (card != null) card.Dispose();
                if (mouseHook != IntPtr.Zero) UnhookWindowsHookEx(mouseHook);
                refresh.Dispose();
                position.Dispose();
                tip.Dispose();
            };
        }

        protected override void WndProc(ref Message m) {
            if (m.Msg == 0x21) { m.Result = new IntPtr(3); return; }
            if (m.Msg == 0x202) {
                long bits = m.LParam.ToInt64();
                var p = new Point((short)(bits & 65535), (short)((bits >> 16) & 65535));
                if (ClientRectangle.Contains(p)) BeginInvoke(new Action(() => HandleRingClick(PointToScreen(p))));
                m.Result = IntPtr.Zero;
                return;
            }
            base.WndProc(ref m);
        }

        void FullscreenChanged(bool hidden) {
            fullscreenHidden = hidden;
            if (hidden) {
                ringClicks.Cancel();
                ringPressed = ringRightPressed = false;
                ContextMenuStrip.Close();
                if (card != null) card.Hide();
                ShowWindow(Handle, 0);
            } else if (!closing && IsHandleCreated) {
                BeginInvoke(new Action(() => {
                    if (!closing && !fullscreenHidden) { Place(); Render(); }
                }));
            }
        }

        void Place() {
            if (fullscreen != null && fullscreen.Refresh()) return;
            var task = FindWindow("Shell_TrayWnd", null);
            Rect t, n, neighborRect;
            int x, y;
            if (task != IntPtr.Zero && GetWindowRect(task, out t)) {
                var tray = FindWindowEx(task, IntPtr.Zero, "TrayNotifyWnd", null);
                int right = GetWindowRect(tray, out n) ? n.Left - 10 : t.Right - 300;
                // Dock cleanly to the left of DeepSeek ring, or Codex ring if DeepSeek is not running
                IntPtr leftMost = FindWindow(null, "DeepSeek 余额任务栏");
                if (leftMost == IntPtr.Zero || !IsWindowVisible(leftMost)) leftMost = FindWindow(null, "Codex 双额度任务栏");
                if (leftMost != IntPtr.Zero && IsWindowVisible(leftMost) && GetWindowRect(leftMost, out neighborRect)) {
                    right = neighborRect.Left - 8;
                }
                x = Math.Max(t.Left + 10, right - Width);
                y = t.Top + Math.Max(0, (t.Bottom - t.Top - Height) / 2);
            } else {
                var r = Screen.PrimaryScreen.WorkingArea;
                x = r.Right - Width - 12;
                y = r.Bottom - Height - 8;
            }
            SetWindowPos(Handle, new IntPtr(-1), x, y, 0, 0, (uint)(Left == x && Top == y ? 0x53 : 0x51));
        }

        void UpdateStatus() {
            DateTimeOffset now = DateTimeOffset.Now;
            bool valid = snapshot != null && snapshot.IsCurrent(now);
            var primary = snapshot != null ? snapshot.Primary : null;
            var secondary = snapshot != null ? snapshot.Secondary : null;
            string state = error != null ? error : busy ? "正在刷新" : valid ? "已更新" : "等待读取";

            string text = "Antigravity 双额度圆环\n"
                + "外环：" + (primary != null ? primary.Name + " " + primary.Percent : "未知") + "\n"
                + "内环：" + (secondary != null ? secondary.Name + " " + secondary.Percent : "未知") + "\n"
                + state + (snapshot != null ? "；上次成功 " + fetched.ToString("HH:mm:ss") : "") + "\n"
                + "单击展开详情；双击切换黑白数字；右键打开菜单";

            if (text != statusText) {
                statusText = text;
                tip.SetToolTip(this, text);
            }
            if (card != null && card.Visible) card.UpdateQuota(snapshot, busy, error);
        }

        async void Fetch() {
            if (busy || closing) return;
            busy = true;
            refresh.Stop();
            UpdateStatus();
            try {
                var result = await Task.Run(() => AntigravityClient.Read());
                if (closing) return;
                snapshot = result;
                fetched = result.Fetched.LocalDateTime;
                error = null;
            } catch (Exception ex) {
                if (!closing) error = ex.Message;
            } finally {
                busy = false;
                if (!closing) {
                    UpdateStatus();
                    Render();
                    refresh.Interval = error == null ? 60000 : 30000;
                    refresh.Start();
                }
            }
        }

        protected override void OnPaint(PaintEventArgs e) { Render(); }

        void Render() {
            if (!IsHandleCreated || closing || fullscreenHidden) return;
            DateTimeOffset now = DateTimeOffset.Now;
            bool valid = snapshot != null && snapshot.IsCurrent(now);
            var primary = snapshot != null ? snapshot.Primary : null;
            var secondary = snapshot != null ? snapshot.Secondary : null;

            using (var bitmap = new Bitmap(Width, Height, System.Drawing.Imaging.PixelFormat.Format32bppPArgb)) {
                using (var g = Graphics.FromImage(bitmap)) {
                    g.Clear(Color.Transparent);
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    using (var hitSurface = new SolidBrush(Color.FromArgb(1, 0, 0, 0))) g.FillEllipse(hitSurface, 0, 0, Width, Height);
                    g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;

                    DrawRing(g, new RectangleF(5, 5, 50, 50), 5, valid && primary != null ? primary.Remaining : (double?)null, outerColor);
                    DrawRing(g, new RectangleF(14, 14, 32, 32), 4.5f, valid && secondary != null ? secondary.Remaining : (double?)null, innerColor);
                    DrawPercentage(g, valid ? primary : null);

                    if (error != null || (snapshot != null && !valid)) {
                        using (var warning = new SolidBrush(Color.FromArgb(244, 183, 94))) g.FillEllipse(warning, 47, 5, 6, 6);
                    }
                }
                var screen = GetDC(IntPtr.Zero);
                var memory = CreateCompatibleDC(screen);
                var image = bitmap.GetHbitmap(Color.FromArgb(0));
                var old = SelectObject(memory, image);
                try {
                    var destination = new NativePoint(Left, Top);
                    var origin = new NativePoint(0, 0);
                    var size = new NativeSize(Width, Height);
                    var blend = new Blend { Alpha = 255, Format = 1 };
                    UpdateLayeredWindow(Handle, screen, ref destination, ref size, memory, ref origin, 0, ref blend, 2);
                } finally {
                    SelectObject(memory, old);
                    DeleteObject(image);
                    DeleteDC(memory);
                    ReleaseDC(IntPtr.Zero, screen);
                }
            }
        }

        void DrawPercentage(Graphics g, QuotaPool primary) {
            string value = primary != null && primary.Remaining.HasValue ? Math.Round(primary.Remaining.Value, MidpointRounding.AwayFromZero).ToString("0") : "--";
            bool stale = error != null || primary == null || !primary.Remaining.HasValue;
            using (var format = new StringFormat(StringFormat.GenericTypographic)) {
                format.Alignment = StringAlignment.Center;
                format.LineAlignment = StringAlignment.Center;
                using (var font = new Font("Segoe UI", value.Length > 2 ? 15 : 19, FontStyle.Bold, GraphicsUnit.Pixel))
                using (var brush = new SolidBrush(blackDigits ? Color.Black : stale ? Color.FromArgb(151, 163, 174) : Color.FromArgb(240, 247, 253))) {
                    g.DrawString(value, font, brush, new RectangleF(16, 18, 28, 24), format);
                }
            }
        }

        static void DrawRing(Graphics g, RectangleF bounds, float thickness, double? remaining, Color color) {
            using (var track = new Pen(Color.FromArgb(100, color), thickness)) g.DrawEllipse(track, bounds);
            if (!remaining.HasValue) return;
            float sweep = (float)(Math.Max(0, Math.Min(100, remaining.Value)) * 3.6);
            RingArc.Draw(g, bounds, thickness, sweep, color);
        }

        bool InsideCircle(NativePoint point) {
            if (fullscreenHidden) return false;
            double x = point.X - Left - Width / 2.0, y = point.Y - Top - Height / 2.0;
            return x * x + y * y <= Width * Width / 4.0;
        }

        IntPtr OnMouse(int code, IntPtr message, IntPtr data) {
            if (code >= 0 && !closing && !fullscreenHidden) {
                int kind = message.ToInt32();
                var point = ((MouseEvent)Marshal.PtrToStructure(data, typeof(MouseEvent))).Point;
                if (kind == 0x204 && InsideCircle(point)) { ringRightPressed = true; return new IntPtr(1); }
                if (kind == 0x205 && ringRightPressed) {
                    ringRightPressed = false;
                    if (InsideCircle(point)) try { BeginInvoke(new Action(() => { ringClicks.Cancel(); if (!closing && !fullscreenHidden) { ContextMenuStrip.Show(new Point(point.X, point.Y)); SetForegroundWindow(ContextMenuStrip.Handle); } })); } catch (InvalidOperationException) { }
                    return new IntPtr(1);
                }
                if (kind == 0x201 && InsideCircle(point)) { ringPressed = true; return new IntPtr(1); }
                if (kind == 0x202 && ringPressed) {
                    ringPressed = false;
                    if (InsideCircle(point)) try { BeginInvoke(new Action(() => HandleRingClick(new Point(point.X, point.Y)))); } catch (InvalidOperationException) { }
                    return new IntPtr(1);
                }
                if ((ContextMenuStrip.Visible || (card != null && card.Visible)) && (kind == 0x201 || kind == 0x204 || kind == 0x207)) {
                    try { BeginInvoke(new Action(() => DismissOutside(point))); } catch (InvalidOperationException) { }
                }
            }
            return CallNextHookEx(mouseHook, code, message, data);
        }

        static bool PointInWindow(IntPtr handle, NativePoint point) {
            Rect r;
            return handle != IntPtr.Zero && GetWindowRect(handle, out r) && point.X >= r.Left && point.X < r.Right && point.Y >= r.Top && point.Y < r.Bottom;
        }

        void DismissOutside(NativePoint point) {
            if (closing) return;
            if (card != null && card.Visible) {
                if (PointInWindow(card.Handle, point)) return;
                card.Hide();
            }
            if (ContextMenuStrip.Visible) {
                if (PointInWindow(ContextMenuStrip.Handle, point)) return;
                foreach (ToolStripItem item in ContextMenuStrip.Items) {
                    var mi = item as ToolStripMenuItem;
                    if (mi != null && mi.HasDropDownItems && mi.DropDown.Visible && PointInWindow(mi.DropDown.Handle, point)) return;
                }
                ContextMenuStrip.Close();
            }
        }

        void HandleRingClick(Point screenPoint) {
            if (closing || fullscreenHidden) return;
            if (ContextMenuStrip.Visible) { ringClicks.Cancel(); ContextMenuStrip.Close(); return; }
            ringClicks.Click(screenPoint);
        }

        void TogglePanel() {
            if (fullscreenHidden || closing) return;
            if (ContextMenuStrip.Visible) { ContextMenuStrip.Close(); return; }
            if (card != null && card.Visible) { card.Hide(); return; }

            if (card == null) {
                card = new AntigravityCard(HandleSetupAction);
                card.SetColors(outerColor, innerColor);
            }
            card.UpdateQuota(snapshot, busy, error);
            Rectangle area = Screen.FromRectangle(Bounds).WorkingArea;
            int x = Math.Max(area.Left + 8, Math.Min(Left + (Width - card.Width) / 2, area.Right - card.Width - 8));
            int y = Math.Max(area.Top + 8, Math.Min(Top - card.Height - 8, area.Bottom - card.Height - 8));
            card.Location = new Point(x, y);
            card.Show();
            Fetch();
        }

        void LoadTheme() {
            try {
                if (!File.Exists(ThemeFile)) return;
                string[] parts = File.ReadAllText(ThemeFile).Trim().Split(',');
                if (parts.Length == 2) {
                    outerColor = ColorTranslator.FromHtml(parts[0].Trim());
                    innerColor = ColorTranslator.FromHtml(parts[1].Trim());
                }
            } catch { }
        }

        void ApplyTheme(Color outer, Color inner) {
            outerColor = outer;
            innerColor = inner;
            try {
                Directory.CreateDirectory(Path.GetDirectoryName(ThemeFile));
                File.WriteAllText(ThemeFile, ColorTranslator.ToHtml(outer) + "," + ColorTranslator.ToHtml(inner));
            } catch { }
            if (card != null) card.SetColors(outer, inner);
            Render();
        }

        void PickCustomColor(bool isOuter) {
            using (var dlg = new ColorDialog { FullOpen = true, Color = isOuter ? outerColor : innerColor }) {
                if (dlg.ShowDialog() == DialogResult.OK) {
                    ApplyTheme(isOuter ? dlg.Color : outerColor, isOuter ? innerColor : dlg.Color);
                }
            }
        }

        void HandleSetupAction() {
            if (AntigravityClient.FindAgyExe() == null) LaunchInstallAgy();
            else LaunchLoginAgy();
        }

        static string PowerShellPath() {
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), @"WindowsPowerShell\v1.0\powershell.exe");
        }

        static void LaunchInstallAgy() {
            try {
                Process.Start(new ProcessStartInfo(PowerShellPath(), "-NoExit -ExecutionPolicy Bypass -Command \"irm https://antigravity.google/cli/install.ps1 | iex\"") {
                    UseShellExecute = true,
                    WindowStyle = ProcessWindowStyle.Normal
                });
            } catch (Exception ex) {
                MessageBox.Show("无法打开 PowerShell 安装窗口：\n" + ex.Message + "\n\n你也可以手动在 PowerShell 中运行：\nirm https://antigravity.google/cli/install.ps1 | iex",
                    "Antigravity CLI 安装", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        static void LaunchLoginAgy() {
            try {
                string agy = AntigravityClient.FindAgyExe() ?? "agy";
                Process.Start(new ProcessStartInfo(PowerShellPath(), "-NoExit -ExecutionPolicy Bypass -Command \"& '" + agy.Replace("'", "''") + "'\"") {
                    UseShellExecute = true,
                    WindowStyle = ProcessWindowStyle.Normal
                });
            } catch (Exception ex) {
                MessageBox.Show("无法打开 agy 登录终端：\n" + ex.Message, "Antigravity CLI 登录", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }
    }
}
