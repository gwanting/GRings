using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

[assembly: System.Reflection.AssemblyTitle("GRings")]
[assembly: System.Reflection.AssemblyDescription("圆环设计与制作：Wanting")]
[assembly: System.Reflection.AssemblyCompany("Wanting")]
[assembly: System.Reflection.AssemblyProduct("GRings")]
[assembly: System.Reflection.AssemblyCopyright("Copyright © 2026 Wanting")]

namespace QuotaRings {
    static class Program {
        [DllImport("user32.dll")] static extern bool SetProcessDpiAwarenessContext(IntPtr context);
        static QuotaRingsContext activeContext;

        [STAThread]
        static void Main() {
            try { SetProcessDpiAwarenessContext(new IntPtr(-4)); } catch { }
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            bool fresh;
            using (var mutex = new Mutex(true, "Local\\Wanting.QuotaRings", out fresh)) {
                if (!fresh) return;
                activeContext = new QuotaRingsContext();
                Application.Run(activeContext);
            }
        }

        public static void ExitAll() {
            if (activeContext != null) activeContext.CloseAll();
        }
    }

    sealed class QuotaRingsContext : ApplicationContext {
        readonly Form[] rings;
        int openCount;
        bool exiting;

        public QuotaRingsContext() {
            var codex = new DualQuotaBar.Bar();
            var deepSeek = new DeepSeekBalance.Bar();
            var antigravity = new AntigravityQuotaBar.Bar();
            rings = new Form[] { codex, deepSeek, antigravity };
            openCount = rings.Length;

            foreach (var ring in rings) {
                ring.FormClosed += OnRingClosed;
                ring.Show();
            }
        }

        void OnRingClosed(object sender, FormClosedEventArgs e) {
            openCount--;
            if (openCount <= 0 && !exiting) {
                exiting = true;
                ExitThread();
            }
        }

        public void CloseAll() {
            if (exiting) return;
            exiting = true;
            foreach (var ring in rings) {
                try {
                    if (ring != null && !ring.IsDisposed) ring.Close();
                } catch { }
            }
            ExitThread();
        }
    }
}
