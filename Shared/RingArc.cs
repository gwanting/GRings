using System;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace QuotaTray {
    static class RingArc {
        public static void Draw(Graphics graphics, RectangleF bounds, float width, float sweep, Color color) {
            if (float.IsNaN(sweep) || sweep <= 0) return;

            using (var pen = new Pen(color, width)) {
                if (sweep >= 359.99f) {
                    graphics.DrawEllipse(pen, bounds);
                    return;
                }

                var center = new PointF(bounds.Left + bounds.Width / 2, bounds.Top + bounds.Height / 2);
                int segments = (int)Math.Ceiling(sweep / 4);
                var points = new PointF[segments + 1];
                for (int i = 0; i <= segments; i++) {
                    double angle = (sweep * i / segments - 90) * Math.PI / 180;
                    points[i] = new PointF(center.X + bounds.Width / 2 * (float)Math.Cos(angle),
                        center.Y + bounds.Height / 2 * (float)Math.Sin(angle));
                }

                if (segments == 1 && points[0] == points[1]) {
                    using (var brush = new SolidBrush(color))
                        graphics.FillEllipse(brush, center.X - width / 2, bounds.Top - width / 2, width, width);
                    return;
                }

                pen.StartCap = pen.EndCap = LineCap.Round;
                pen.LineJoin = LineJoin.Round;
                graphics.DrawLines(pen, points);
            }
        }
    }
}
