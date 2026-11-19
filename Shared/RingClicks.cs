using System;
using System.Drawing;
using System.Windows.Forms;

namespace QuotaTray {
    sealed class RingClicks : IDisposable {
        readonly Timer timer = new Timer();
        readonly Action singleClick;
        readonly Action doubleClick;
        Point firstClick;
        int firstClickTime;

        public RingClicks(Action singleClick, Action doubleClick) {
            this.singleClick = singleClick;
            this.doubleClick = doubleClick;
            timer.Tick += (sender, args) => CommitSingleClick();
        }

        public void Click(Point point) {
            int now = Environment.TickCount;
            Size distance = SystemInformation.DoubleClickSize;
            if (timer.Enabled && unchecked((uint)(now - firstClickTime)) <= timer.Interval &&
                Math.Abs(point.X - firstClick.X) <= distance.Width / 2 &&
                Math.Abs(point.Y - firstClick.Y) <= distance.Height / 2) {
                Cancel();
                doubleClick();
                return;
            }

            if (timer.Enabled) CommitSingleClick();
            firstClick = point;
            firstClickTime = now;
            timer.Interval = SystemInformation.DoubleClickTime;
            timer.Start();
        }

        void CommitSingleClick() {
            Cancel();
            singleClick();
        }

        public void Cancel() { timer.Stop(); }
        public void Dispose() { timer.Dispose(); }
    }
}
