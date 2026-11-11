// Controles propios: negro con detalles blancos.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;

namespace Hanafuda
{
    public static class Theme
    {
        public static readonly Color Bg = Color.FromArgb(0, 0, 0);
        public static readonly Color Panel = Color.FromArgb(10, 10, 10);
        public static readonly Color Hover = Color.FromArgb(24, 24, 24);
        public static readonly Color Line = Color.FromArgb(38, 38, 38);
        public static readonly Color Text = Color.White;
        public static readonly Color Dim = Color.FromArgb(150, 150, 150);
        public static readonly Color Faint = Color.FromArgb(80, 80, 80);
        static readonly Dictionary<string, Font> fonts = new Dictionary<string, Font>();

        public static Font F(float size, FontStyle style = FontStyle.Regular, string family = "Segoe UI")
        {
            var key = family + size + style;
            Font f;
            if (!fonts.TryGetValue(key, out f)) { f = new Font(family, size, style, GraphicsUnit.Point); fonts[key] = f; }
            return f;
        }

        public static GraphicsPath Round(Rectangle r, int radius)
        {
            var p = new GraphicsPath();
            int d = radius * 2;
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }

        public static void Draw(Graphics g, string s, Font f, Color c, Rectangle r, TextFormatFlags flags)
        {
            TextRenderer.DrawText(g, s, f, r, c, flags | TextFormatFlags.NoPrefix);
        }

        // Carta hanafuda: rectangulo blanco con luna negra.
        public static void Card(Graphics g, Rectangle r)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using (var path = Round(r, 3)) g.FillPath(Brushes.White, path);
            int d = r.Width * 6 / 10;
            using (var b = new SolidBrush(Bg)) g.FillEllipse(b, r.X + (r.Width - d) / 2, r.Y + r.Height / 5, d, d);
            using (var pen = new Pen(Bg, Math.Max(1, r.Width / 14f)))
                g.DrawLine(pen, r.X + r.Width / 5, r.Bottom - r.Height / 5, r.Right - r.Width / 5, r.Bottom - r.Height / 5);
        }
    }

    public class Ctl : Control
    {
        protected bool hover;
        public Ctl()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            BackColor = Theme.Bg;
            ForeColor = Theme.Text;
        }
        protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hover = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnEnabledChanged(EventArgs e) { Invalidate(); base.OnEnabledChanged(e); }
    }

    public class FlatButton : Ctl
    {
        public bool Primary;
        public FlatButton(string text, int width = 120) { Text = text; Size = new Size(width, 32); Cursor = Cursors.Hand; Font = Theme.F(9f, FontStyle.Bold); }
        protected override void OnTextChanged(EventArgs e) { Invalidate(); base.OnTextChanged(e); }
        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            var r = new Rectangle(0, 0, Width - 1, Height - 1);
            bool fill = Enabled && (Primary ^ hover);
            using (var path = Theme.Round(r, 4))
            {
                if (fill) g.FillPath(Brushes.White, path);
                using (var pen = new Pen(Enabled ? Theme.Text : Theme.Faint)) g.DrawPath(pen, path);
            }
            Theme.Draw(g, Text, Font, !Enabled ? Theme.Faint : fill ? Theme.Bg : Theme.Text, r, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        }
    }

    public class Toggle : Ctl
    {
        string state = "busy";
        public string State { get { return state; } set { state = value; Invalidate(); } }
        public Toggle() { Size = new Size(46, 24); Cursor = Cursors.Hand; }
        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            var r = new Rectangle(1, 2, Width - 3, Height - 5);
            using (var path = Theme.Round(r, r.Height / 2))
            {
                if (state == "on") g.FillPath(Brushes.White, path);
                var border = state == "na" || state == "busy" ? Theme.Faint : hover || state != "off" ? Theme.Text : Theme.Dim;
                using (var pen = new Pen(border, 1.4f)) g.DrawPath(pen, path);
            }
            int k = r.Height - 6;
            int x = state == "on" ? r.Right - k - 3 : state == "partial" ? r.X + (r.Width - k) / 2 : r.X + 3;
            var knob = state == "on" ? Theme.Bg : state == "na" || state == "busy" ? Theme.Faint : Theme.Text;
            if (state == "busy") { using (var p = new Pen(knob, 1.4f)) g.DrawEllipse(p, x, r.Y + 3, k, k); return; }
            using (var b = new SolidBrush(knob)) g.FillEllipse(b, x, r.Y + 3, k, k);
        }
    }

    public class Check : Ctl
    {
        bool on;
        public bool Checked { get { return on; } set { on = value; Invalidate(); } }
        public event EventHandler CheckedChanged;
        public Check() { Size = new Size(20, 20); Cursor = Cursors.Hand; }
        protected override void OnClick(EventArgs e) { Checked = !Checked; if (CheckedChanged != null) CheckedChanged(this, e); base.OnClick(e); }
        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            var r = new Rectangle(2, 2, Width - 5, Height - 5);
            using (var path = Theme.Round(r, 3))
            {
                if (on) g.FillPath(Brushes.White, path);
                using (var pen = new Pen(on || hover ? Theme.Text : Theme.Dim, 1.4f)) g.DrawPath(pen, path);
            }
            if (on)
                using (var pen = new Pen(Theme.Bg, 2f))
                    g.DrawLines(pen, new[] { new Point(r.X + 3, r.Y + r.Height / 2), new Point(r.X + r.Width / 2 - 1, r.Bottom - 4), new Point(r.Right - 3, r.Y + 3) });
        }
    }

    public class NavButton : Ctl
    {
        public string Title, Sub;
        bool selected;
        public bool Selected { get { return selected; } set { selected = value; Invalidate(); } }
        public NavButton(string title, string sub) { Title = title; Sub = sub; Size = new Size(200, 44); Cursor = Cursors.Hand; }
        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            if (selected || hover) using (var b = new SolidBrush(selected ? Theme.Hover : Theme.Panel)) g.FillRectangle(b, ClientRectangle);
            if (selected) g.FillRectangle(Brushes.White, 0, 8, 3, Height - 16);
            Theme.Draw(g, Title, Theme.F(10f, FontStyle.Bold), selected ? Theme.Text : Theme.Dim, new Rectangle(18, 5, Width - 20, 20), TextFormatFlags.Left);
            Theme.Draw(g, Sub, Theme.F(8.25f), selected ? Theme.Dim : Theme.Faint, new Rectangle(18, 23, Width - 20, 16), TextFormatFlags.Left | TextFormatFlags.EndEllipsis);
        }
    }

    public class SectionTitle : Ctl
    {
        public SectionTitle(string text) { Text = text; Height = 40; }
        protected override void OnPaint(PaintEventArgs e)
        {
            Theme.Draw(e.Graphics, Text.ToUpperInvariant(), Theme.F(8.25f, FontStyle.Bold), Theme.Dim, new Rectangle(4, 16, Width, 18), TextFormatFlags.Left);
            using (var pen = new Pen(Theme.Line)) e.Graphics.DrawLine(pen, 4, Height - 2, Width - 4, Height - 2);
        }
    }

    // Fila: texto a la izquierda, control a la derecha (interruptor o boton) y opcional casilla al inicio.
    public class Row : Ctl
    {
        public string Title, Desc;
        public List<string> Tags = new List<string>();
        public new Control Right;
        public Control Lead;
        public object Tag2;
        public Row(string title, string desc, Control right, Control lead = null)
        {
            Title = title; Desc = desc ?? ""; Right = right; Lead = lead; Height = 56;
            if (right != null) Controls.Add(right);
            if (lead != null) Controls.Add(lead);
        }
        protected override void OnLayout(LayoutEventArgs e)
        {
            if (Right != null) Right.Location = new Point(Width - Right.Width - 14, (Height - Right.Height) / 2);
            if (Lead != null) Lead.Location = new Point(10, (Height - Lead.Height) / 2);
            base.OnLayout(e);
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            if (hover) using (var b = new SolidBrush(Theme.Panel)) g.FillRectangle(b, ClientRectangle);
            int x = Lead != null ? 40 : 12;
            int right = (Right != null ? Right.Left - 14 : Width - 12);
            var tf = Theme.F(10f);
            var size = TextRenderer.MeasureText(g, Title, tf, new Size(right - x, 20), TextFormatFlags.NoPadding | TextFormatFlags.EndEllipsis);
            Theme.Draw(g, Title, tf, Theme.Text, new Rectangle(x, 9, right - x, 20), TextFormatFlags.Left | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);
            int tx = x + Math.Min(size.Width, right - x) + 8;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            foreach (var tag in Tags)
            {
                var font = Theme.F(7f, FontStyle.Bold);
                int w = TextRenderer.MeasureText(tag, font).Width + 4;
                if (tx + w > right) break;
                var r = new Rectangle(tx, 11, w, 15);
                using (var path = Theme.Round(r, 3))
                using (var pen = new Pen(tag == "RIESGO" || tag == "SIN DESHACER" ? Theme.Text : Theme.Faint)) g.DrawPath(pen, path);
                Theme.Draw(g, tag, font, tag == "RIESGO" || tag == "SIN DESHACER" ? Theme.Text : Theme.Dim, r, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                tx += w + 5;
            }
            Theme.Draw(g, Desc, Theme.F(8.5f), Theme.Dim, new Rectangle(x, 31, right - x, 18), TextFormatFlags.Left | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);
            using (var pen = new Pen(Theme.Line)) g.DrawLine(pen, 0, Height - 1, Width, Height - 1);
        }
    }

    public class HudTile : Ctl
    {
        public string Title = "", Value = "", Sub = "";
        public float Bar = -1;
        public readonly List<float> History = new List<float>();
        public HudTile(string title) { Title = title; Size = new Size(280, 112); }
        public void Push(float v) { History.Add(v); if (History.Count > 60) History.RemoveAt(0); Invalidate(); }
        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            var r = new Rectangle(0, 0, Width - 1, Height - 1);
            using (var path = Theme.Round(r, 6))
            {
                using (var b = new SolidBrush(Theme.Panel)) g.FillPath(b, path);
                using (var pen = new Pen(Theme.Line)) g.DrawPath(pen, path);
            }
            Theme.Draw(g, Title.ToUpperInvariant(), Theme.F(7.5f, FontStyle.Bold), Theme.Dim, new Rectangle(14, 10, Width - 28, 14), TextFormatFlags.Left);
            Theme.Draw(g, Value, Theme.F(18f, FontStyle.Bold), Theme.Text, new Rectangle(12, 26, Width - 24, 34), TextFormatFlags.Left | TextFormatFlags.EndEllipsis);
            Theme.Draw(g, Sub, Theme.F(8.25f), Theme.Dim, new Rectangle(14, 62, Width - 28, 16), TextFormatFlags.Left | TextFormatFlags.EndEllipsis);
            var area = new Rectangle(14, 82, Width - 28, 20);
            if (History.Count > 1)
            {
                var pts = History.Select((v, i) => new PointF(area.Right - (History.Count - 1 - i) * (area.Width / 59f), area.Bottom - v / 100f * area.Height)).ToArray();
                using (var pen = new Pen(Theme.Text, 1.3f)) g.DrawLines(pen, pts);
            }
            else if (Bar >= 0)
            {
                var track = new Rectangle(area.X, area.Y + 8, area.Width, 6);
                using (var b = new SolidBrush(Theme.Line)) g.FillRectangle(b, track);
                g.FillRectangle(Brushes.White, track.X, track.Y, (int)(track.Width * Math.Min(1f, Bar / 100f)), track.Height);
            }
        }
    }

    public class ConfirmDialog : Form
    {
        public ConfirmDialog(string title, string text, string ok)
        {
            FormBorderStyle = FormBorderStyle.None; StartPosition = FormStartPosition.CenterParent; ShowInTaskbar = false;
            BackColor = Theme.Bg; Size = new Size(480, 210); KeyPreview = true;
            var yes = new FlatButton(ok, 130) { Primary = true, Location = new Point(Width - 290, Height - 56) };
            var no = new FlatButton("Cancelar", 130) { Location = new Point(Width - 150, Height - 56) };
            yes.Click += delegate { DialogResult = DialogResult.OK; };
            no.Click += delegate { DialogResult = DialogResult.Cancel; };
            Controls.Add(yes); Controls.Add(no);
            Paint += (s, e) =>
            {
                using (var pen = new Pen(Theme.Text)) e.Graphics.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
                Theme.Draw(e.Graphics, title, Theme.F(13f, FontStyle.Bold), Theme.Text, new Rectangle(22, 20, Width - 44, 28), TextFormatFlags.Left | TextFormatFlags.EndEllipsis);
                Theme.Draw(e.Graphics, text, Theme.F(9.5f), Theme.Dim, new Rectangle(22, 58, Width - 44, 80), TextFormatFlags.Left | TextFormatFlags.WordBreak);
            };
            KeyDown += (s, e) => { if (e.KeyCode == Keys.Escape) DialogResult = DialogResult.Cancel; };
        }

        public static bool Ask(IWin32Window owner, string title, string text, string ok = "Confirmar")
        {
            using (var d = new ConfirmDialog(title, text, ok)) return d.ShowDialog(owner) == DialogResult.OK;
        }
    }
}
