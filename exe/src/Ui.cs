// Controles propios: minimalista, negro con degradados hacia el blanco.
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
        public static readonly Color Panel = Color.FromArgb(12, 12, 12);
        public static readonly Color Hover = Color.FromArgb(24, 24, 24);
        public static readonly Color Line = Color.FromArgb(34, 34, 34);
        public static readonly Color Text = Color.White;
        public static readonly Color Dim = Color.FromArgb(150, 150, 150);
        public static readonly Color Faint = Color.FromArgb(85, 85, 85);
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
            int d = Math.Max(1, Math.Min(radius * 2, Math.Min(r.Width, r.Height)));
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

        public static Color Mix(Color a, Color b, float t)
        {
            t = Math.Max(0, Math.Min(1, t));
            return Color.FromArgb((int)(a.A + (b.A - a.A) * t), (int)(a.R + (b.R - a.R) * t), (int)(a.G + (b.G - a.G) * t), (int)(a.B + (b.B - a.B) * t));
        }

        // Linea fina que pasa de negro a blanco y vuelve a negro.
        public static void AccentLine(Graphics g, int x, int y, int width)
        {
            if (width < 4) return;
            using (var b = new LinearGradientBrush(new Rectangle(x, y, width, 1), Color.Black, Color.Black, 0f))
            {
                b.InterpolationColors = new ColorBlend { Colors = new[] { Color.FromArgb(0, 0, 0), Color.FromArgb(235, 235, 235), Color.FromArgb(0, 0, 0) }, Positions = new[] { 0f, 0.5f, 1f } };
                g.FillRectangle(b, x, y, width, 1);
            }
        }

        // Carta hanafuda: rectangulo blanco con luna negra.
        public static void Card(Graphics g, Rectangle r)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using (var path = Round(r, 3))
            using (var b = new LinearGradientBrush(r, Color.White, Color.FromArgb(200, 200, 200), 90f)) g.FillPath(b, path);
            int d = r.Width * 6 / 10;
            using (var b = new SolidBrush(Bg)) g.FillEllipse(b, r.X + (r.Width - d) / 2, r.Y + r.Height / 5, d, d);
            using (var pen = new Pen(Bg, Math.Max(1, r.Width / 14f)))
                g.DrawLine(pen, r.X + r.Width / 5, r.Bottom - r.Height / 5, r.Right - r.Width / 5, r.Bottom - r.Height / 5);
        }
    }

    // Un contenedor con degradado pinta tambien el fondo de sus controles hijos.
    public interface IGradientHost { void PaintChildBackground(Graphics g, Control child); }

    public class Ctl : Control
    {
        protected bool hover;
        public Ctl()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            BackColor = Theme.Bg;
            ForeColor = Theme.Text;
        }
        protected override void OnPaintBackground(PaintEventArgs e)
        {
            var host = Parent as IGradientHost;
            if (host != null) host.PaintChildBackground(e.Graphics, this);
            else base.OnPaintBackground(e);
        }
        protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hover = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnEnabledChanged(EventArgs e) { Invalidate(); base.OnEnabledChanged(e); }
    }

    public class GradientCtl : Ctl, IGradientHost
    {
        public Color From = Color.FromArgb(0, 0, 0), To = Color.FromArgb(40, 40, 40);
        public float Angle = 0f;
        public GradientCtl(Color from, Color to, float angle) { From = from; To = to; Angle = angle; }

        void Fill(Graphics g, Rectangle area)
        {
            if (area.Width <= 0 || area.Height <= 0) return;
            using (var b = new LinearGradientBrush(area, From, To, Angle)) g.FillRectangle(b, area);
        }
        protected override void OnPaintBackground(PaintEventArgs e) { Fill(e.Graphics, ClientRectangle); }
        public void PaintChildBackground(Graphics g, Control child)
        {
            var state = g.Save();
            g.TranslateTransform(-child.Left, -child.Top);
            Fill(g, ClientRectangle);
            g.Restore(state);
        }
        protected override void OnResize(EventArgs e) { base.OnResize(e); foreach (Control c in Controls) c.Invalidate(); }
    }

    public class FlatButton : Ctl
    {
        public bool Primary;
        public FlatButton(string text, int width = 120) { Text = text; Size = new Size(width, 34); Cursor = Cursors.Hand; Font = Theme.F(9f, FontStyle.Bold); }
        protected override void OnTextChanged(EventArgs e) { Invalidate(); base.OnTextChanged(e); }
        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            var r = new Rectangle(0, 0, Width - 1, Height - 1);
            bool fill = Enabled && (Primary || hover);
            using (var path = Theme.Round(r, Height / 2))
            {
                if (fill)
                {
                    var top = Primary && hover ? Color.FromArgb(225, 225, 225) : Color.White;
                    var bottom = Primary && hover ? Color.FromArgb(170, 170, 170) : Color.FromArgb(205, 205, 205);
                    using (var b = new LinearGradientBrush(r, top, bottom, 90f)) g.FillPath(b, path);
                }
                else using (var pen = new Pen(Enabled ? Color.FromArgb(120, 120, 120) : Theme.Faint)) g.DrawPath(pen, path);
            }
            Theme.Draw(g, Text, Font, !Enabled ? Theme.Faint : fill ? Theme.Bg : Theme.Text, r, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        }
    }

    // Interruptor estilo iOS con animacion. Estados: on, off, partial, na, busy.
    public class Toggle : Ctl
    {
        string state = "busy";
        float pos, target;
        readonly Timer anim = new Timer { Interval = 15 };
        static readonly Color TrackOff = Color.FromArgb(48, 48, 52);
        static readonly Color TrackIdle = Color.FromArgb(26, 26, 28);

        public Toggle()
        {
            Size = new Size(52, 30);
            Cursor = Cursors.Hand;
            anim.Tick += delegate
            {
                float step = 0.14f;
                if (Math.Abs(target - pos) <= step) { pos = target; anim.Stop(); }
                else pos += target > pos ? step : -step;
                Invalidate();
            };
        }

        public string State
        {
            get { return state; }
            set
            {
                state = value;
                target = value == "on" ? 1f : value == "partial" ? 0.5f : 0f;
                if (!IsHandleCreated || !Visible) { pos = target; Invalidate(); }
                else anim.Start();
            }
        }

        protected override void Dispose(bool disposing) { if (disposing) anim.Dispose(); base.Dispose(disposing); }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            var r = new Rectangle(1, 1, Width - 3, Height - 3);
            bool idle = state == "na" || state == "busy";
            using (var path = Theme.Round(r, r.Height / 2))
            {
                if (idle) using (var b = new SolidBrush(TrackIdle)) g.FillPath(b, path);
                else
                {
                    var top = Theme.Mix(TrackOff, Color.White, pos);
                    var bottom = Theme.Mix(TrackOff, Color.FromArgb(196, 196, 196), pos);
                    using (var b = new LinearGradientBrush(r, top, bottom, 90f)) g.FillPath(b, path);
                }
                if (hover && !idle) using (var pen = new Pen(Color.FromArgb(150, 255, 255, 255))) g.DrawPath(pen, path);
            }
            int d = r.Height - 4;
            float x = r.X + 2 + pos * (r.Width - d - 4);
            float y = r.Y + 2;
            if (state == "busy")
            {
                using (var pen = new Pen(Theme.Faint, 1.4f)) g.DrawEllipse(pen, x + 2, y + 2, d - 4, d - 4);
                return;
            }
            using (var shadow = new SolidBrush(Color.FromArgb(110, 0, 0, 0))) g.FillEllipse(shadow, x, y + 1.5f, d, d);
            var knob = state == "na" ? Color.FromArgb(70, 70, 70) : Theme.Mix(Color.White, Color.FromArgb(14, 14, 14), pos);
            using (var b = new SolidBrush(knob)) g.FillEllipse(b, x, y, d, d);
        }
    }

    public class Check : Ctl
    {
        bool on;
        public bool Checked { get { return on; } set { on = value; Invalidate(); } }
        public event EventHandler CheckedChanged;
        public Check() { Size = new Size(22, 22); Cursor = Cursors.Hand; }
        protected override void OnClick(EventArgs e) { Checked = !Checked; if (CheckedChanged != null) CheckedChanged(this, e); base.OnClick(e); }
        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            var r = new Rectangle(2, 2, Width - 5, Height - 5);
            using (var path = Theme.Round(r, r.Height / 2))
            {
                if (on) using (var b = new LinearGradientBrush(r, Color.White, Color.FromArgb(200, 200, 200), 90f)) g.FillPath(b, path);
                using (var pen = new Pen(on || hover ? Theme.Text : Theme.Dim, 1.4f)) g.DrawPath(pen, path);
            }
            if (on)
                using (var pen = new Pen(Theme.Bg, 2f))
                    g.DrawLines(pen, new[] { new Point(r.X + 4, r.Y + r.Height / 2), new Point(r.X + r.Width / 2 - 1, r.Bottom - 5), new Point(r.Right - 4, r.Y + 5) });
        }
    }

    public class NavButton : Ctl
    {
        public string Title, Sub;
        bool selected;
        public bool Selected { get { return selected; } set { selected = value; Invalidate(); } }
        public NavButton(string title, string sub) { Title = title; Sub = sub; Size = new Size(200, 42); Cursor = Cursors.Hand; }
        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            var r = new Rectangle(0, 2, Width - 1, Height - 5);
            if (selected || hover)
                using (var path = Theme.Round(r, 8))
                using (var b = new LinearGradientBrush(r, Color.FromArgb(selected ? 46 : 20, 255, 255, 255), Color.FromArgb(0, 255, 255, 255), 0f))
                    g.FillPath(b, path);
            if (selected) using (var path = Theme.Round(new Rectangle(0, 10, 3, Height - 21), 1)) g.FillPath(Brushes.White, path);
            Theme.Draw(g, Title, Theme.F(10f, FontStyle.Bold), selected ? Theme.Text : Color.FromArgb(205, 205, 205), new Rectangle(16, 5, Width - 20, 20), TextFormatFlags.Left);
            Theme.Draw(g, Sub, Theme.F(8.25f), selected ? Color.FromArgb(185, 185, 185) : Color.FromArgb(125, 125, 125), new Rectangle(16, 23, Width - 20, 16), TextFormatFlags.Left | TextFormatFlags.EndEllipsis);
        }
    }

    public class SectionTitle : Ctl
    {
        public SectionTitle(string text) { Text = text; Height = 42; }
        protected override void OnPaint(PaintEventArgs e)
        {
            Theme.Draw(e.Graphics, Text.ToUpperInvariant(), Theme.F(8f, FontStyle.Bold), Theme.Dim, new Rectangle(4, 18, Width, 18), TextFormatFlags.Left);
        }
    }

    // Tarjeta de un juego en la Biblioteca: icono del juego instalado o una placa con sus iniciales.
    public class GameTile : Ctl
    {
        public Bitmap Icon;
        public string GameName = "", Short = "", Status = "Buscando…", Sub = "";
        public bool Installed;
        public GameTile() { Size = new Size(280, 196); Cursor = Cursors.Hand; }

        public static void Monogram(Graphics g, Rectangle r, string text, bool dim)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using (var path = Theme.Round(r, r.Width / 5))
            using (var b = new LinearGradientBrush(r, dim ? Color.FromArgb(70, 70, 70) : Color.White, dim ? Color.FromArgb(35, 35, 35) : Color.FromArgb(185, 185, 185), 90f))
                g.FillPath(b, path);
            float size = r.Height * (text.Length > 2 ? 0.27f : 0.36f);
            Theme.Draw(g, text, Theme.F(size, FontStyle.Bold), dim ? Color.FromArgb(150, 150, 150) : Theme.Bg, r, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            var r = new Rectangle(0, 0, Width - 1, Height - 1);
            using (var path = Theme.Round(r, 16))
            {
                using (var b = new LinearGradientBrush(r, hover ? Color.FromArgb(44, 44, 44) : Color.FromArgb(28, 28, 28), Color.FromArgb(4, 4, 4), 90f)) g.FillPath(b, path);
                using (var pen = new Pen(hover ? Color.FromArgb(220, 220, 220) : Color.FromArgb(48, 48, 48), hover ? 1.4f : 1f)) g.DrawPath(pen, path);
            }
            var iconRect = new Rectangle((Width - 84) / 2, 22, 84, 84);
            if (Icon != null) g.DrawImage(Icon, iconRect);
            else Monogram(g, iconRect, Short, !Installed && Status != "Buscando…");
            Theme.Draw(g, GameName, Theme.F(11.5f, FontStyle.Bold), Theme.Text, new Rectangle(8, 116, Width - 16, 24), TextFormatFlags.HorizontalCenter | TextFormatFlags.EndEllipsis);
            Theme.Draw(g, Status, Theme.F(8.5f, FontStyle.Bold), Installed ? Color.FromArgb(225, 225, 225) : Theme.Faint, new Rectangle(8, 142, Width - 16, 18), TextFormatFlags.HorizontalCenter | TextFormatFlags.EndEllipsis);
            Theme.Draw(g, Sub, Theme.F(8f), Theme.Dim, new Rectangle(12, 162, Width - 24, 18), TextFormatFlags.HorizontalCenter | TextFormatFlags.EndEllipsis);
        }
    }

    // Pastilla de estado de un servicio: blanca si esta bien, con borde si no.
    public class StatusPill : Ctl
    {
        string label = "…";
        bool good;
        public StatusPill() { Size = new Size(104, 26); }

        // Pastilla con un valor recomendado; el ancho se ajusta al texto.
        public static StatusPill Value(string text)
        {
            var p = new StatusPill();
            p.Width = Math.Min(230, Math.Max(80, TextRenderer.MeasureText(text, Theme.F(7.5f, FontStyle.Bold)).Width + 26));
            p.Set(text, false);
            return p;
        }
        public void Set(string text, bool ok) { label = text; good = ok; Invalidate(); }
        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            var r = new Rectangle(1, 1, Width - 3, Height - 3);
            using (var path = Theme.Round(r, r.Height / 2))
            {
                if (good) using (var b = new LinearGradientBrush(r, Color.White, Color.FromArgb(200, 200, 200), 90f)) g.FillPath(b, path);
                else using (var pen = new Pen(label == "…" ? Theme.Faint : Color.FromArgb(200, 200, 200), 1.3f)) g.DrawPath(pen, path);
            }
            Theme.Draw(g, label, Theme.F(7.5f, FontStyle.Bold), good ? Theme.Bg : Theme.Text, r, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        }
    }

    // Boton ? que abre la explicacion detallada de una opcion.
    public class HelpButton : Ctl
    {
        readonly string title, text;
        public HelpButton(string title, string text) { this.title = title; this.text = text; Size = new Size(24, 24); Cursor = Cursors.Help; }
        protected override void OnClick(EventArgs e) { HelpDialog.Open(FindForm(), title, text); base.OnClick(e); }
        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            var r = new Rectangle(1, 1, Width - 3, Height - 3);
            if (hover) g.FillEllipse(Brushes.White, r);
            else using (var pen = new Pen(Color.FromArgb(110, 110, 110), 1.2f)) g.DrawEllipse(pen, r);
            Theme.Draw(g, "?", Theme.F(9f, FontStyle.Bold), hover ? Theme.Bg : Color.FromArgb(170, 170, 170), new Rectangle(0, 0, Width, Height - 1), TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        }
    }

    // Fila: texto a la izquierda; a la derecha boton ? y control (interruptor o boton); casilla opcional al inicio.
    public class Row : Ctl
    {
        public string Title, Desc;
        public List<string> Tags = new List<string>();
        public new Control Right;
        public Control Lead;
        public HelpButton Help;
        public Row(string title, string desc, Control right, Control lead = null, string help = null)
        {
            Title = title; Desc = desc ?? ""; Right = right; Lead = lead; Height = 58;
            if (right != null) Controls.Add(right);
            if (lead != null) Controls.Add(lead);
            if (!string.IsNullOrEmpty(help)) { Help = new HelpButton(title, help); Controls.Add(Help); }
        }
        protected override void OnLayout(LayoutEventArgs e)
        {
            int edge = Width - 14;
            if (Right != null) { Right.Location = new Point(edge - Right.Width, (Height - Right.Height) / 2); edge = Right.Left - 12; }
            if (Help != null) Help.Location = new Point(edge - Help.Width, (Height - Help.Height) / 2);
            if (Lead != null) Lead.Location = new Point(10, (Height - Lead.Height) / 2);
            base.OnLayout(e);
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            if (hover) using (var b = new SolidBrush(Theme.Panel)) g.FillRectangle(b, ClientRectangle);
            int x = Lead != null ? 42 : 12;
            int right = Help != null ? Help.Left - 12 : Right != null ? Right.Left - 14 : Width - 12;
            var tf = Theme.F(10f);
            var size = TextRenderer.MeasureText(g, Title, tf, new Size(right - x, 20), TextFormatFlags.NoPadding | TextFormatFlags.EndEllipsis);
            Theme.Draw(g, Title, tf, Theme.Text, new Rectangle(x, 10, right - x, 20), TextFormatFlags.Left | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);
            int tx = x + Math.Min(size.Width, right - x) + 8;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            foreach (var tag in Tags)
            {
                var font = Theme.F(6.75f, FontStyle.Bold);
                int w = TextRenderer.MeasureText(tag, font).Width + 6;
                if (tx + w > right) break;
                var r = new Rectangle(tx, 12, w, 15);
                bool strong = tag == "RIESGO" || tag == "SIN DESHACER" || tag == "ROMPE FIVEM";
                using (var path = Theme.Round(r, 7))
                {
                    if (strong) g.FillPath(Brushes.White, path);
                    else using (var pen = new Pen(Color.FromArgb(70, 70, 70))) g.DrawPath(pen, path);
                }
                Theme.Draw(g, tag, font, strong ? Theme.Bg : Theme.Dim, r, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                tx += w + 5;
            }
            Theme.Draw(g, Desc, Theme.F(8.5f), Theme.Dim, new Rectangle(x, 33, right - x, 18), TextFormatFlags.Left | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);
            using (var pen = new Pen(Theme.Line)) g.DrawLine(pen, x, Height - 1, Width, Height - 1);
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
            using (var path = Theme.Round(r, 14))
            {
                using (var b = new LinearGradientBrush(r, Color.FromArgb(30, 30, 30), Color.FromArgb(6, 6, 6), 60f)) g.FillPath(b, path);
                using (var pen = new Pen(Color.FromArgb(44, 44, 44))) g.DrawPath(pen, path);
            }
            Theme.Draw(g, Title.ToUpperInvariant(), Theme.F(7.5f, FontStyle.Bold), Theme.Dim, new Rectangle(16, 12, Width - 32, 14), TextFormatFlags.Left);
            Theme.Draw(g, Value, Theme.F(18f, FontStyle.Bold), Theme.Text, new Rectangle(14, 28, Width - 28, 34), TextFormatFlags.Left | TextFormatFlags.EndEllipsis);
            Theme.Draw(g, Sub, Theme.F(8.25f), Theme.Dim, new Rectangle(16, 62, Width - 32, 16), TextFormatFlags.Left | TextFormatFlags.EndEllipsis);
            var area = new Rectangle(16, 82, Width - 32, 20);
            if (History.Count > 1)
            {
                var pts = History.Select((v, i) => new PointF(area.Right - (History.Count - 1 - i) * (area.Width / 59f), area.Bottom - v / 100f * area.Height)).ToArray();
                using (var pen = new Pen(Theme.Text, 1.4f)) g.DrawLines(pen, pts);
            }
            else if (Bar >= 0)
            {
                var track = new Rectangle(area.X, area.Y + 8, area.Width, 6);
                using (var path = Theme.Round(track, 3)) using (var b = new SolidBrush(Color.FromArgb(40, 40, 40))) g.FillPath(b, path);
                var fill = new Rectangle(track.X, track.Y, Math.Max(6, (int)(track.Width * Math.Min(1f, Bar / 100f))), track.Height);
                using (var path = Theme.Round(fill, 3)) using (var b = new LinearGradientBrush(fill, Color.FromArgb(150, 150, 150), Color.White, 0f)) g.FillPath(b, path);
            }
        }
    }

    public class ConfirmDialog : Form
    {
        public ConfirmDialog(string title, string text, string ok)
        {
            FormBorderStyle = FormBorderStyle.None; StartPosition = FormStartPosition.CenterParent; ShowInTaskbar = false;
            BackColor = Theme.Bg; Size = new Size(480, 210); KeyPreview = true;
            var yes = new FlatButton(ok, 130) { Primary = true, Location = new Point(Width - 290, Height - 58) };
            var no = new FlatButton("Cancelar", 130) { Location = new Point(Width - 150, Height - 58) };
            yes.Click += delegate { DialogResult = DialogResult.OK; };
            no.Click += delegate { DialogResult = DialogResult.Cancel; };
            Controls.Add(yes); Controls.Add(no);
            Paint += (s, e) =>
            {
                using (var b = new LinearGradientBrush(ClientRectangle, Color.FromArgb(26, 26, 26), Color.Black, 90f)) e.Graphics.FillRectangle(b, ClientRectangle);
                using (var pen = new Pen(Color.FromArgb(90, 90, 90))) e.Graphics.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
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

    // Ventana del boton ?: explicacion simple arriba y detalle tecnico abajo.
    public class HelpDialog : Form
    {
        public HelpDialog(string title, string text)
        {
            FormBorderStyle = FormBorderStyle.None; StartPosition = FormStartPosition.CenterParent; ShowInTaskbar = false;
            BackColor = Theme.Bg; KeyPreview = true;
            int width = 580;
            var font = Theme.F(9.75f);
            int textH = TextRenderer.MeasureText(text, font, new Size(width - 60, int.MaxValue), TextFormatFlags.WordBreak).Height + 16;
            int boxH = Math.Max(90, Math.Min(440, textH));
            Size = new Size(width, boxH + 140);
            var box = new TextBox
            {
                Multiline = true, ReadOnly = true, BorderStyle = BorderStyle.None, BackColor = Color.FromArgb(14, 14, 14), ForeColor = Color.FromArgb(215, 215, 215),
                Font = font, Text = text.Replace("\n", Environment.NewLine), ScrollBars = textH > boxH ? ScrollBars.Vertical : ScrollBars.None,
                Bounds = new Rectangle(26, 64, width - 52, boxH), TabStop = false
            };
            var ok = new FlatButton("Entendido", 130) { Primary = true, Location = new Point(width - 156, Height - 56) };
            ok.Click += delegate { Close(); };
            Controls.Add(box); Controls.Add(ok);
            Shown += delegate { box.SelectionLength = 0; ok.Focus(); };
            Paint += (s, e) =>
            {
                using (var b = new LinearGradientBrush(ClientRectangle, Color.FromArgb(30, 30, 30), Color.Black, 90f)) e.Graphics.FillRectangle(b, ClientRectangle);
                using (var pen = new Pen(Color.FromArgb(90, 90, 90))) e.Graphics.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
                Theme.Draw(e.Graphics, "¿Qué hace?", Theme.F(8f, FontStyle.Bold), Theme.Dim, new Rectangle(26, 16, width - 52, 16), TextFormatFlags.Left);
                Theme.Draw(e.Graphics, title, Theme.F(13f, FontStyle.Bold), Theme.Text, new Rectangle(26, 32, width - 52, 26), TextFormatFlags.Left | TextFormatFlags.EndEllipsis);
            };
            KeyDown += (s, e) => { if (e.KeyCode == Keys.Escape || e.KeyCode == Keys.Enter) Close(); };
        }

        public static void Open(IWin32Window owner, string title, string text)
        {
            using (var d = new HelpDialog(title, text)) d.ShowDialog(owner);
        }
    }
}
