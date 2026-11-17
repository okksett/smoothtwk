// Ventana principal de SmoothTwk.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace SmoothTwk
{
    public class MainForm : Form
    {
        const int TopH = 40, SideW = 214, HeadH = 68, LogH = 36;
        readonly string[][] pagesDef =
        {
            new[] { "inicio", L.T("Inicio", "Home") },
            new[] { "matsu", L.T("Rendimiento", "Performance") },
            new[] { "kiku", L.T("Energía y CPU", "Power & CPU") },
            new[] { "fuji", L.T("Juegos", "Gaming") },
            new[] { "biblioteca", L.T("Biblioteca", "Library") },
            new[] { "ume", L.T("Privacidad", "Privacy") },
            new[] { "sakura", L.T("Limpiar Windows", "Debloat Windows") },
            new[] { "kiri", L.T("Ajustes extra", "Extra tweaks") },
            new[] { "botan", L.T("Apps y DNS", "Apps & DNS") },
            new[] { "ayame", L.T("Liberar espacio", "Free up space") },
            new[] { "hagi", L.T("Arranque", "Startup apps") },
            new[] { "susuki", L.T("Mantenimiento", "Maintenance") },
            new[] { "yanagi", L.T("Seguridad avanzada", "Advanced security") }
        };
        readonly Dictionary<string, NavButton> navs = new Dictionary<string, NavButton>();
        readonly Dictionary<string, Panel> pages = new Dictionary<string, Panel>();
        readonly Dictionary<string, Action> refreshers = new Dictionary<string, Action>();
        readonly Panel body = new Panel();
        readonly GradientCtl header = new GradientCtl(Color.FromArgb(0, 0, 0), Color.FromArgb(48, 48, 48), 0f);
        readonly GradientCtl topBar = new GradientCtl(Color.FromArgb(0, 0, 0), Color.FromArgb(32, 32, 32), 0f);
        readonly TextBox log = new TextBox();
        readonly List<Control> headerButtons = new List<Control>();
        string current, headTitle = "", headSub = "", headMono;
        Bitmap headIcon;
        int busy, pendingStatus;
        HudTile cpu, ram, disk, gpu, sys, power;
        readonly System.Windows.Forms.Timer timer = new System.Windows.Forms.Timer { Interval = 1000 };

        [DllImport("user32.dll")] static extern bool ReleaseCapture();
        [DllImport("user32.dll")] static extern IntPtr SendMessage(IntPtr h, int msg, int w, int l);
        [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr h, int attr, ref int value, int size);

        public MainForm()
        {
            Text = "SmoothTwk";
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(1120, 700);
            BackColor = Theme.Bg;
            ForeColor = Theme.Text;
            Font = Theme.F(9f);
            DoubleBuffered = true;
            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }

            BuildTopBar();
            BuildSidebar();
            header.SetBounds(SideW, TopH, ClientSize.Width - SideW, HeadH);
            header.Paint += (s, e) =>
            {
                int x = 26;
                if (headIcon != null || headMono != null)
                {
                    var ir = new Rectangle(24, 12, 44, 44);
                    e.Graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                    if (headIcon != null) e.Graphics.DrawImage(headIcon, ir); else GameTile.Monogram(e.Graphics, ir, headMono, false);
                    x = 84;
                }
                int titleRoom = (headerButtons.Count > 0 ? headerButtons.Min(b => b.Left) : header.Width) - x - 14;
                int ty = headSub.Length > 0 ? 8 : (HeadH - 36) / 2;
                Theme.Draw(e.Graphics, headTitle, Theme.F(titleRoom < 420 ? 15f : 20f, FontStyle.Bold), Theme.Text, new Rectangle(x, ty, titleRoom, 36), TextFormatFlags.Left | TextFormatFlags.EndEllipsis);
                if (headSub.Length > 0) Theme.Draw(e.Graphics, headSub, Theme.F(9f), Theme.Dim, new Rectangle(x + 2, 44, Math.Max(200, titleRoom), 18), TextFormatFlags.Left | TextFormatFlags.EndEllipsis);
                Theme.AccentLine(e.Graphics, 0, HeadH - 1, header.Width);
            };
            Controls.Add(header);

            body.SetBounds(SideW, TopH + HeadH, ClientSize.Width - SideW, ClientSize.Height - TopH - HeadH - LogH);
            body.BackColor = Theme.Bg;
            Controls.Add(body);

            log.Multiline = true; log.ReadOnly = true; log.ScrollBars = ScrollBars.None; log.BorderStyle = BorderStyle.None;
            log.BackColor = Theme.Panel; log.ForeColor = Theme.Dim; log.Font = Theme.F(8.75f, FontStyle.Regular, "Consolas");
            log.SetBounds(SideW + 22, ClientSize.Height - LogH + 10, ClientSize.Width - SideW - 44, LogH - 16);
            log.HandleCreated += delegate { DarkScroll(log.Handle); };
            Controls.Add(log);

            Engine.Log = Log;
            Paint += (s, e) =>
            {
                using (var b = new SolidBrush(Theme.Panel)) e.Graphics.FillRectangle(b, SideW, ClientSize.Height - LogH, ClientSize.Width - SideW, LogH);
                using (var pen = new Pen(Theme.Line))
                {
                    e.Graphics.DrawLine(pen, SideW, ClientSize.Height - LogH, ClientSize.Width, ClientSize.Height - LogH);
                    e.Graphics.DrawRectangle(pen, 0, 0, ClientSize.Width - 1, ClientSize.Height - 1);
                }
            };

            if (Engine.Limited)
            {
                if (Program.LastElevationError.Length > 0) Log(Program.LastElevationError);
                Log(L.T("Abierto sin permisos de administrador: puedes ver todo. Para cambiar algo, pulsa «Abrir como administrador».", "Opened without admin rights: you can look around. To change anything, click \"Run as administrator\"."));
            }
            Log(Engine.Dry ? L.T("Modo simulación: nada se cambia en el sistema.", "Simulation mode: nothing is changed.") : L.T("Listo. Todo lo que cambies se puede deshacer.", "Ready. Everything you change can be undone."));
            timer.Tick += (s, e) => UpdateHud();
            timer.Start();
            ShowPage("inicio");
        }

        // La copia de administrador nace sin foco: se trae al frente para que no quede escondida.
        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            if (Location.X < -1000) return;
            WindowState = FormWindowState.Normal;
            TopMost = true;
            Activate();
            BringToFront();
            TopMost = false;
            Program.Trace("ventana visible en " + Location + " tamaño " + Size);
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            try { int round = 2; DwmSetWindowAttribute(Handle, 33, ref round, 4); } catch { }
        }

        // ------------------------------------------------------------ marco
        void BuildTopBar()
        {
            topBar.SetBounds(0, 0, ClientSize.Width, TopH);
            topBar.MouseDown += (s, e) => { if (e.Button == MouseButtons.Left) { ReleaseCapture(); SendMessage(Handle, 0xA1, 2, 0); } };
            topBar.Paint += (s, e) =>
            {
                var g = e.Graphics;
                Theme.Card(g, new Rectangle(12, 10, 20, 20));
                Theme.Draw(g, "SmoothTwk", Theme.F(9f, FontStyle.Bold), Theme.Text, new Rectangle(36, 0, 200, TopH), TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
                var mode = Engine.Dry ? L.T("MODO SIMULACIÓN", "SIMULATION") : Engine.Limited ? L.T("SOLO LECTURA", "READ ONLY") : "";
                if (Engine.RebootPending) mode = L.T("REINICIO PENDIENTE", "RESTART PENDING") + (mode.Length > 0 ? "  ·  " + mode : "");
                Theme.Draw(g, mode, Theme.F(7.5f, FontStyle.Bold), Engine.RebootPending || Engine.Limited ? Theme.Text : Theme.Dim, new Rectangle(0, 0, ClientSize.Width - (Engine.Limited ? 420 : 206), TopH), TextFormatFlags.Right | TextFormatFlags.VerticalCenter);
                using (var pen = new Pen(Theme.Line)) g.DrawLine(pen, 0, TopH - 1, ClientSize.Width, TopH - 1);
            };
            var min = new WindowButton("min") { Location = new Point(ClientSize.Width - 92, 0) };
            var close = new WindowButton("close") { Location = new Point(ClientSize.Width - 46, 0) };
            min.Click += delegate { WindowState = FormWindowState.Minimized; };
            close.Click += delegate { Close(); };
            topBar.Controls.Add(min);
            topBar.Controls.Add(close);
            // Idioma: guarda la elección y reabre el programa en ese idioma.
            var lang = new FlatButton(L.T("English", "Español"), 92) { Height = 26, Location = new Point(ClientSize.Width - 196, 7) };
            lang.Click += delegate { L.Save(L.En ? "es" : "en"); Application.Restart(); };
            topBar.Controls.Add(lang);
            if (Engine.Limited)
            {
                var elevate = new FlatButton(L.T("Abrir como administrador", "Run as administrator"), 200) { Primary = true, Height = 26, Location = new Point(ClientSize.Width - 406, 7) };
                elevate.Click += delegate { Reopen(); };
                topBar.Controls.Add(elevate);
            }
            Controls.Add(topBar);
        }

        class WindowButton : Ctl
        {
            readonly string kind;
            public WindowButton(string kind) { this.kind = kind; Size = new Size(46, TopH - 1); Cursor = Cursors.Hand; }
            protected override void OnPaint(PaintEventArgs e)
            {
                if (hover) e.Graphics.FillRectangle(kind == "close" ? Brushes.White : new SolidBrush(Theme.Hover), ClientRectangle);
                using (var pen = new Pen(hover && kind == "close" ? Theme.Bg : Theme.Text, 1.2f))
                {
                    int cx = Width / 2, cy = Height / 2;
                    if (kind == "min") e.Graphics.DrawLine(pen, cx - 5, cy, cx + 5, cy);
                    else { e.Graphics.DrawLine(pen, cx - 5, cy - 5, cx + 5, cy + 5); e.Graphics.DrawLine(pen, cx + 5, cy - 5, cx - 5, cy + 5); }
                }
            }
        }

        void BuildSidebar()
        {
            var side = new GradientCtl(Color.FromArgb(0, 0, 0), Color.FromArgb(56, 56, 56), 90f);
            side.SetBounds(0, TopH, SideW, ClientSize.Height - TopH);
            side.Paint += (s, e) =>
            {
                var g = e.Graphics;
                Theme.Card(g, new Rectangle(18, 16, 38, 38));
                Theme.Draw(g, "SmoothTwk", Theme.F(16f, FontStyle.Bold), Theme.Text, new Rectangle(62, 12, 160, 28), TextFormatFlags.Left);
                Theme.Draw(g, "O P T I M I Z E R", Theme.F(7.5f, FontStyle.Bold), Theme.Dim, new Rectangle(64, 38, 150, 16), TextFormatFlags.Left);
                using (var pen = new Pen(Theme.Line)) g.DrawLine(pen, SideW - 1, 0, SideW - 1, side.Height);
            };
            int y = 74;
            foreach (var p in pagesDef)
            {
                var key = p[0];
                var nav = new NavButton(p[1], "") { Location = new Point(8, y), Width = SideW - 16 };
                nav.Click += delegate { ShowPage(key); };
                navs[key] = nav;
                side.Controls.Add(nav);
                y += 38;
            }
            Controls.Add(side);
        }

        public void Log(string text)
        {
            if (IsDisposed) return;
            if (InvokeRequired) { BeginInvoke(new Action<string>(Log), text); return; }
            log.AppendText(DateTime.Now.ToString("HH:mm:ss") + "  " + text + Environment.NewLine);
        }

        void SetHeader(string title, string sub, params FlatButton[] buttons)
        {
            headTitle = title; headSub = sub;
            foreach (var b in headerButtons) { header.Controls.Remove(b); b.Dispose(); }
            headerButtons.Clear();
            int x = header.Width - 22;
            foreach (var b in buttons.Reverse())
            {
                x -= b.Width;
                b.Location = new Point(x, (HeadH - b.Height) / 2);
                header.Controls.Add(b);
                headerButtons.Add(b);
                x -= 10;
            }
            header.Invalidate();
        }

        static FlatButton Btn(string text, Action click, int width = 150, bool primary = false)
        {
            var b = new FlatButton(text, width) { Primary = primary };
            b.Click += delegate { click(); };
            return b;
        }

        public void ShowPage(string key)
        {
            current = key;
            var navKey = key == "fivem" || key.StartsWith("game-") ? "biblioteca" : key;
            foreach (var n in navs) n.Value.Selected = n.Key == navKey;
            headIcon = null; headMono = null;
            foreach (var p in pages.Values) p.Visible = false;
            Panel page;
            if (!pages.TryGetValue(key, out page))
            {
                page = NewPage();
                pages[key] = page;
                body.Controls.Add(page);
                Build(key, page);
            }
            else if (refreshers.ContainsKey(key)) refreshers[key]();
            SetHeaderFor(key);
            page.Visible = true;
            page.BringToFront();
        }

        Panel NewPage()
        {
            var p = new Panel { AutoScroll = true, BackColor = Theme.Bg, Bounds = new Rectangle(0, 0, body.Width, body.Height) };
            p.Padding = new Padding(0, 0, 0, 12);
            p.HandleCreated += delegate { DarkScroll(p.Handle); };
            return p;
        }

        [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)] static extern int SetWindowTheme(IntPtr h, string app, string idList);
        static void DarkScroll(IntPtr handle)
        {
            try { SetWindowTheme(handle, "DarkMode_Explorer", null); } catch { }
        }

        int RowWidth { get { return body.Width - 44; } }

        static void Stack(Panel page)
        {
            int y = 6 - (page.AutoScrollPosition.Y < 0 ? -page.AutoScrollPosition.Y : 0);
            foreach (Control c in page.Controls)
            {
                if (c.Tag as string == "hidden" || c.Tag as string == "fixed") continue;
                c.Top = y;
                y += c.Height + (c is SectionTitle ? 2 : c is Row ? 6 : 0);
            }
        }

        void Add(Panel page, Control c)
        {
            c.Left = 22;
            c.Width = RowWidth;
            c.Tag = c.Tag ?? "row";
            page.Controls.Add(c);
        }

        // ------------------------------------------------------------ paginas
        void Build(string key, Panel page)
        {
            switch (key)
            {
                case "inicio": BuildHome(page); break;
                case "botan": BuildApps(page); break;
                case "ayame": BuildClean(page); break;
                case "hagi": BuildStartup(page); break;
                case "fivem": BuildFiveM(page); break;
                case "biblioteca": BuildLibrary(page); break;
                default:
                    if (key.StartsWith("game-")) BuildGame(key.Substring(5), page);
                    else BuildCard(key, page);
                    break;
            }
            Stack(page);
        }

        void SetGameHeader(string id, string sub, params FlatButton[] extra)
        {
            var info = GameLibrary.Find(id);
            var def = GameLibrary.Def(id);
            headIcon = GameLibrary.Icon(info, 128);
            headMono = headIcon == null && def != null ? def.Short : null;
            var buttons = new List<FlatButton> { Btn(L.T("←  Biblioteca", "←  Library"), () => ShowPage("biblioteca"), 120) };
            buttons.AddRange(extra);
            SetHeader(def != null ? def.Name : id, sub, buttons.ToArray());
        }

        void SetHeaderFor(string key)
        {
            if (key == "fivem")
            {
                SetGameHeader("fivem", L.T("PC check y rendimiento", "PC check and performance"),
                    Btn(L.T("Actualizar", "Refresh"), () => { if (refreshers.ContainsKey("fivem")) refreshers["fivem"](); }, 110), Btn(L.T("Restaurar para FiveM", "Restore for FiveM"), RestoreFiveM, 180, true));
                return;
            }
            if (key.StartsWith("game-"))
            {
                var id = key.Substring(5);
                var gc = Catalog.Games.FirstOrDefault(g => g.Id == id);
                SetGameHeader(id, gc != null ? gc.Tagline : "", Btn(L.T("Aplicar ajustes de este juego", "Apply this game's tweaks"), () => ApplyGame(id), 220, true));
                return;
            }
            var def = pagesDef.First(p => p[0] == key);
            var title = def[1];
            switch (key)
            {
                case "inicio":
                    SetHeader(title, Engine.IsLaptop ? L.T("Laptop", "Laptop") + "  ·  " + Engine.CpuName : L.T("Escritorio", "Desktop") + "  ·  " + Engine.CpuName);
                    break;
                case "botan":
                    SetHeader(title, "",
                        Btn(L.T("Instalar", "Install"), () => RunApps("install"), 110, true), Btn(L.T("Desinstalar", "Uninstall"), () => RunApps("uninstall"), 120), Btn(L.T("Actualizar todo", "Update all"), () => RunApps("upgrade"), 140));
                    break;
                case "ayame":
                    SetHeader(title, "", Btn(L.T("Medir", "Scan"), MeasureClean, 100), Btn(L.T("Limpiar", "Clean"), RunClean, 110, true));
                    break;
                case "hagi":
                    SetHeader(title, "", Btn(L.T("Recargar", "Reload"), () => { RebuildPage("hagi"); }, 110));
                    break;
                case "kiri":
                    SetHeader(title, "",
                        Btn(L.T("Básico", "Basic"), () => ApplyPreset("Minimal"), 110), Btn(L.T("Estándar", "Standard"), () => ApplyPreset("Standard"), 120, true), Btn(L.T("Deshacer todo aquí", "Undo this page"), () => RevertCard(key), 150));
                    break;
                case "biblioteca":
                    SetHeader(title, "", Btn(L.T("Buscar juegos", "Scan for games"), () => { GameLibrary.Refresh(); RebuildPage("biblioteca"); }, 150));
                    break;
                case "susuki":
                    SetHeader(title, "");
                    break;
                default:
                    var rec = Catalog.Tweaks.Any(t => t.Card == key && t.Rec && Engine.DeviceMatches(t));
                    var buttons = new List<FlatButton>();
                    if (rec) buttons.Add(Btn(L.T("Aplicar recomendados", "Apply recommended"), () => ApplyRecommended(key), 180, true));
                    buttons.Add(Btn(L.T("Deshacer todo aquí", "Undo this page"), () => RevertCard(key), 150));
                    SetHeader(title, key == "yanagi" ? L.T("Reducen la seguridad. Cada uno pide confirmación.", "These lower security. Each one asks first.") : "", buttons.ToArray());
                    break;
            }
        }

        void RebuildPage(string key)
        {
            Panel old;
            if (pages.TryGetValue(key, out old)) { body.Controls.Remove(old); old.Dispose(); pages.Remove(key); refreshers.Remove(key); }
            ShowPage(key);
        }

        // ------------------------------------------------------------ inicio
        void BuildHome(Panel page)
        {
            cpu = new HudTile(L.T("Procesador", "Processor")); ram = new HudTile(L.T("Memoria", "Memory")); disk = new HudTile(L.T("Disco del sistema", "System drive"));
            gpu = new HudTile(L.T("Gráficos", "Graphics")); sys = new HudTile(L.T("Encendido", "Uptime")); power = new HudTile(L.T("Energía", "Power"));
            var tiles = new[] { cpu, ram, disk, gpu, sys, power };
            int w = (RowWidth - 24) / 3;
            for (int i = 0; i < tiles.Length; i++)
            {
                tiles[i].Size = new Size(w, 112);
                tiles[i].Location = new Point(22 + (i % 3) * (w + 12), 10 + (i / 3) * 124);
                tiles[i].Tag = "fixed";
                page.Controls.Add(tiles[i]);
            }
            var gpus = Metrics.Gpu();
            var main = gpus.Split(new[] { " + " }, StringSplitOptions.None).OrderBy(n => n.IndexOf("Intel", StringComparison.OrdinalIgnoreCase) >= 0 ? 1 : 0).First();
            gpu.Value = System.Text.RegularExpressions.Regex.Replace(main, @"NVIDIA |GeForce |AMD |Radeon\(TM\) |Intel\(R\) |\(R\)|\(TM\)| GPU$", "").Trim();
            gpu.Sub = gpus;
            sys.Sub = Metrics.WindowsName();
            Metrics.Cpu();
            UpdateHud(true);

            int y = 270;
            var actions = new[]
            {
                Btn(L.T("Crear punto de restauración", "Create restore point"), () => RunAction(Catalog.Actions.First(a => a.Id == "restore-point")), 230, true),
                Btn(L.T("Aplicar recomendados", "Apply recommended"), Yaku, 190),
                Btn(L.T("Deshacer todo", "Undo everything"), Momiji, 160),
                Btn(L.T("Reiniciar Explorer", "Restart Explorer"), () => RunJob(L.T("Reiniciar Explorer", "Restart Explorer"), () => { Engine.RunScript("Invoke-WinUtilExplorerUpdate -action restart", Log); return L.T("Explorer reiniciado.", "Explorer restarted."); }, null), 160)
            };
            int x = 22;
            foreach (var b in actions)
            {
                b.Height = 40;
                b.Location = new Point(x, y);
                b.Tag = "fixed";
                page.Controls.Add(b);
                x += b.Width + 12;
            }
            var info = new Ctl { Tag = "fixed", Location = new Point(22, y + 56), Size = new Size(RowWidth, 22) };
            info.Paint += (s, e) => Theme.Draw(e.Graphics, L.T("Crea un punto de restauración antes de optimizar. Pulsa ? en cualquier opción para ver qué hace.", "Create a restore point before optimizing. Click ? on any option to see what it does."),
                Theme.F(9f), Theme.Dim, new Rectangle(0, 0, info.Width, info.Height), TextFormatFlags.Left);
            page.Controls.Add(info);
        }

        void UpdateHud() { UpdateHud(false); }

        void UpdateHud(bool first)
        {
            if (cpu == null || current != "inicio") return;
            cpu.Sub = Engine.CpuName;
            if (first) { cpu.Value = "…"; cpu.Invalidate(); }
            else { var c = Metrics.Cpu(); cpu.Value = c.ToString("0") + " %"; cpu.Push((float)c); }
            double used, total, pct;
            Metrics.Ram(out used, out total, out pct);
            ram.Value = pct.ToString("0") + " %"; ram.Sub = used.ToString("0.0") + L.T(" de ", " of ") + total.ToString("0") + L.T(" GB en uso", " GB in use"); ram.Push((float)pct);
            try
            {
                var d = new DriveInfo(Path.GetPathRoot(Environment.SystemDirectory));
                float usedPct = 100f * (d.TotalSize - d.TotalFreeSpace) / d.TotalSize;
                disk.Value = Cleaner.Format(d.TotalFreeSpace) + L.T(" libres", " free"); disk.Sub = d.Name + "  ·  " + Cleaner.Format(d.TotalSize) + L.T(" en total", " total"); disk.Bar = usedPct; disk.Invalidate();
            }
            catch { }
            var up = Metrics.Uptime();
            sys.Value = (int)up.TotalDays > 0 ? (int)up.TotalDays + " d " + up.Hours + " h" : up.Hours + " h " + up.Minutes + " min";
            sys.Invalidate();
            var ps = SystemInformation.PowerStatus;
            if (Engine.IsLaptop)
            {
                power.Value = (ps.BatteryLifePercent * 100).ToString("0") + " %";
                power.Sub = ps.PowerLineStatus == PowerLineStatus.Online ? L.T("Enchufada", "Plugged in") : L.T("Con batería", "On battery");
                power.Bar = ps.BatteryLifePercent * 100;
            }
            else { power.Value = L.T("Escritorio", "Desktop"); power.Sub = L.T("Corriente directa", "Wall power"); power.Bar = -1; }
            power.Invalidate();
            gpu.Invalidate();
        }

        // ------------------------------------------------------------ cartas de ajustes
        void BuildCard(string key, Panel page)
        {
            var toggles = new List<KeyValuePair<Tweak, Toggle>>();
            foreach (var group in Catalog.Tweaks.Where(t => t.Card == key).GroupBy(t => t.Group))
            {
                Add(page, new SectionTitle(group.Key) { Tag = "head" });
                foreach (var t in group) Add(page, TweakRow(t, toggles));
            }
            foreach (var group in Catalog.Actions.Where(a => a.Card == key).GroupBy(a => a.Group))
            {
                Add(page, new SectionTitle(group.Key) { Tag = "head" });
                foreach (var a in group) Add(page, ActionRow(a));
            }
            Action refresh = () => LoadStatuses(toggles);
            refreshers[key] = refresh;
            refresh();
        }

        Row TweakRow(Tweak t, List<KeyValuePair<Tweak, Toggle>> toggles)
        {
            Control right;
            if (t.OneShot) right = Btn(L.T("Ejecutar", "Run"), () => RunOneShot(t), 100);
            else
            {
                var tg = new Toggle();
                tg.Click += delegate { OnToggle(t, tg); };
                toggles.Add(new KeyValuePair<Tweak, Toggle>(t, tg));
                right = tg;
            }
            var row = new Row(t.Name, "", right, null, TweakHelp(t));
            row.Tags.AddRange(Tags(t));
            return row;
        }

        // ------------------------------------------------------------ Biblioteca de juegos
        void BuildLibrary(Panel page)
        {
            var tiles = new List<KeyValuePair<GameDef, GameTile>>();
            int w = (RowWidth - 24) / 3, h = 196, i = 0;
            foreach (var d in GameLibrary.Defs)
            {
                var gc = Catalog.Games.FirstOrDefault(g => g.Id == d.Id);
                var tile = new GameTile
                {
                    GameName = d.Name, Short = d.Short, Sub = gc != null && gc.AntiCheat.Length > 0 ? gc.AntiCheat : "",
                    Tag = "fixed", Size = new Size(w, h), Location = new Point(22 + (i % 3) * (w + 12), 12 + (i / 3) * (h + 12))
                };
                var id = d.Id;
                tile.Click += delegate { ShowPage(id == "fivem" ? "fivem" : "game-" + id); };
                page.Controls.Add(tile);
                tiles.Add(new KeyValuePair<GameDef, GameTile>(d, tile));
                i++;
            }
            Interlocked.Increment(ref pendingStatus);
            ThreadPool.QueueUserWorkItem(delegate
            {
                foreach (var kv in tiles)
                {
                    var info = GameLibrary.Find(kv.Key.Id);
                    var icon = GameLibrary.Icon(info, 128);
                    var tile = kv.Value;
                    if (IsDisposed) return;
                    BeginInvoke(new Action(() =>
                    {
                        tile.Icon = icon;
                        tile.Installed = info.Installed;
                        tile.Status = info.Installed ? L.T("Instalado", "Installed") : L.T("No encontrado", "Not found");
                        tile.Invalidate();
                    }));
                }
                Interlocked.Decrement(ref pendingStatus);
            });
        }

        void BuildGame(string id, Panel page)
        {
            var gc = Catalog.Games.FirstOrDefault(g => g.Id == id);
            var info = GameLibrary.Find(id);
            var def = GameLibrary.Def(id);
            var name = def != null ? def.Name : id;
            var toggles = new List<KeyValuePair<Tweak, Toggle>>();

            Add(page, new SectionTitle(L.T("En esta PC", "On this PC")) { Tag = "head" });
            var pill = new StatusPill();
            pill.Set(info.Installed ? L.T("INSTALADO", "INSTALLED") : L.T("NO ENCONTRADO", "NOT FOUND"), info.Installed);
            Add(page, new Row(info.Installed ? name + L.T(" está instalado", " is installed") : name + L.T(" no está instalado", " is not installed"), info.Installed ? info.Root : "", pill, null,
                info.Installed
                    ? L.T("SmoothTwk encontró estos ejecutables, que son los que reciben los ajustes de Windows de esta página:", "SmoothTwk found these executables; they get this page's Windows tweaks:") + "\n" + string.Join("\n", info.Exes.Select(x => "• " + x))
                    : L.T("No encontré " + name + " en Steam, Epic, Riot, Ubisoft, Battle.net, Microsoft Store ni en sus carpetas por defecto. Los ajustes de este juego se activan cuando esté instalado. Los ajustes generales funcionan igual.",
                          name + " was not found in Steam, Epic, Riot, Ubisoft, Battle.net, Microsoft Store or its default folders. This game's tweaks unlock once it is installed. The general tweaks work anyway.")));

            Add(page, new SectionTitle(L.T("Ajustes de Windows solo para ", "Windows tweaks only for ") + name) { Tag = "head" });
            foreach (var t in Catalog.Tweaks.Where(t => t.Card == "game-" + id)) Add(page, TweakRow(t, toggles));

            if (gc != null)
            {
                Add(page, new SectionTitle(L.T("Ajustes generales que ayudan", "General tweaks that help")) { Tag = "head" });
                foreach (var sid in gc.Shared)
                {
                    var t = Catalog.Tweaks.FirstOrDefault(x => x.Id == sid);
                    if (t != null && (t.Device == "both" || Engine.DeviceMatches(t))) Add(page, TweakRow(t, toggles));
                }
                foreach (var group in gc.Guide.GroupBy(g => g.Group))
                {
                    Add(page, new SectionTitle(L.T("Dentro del juego · ", "In game · ") + group.Key) { Tag = "head" });
                    foreach (var item in group)
                        Add(page, new Row(item.Name, "", StatusPill.Value(item.Value), null,
                            item.Why + L.T("\n\nValor recomendado: ", "\n\nRecommended value: ") + item.Value + L.T("\n\nEsto se cambia a mano dentro del juego, en su menú de configuración. SmoothTwk no toca los archivos del juego ni el anticheat.", "\n\nYou change this by hand in the game's settings menu. SmoothTwk never touches game files or the anticheat.")));
                }
                if (gc.AntiCheat.Length > 0 || gc.Cautions.Count > 0)
                {
                    Add(page, new SectionTitle(L.T("Anticheat y cuidados", "Anticheat and cautions")) { Tag = "head" });
                    foreach (var c in gc.Cautions)
                    {
                        var cut = c.IndexOf(". ", StringComparison.Ordinal);
                        Add(page, new Row(cut > 0 ? c.Substring(0, cut + 1) : c, "", null, null, c + (gc.AntiCheat.Length > 0 ? L.T("\n\nAnticheat del juego: ", "\n\nGame anticheat: ") + gc.AntiCheat + "." : "")));
                    }
                }
            }
            Action refresh = () => LoadStatuses(toggles);
            refreshers["game-" + id] = refresh;
            refresh();
        }

        void ApplyGame(string id)
        {
            var gc = Catalog.Games.FirstOrDefault(g => g.Id == id);
            var list = Catalog.Tweaks.Where(t => t.Card == "game-" + id).ToList();
            if (gc != null) list.AddRange(gc.Shared.Select(s => Catalog.Tweaks.FirstOrDefault(t => t.Id == s)).Where(t => t != null && t.Rec && Engine.DeviceMatches(t)));
            list = list.Where(t => Engine.GuardReason(t) == null && !t.OneShot).Distinct().ToList();
            if (list.Count == 0) { Log(L.T("No hay ajustes que se puedan aplicar para este juego en esta PC.", "No tweaks can be applied for this game on this PC.")); return; }
            if (!ConfirmDialog.Ask(this, L.T("Aplicar ajustes del juego", "Apply game tweaks"), L.T("Se aplican ", "Applies ") + list.Count + L.T(" ajustes: ", " tweaks: ") + string.Join(", ", list.Select(t => t.Name)) + L.T(". Todos se pueden deshacer.", ". All can be undone."), L.T("Aplicar", "Apply"))) return;
            RunBatch(L.T("Ajustes del juego", "Game tweaks"), list, false, "game-" + id);
        }

        // ------------------------------------------------------------ FiveM
        static readonly Dictionary<string, string> ServiceHelp = new Dictionary<string, string>
        {
            { "PcaSvc", "Asistente para la compatibilidad de programas. Anota qué programas se abrieron y ayuda cuando uno viejo falla. Los staff lo revisan porque guarda rastro de los .exe que ejecutaste." },
            { "DPS", "Servicio de directivas de diagnóstico. Detecta y resuelve problemas de Windows y guarda historial de diagnóstico. Si está apagado, el solucionador de problemas no funciona." },
            { "DiagTrack", "Experiencias del usuario y telemetría. Envía datos de uso a Microsoft y registra actividad. Muchos optimizadores lo apagan; los servidores de FiveM lo piden encendido." },
            { "SysMain", "SysMain (antes Superfetch). Precarga tus programas y mantiene los datos de Prefetch, que muestran qué ejecutables se usaron y cuándo." },
            { "EventLog", "Registro de eventos de Windows. Guarda el historial de todo lo que pasa en el sistema. Si está apagado no queda ningún registro: casi todos los PC check lo revisan." },
            { "Appinfo", "Información de aplicaciones. Es el servicio que muestra el aviso de administrador (UAC). Viene en manual: arranca solo cuando algo pide permisos." },
            { "BFE", "Motor de filtrado base. Es la base del Firewall de Windows, de la seguridad de red y de muchos antivirus y anticheats." },
            { "bam", "Moderador de actividad en segundo plano (BAM). Es un driver de Windows que registra cuándo se ejecutó cada programa. Si se apaga, vuelve a funcionar tras reiniciar." },
            { "CDPSvc", "Servicio de plataforma de dispositivos conectados. Lo usan el portapapeles en la nube, Phone Link y compartir con dispositivos cercanos." },
            { "DusmSvc", "Uso de datos. Cuenta cuántos datos usa cada app y aplica los límites de datos." },
            { "SSDPSRV", "Detección SSDP. Encuentra dispositivos en tu red como impresoras, televisores y consolas." },
            { "UmRdpService", "Redirector de puertos del Escritorio remoto. Solo se usa en conexiones de Escritorio remoto." },
            { "RemoteRegistry", "Registro remoto. Permite leer y cambiar tu registro desde otra PC. En Windows viene apagado; enciéndelo solo si el staff lo pide." }
        };
        static readonly Dictionary<string, string> ServiceHelpEn = new Dictionary<string, string>
        {
            { "PcaSvc", "Program Compatibility Assistant. Records which programs ran and helps when an old one fails. Staff check it because it keeps a trace of the .exe files you ran." },
            { "DPS", "Diagnostic Policy Service. Detects and fixes Windows problems and keeps diagnostic history. If it is off, troubleshooters stop working." },
            { "DiagTrack", "Connected User Experiences and Telemetry. Sends usage data to Microsoft and logs activity. Many optimizers turn it off; FiveM servers want it on." },
            { "SysMain", "SysMain (formerly Superfetch). Preloads your programs and keeps the Prefetch data that shows which executables ran and when." },
            { "EventLog", "Windows Event Log. Keeps the history of everything that happens on the system. If it is off, nothing is logged: almost every PC check looks at it." },
            { "Appinfo", "Application Information. The service behind the admin prompt (UAC). It is set to manual: it starts when something asks for admin rights." },
            { "BFE", "Base Filtering Engine. The base of Windows Firewall, network security and many antivirus and anticheats." },
            { "bam", "Background Activity Moderator (BAM). A Windows driver that records when each program ran. If it is turned off, it works again after a restart." },
            { "CDPSvc", "Connected Devices Platform Service. Used by cloud clipboard, Phone Link and nearby sharing." },
            { "DusmSvc", "Data Usage. Counts how much data each app uses and applies data limits." },
            { "SSDPSRV", "SSDP Discovery. Finds devices on your network such as printers, TVs and consoles." },
            { "UmRdpService", "Remote Desktop port redirector. Only used for Remote Desktop connections." },
            { "RemoteRegistry", "Remote Registry. Lets another PC read and change your registry. Off by default in Windows; turn it on only if staff ask." }
        };
        readonly List<KeyValuePair<string, Row>> fivemRows = new List<KeyValuePair<string, Row>>();

        void BuildFiveM(Panel page)
        {
            Add(page, new SectionTitle(L.T("Protección del PC check", "PC check protection")) { Tag = "head" });
            var guard = new Toggle { State = Engine.FiveMGuard ? "on" : "off" };
            guard.Click += delegate
            {
                bool next = !Engine.FiveMGuard;
                if (!next && !ConfirmDialog.Ask(this, L.T("Apagar la protección", "Turn protection off"), L.T("Otros apartados podrán apagar servicios que los servidores de FiveM revisan. Si juegas FiveM, déjala encendida.", "Other pages will be able to turn off services FiveM servers check. If you play FiveM, keep it on."), L.T("Apagar", "Turn off"))) return;
                Engine.FiveMGuard = next;
                guard.State = next ? "on" : "off";
                Log((next ? L.T("Protección de FiveM encendida", "FiveM protection on") : L.T("Protección de FiveM apagada", "FiveM protection off")) + (Engine.Dry ? L.T(" (simulación)", " (simulation)") : "") + ".");
                foreach (var r in refreshers.Where(r => r.Key != "fivem").ToList()) { Panel old; if (pages.TryGetValue(r.Key, out old)) { body.Controls.Remove(old); old.Dispose(); pages.Remove(r.Key); refreshers.Remove(r.Key); } }
            };
            var guardRow = new Row(L.T("Proteger el PC check", "Protect the PC check"), "", guard, null,
                L.T("Con esto encendido, SmoothTwk no deja aplicar ningún ajuste (de cualquier apartado ni de «Aplicar recomendados») que apague PcaSvc, DPS, DiagTrack, SysMain, EventLog, Appinfo, BFE o BAM, ni que desactive Prefetch o el Asistente de compatibilidad. Esos ajustes aparecen marcados como ROMPE FIVEM. Lo que ya habías aplicado lo puedes seguir deshaciendo. Viene encendido por defecto.",
                    "While this is on, SmoothTwk won't apply any tweak (from any page or from \"Apply recommended\") that turns off PcaSvc, DPS, DiagTrack, SysMain, EventLog, Appinfo, BFE or BAM, or disables Prefetch or the Compatibility Assistant. Those tweaks are marked BREAKS FIVEM. Anything you already applied can still be undone. On by default."));
            guardRow.Tags.Add("REC");
            Add(page, guardRow);

            Add(page, new SectionTitle(L.T("Servicios que revisan los servidores", "Services servers check")) { Tag = "head" });
            fivemRows.Clear();
            foreach (var name in Engine.FiveMServices.Concat(Engine.FiveMExtras))
            {
                var pill = new StatusPill();
                string help;
                (L.En ? ServiceHelpEn : ServiceHelp).TryGetValue(name, out help);
                bool extra = Engine.FiveMExtras.Contains(name);
                var row = new Row(name, L.T("Leyendo estado…", "Reading status…"), pill, null, (help ?? name) + (extra
                    ? L.T("\n\nNo forma parte de los servicios que suelen revisar los PC check, pero viene en Servicios.rar.", "\n\nNot one of the services PC checks usually look at, but it is part of Servicios.rar.")
                    : L.T("\n\nEste servicio suele revisarse en el PC check de los servidores de FiveM. Debe estar en ejecución.", "\n\nFiveM server PC checks usually look at this service. It must be running.")));
                if (!extra) row.Tags.Add("PC CHECK");
                Add(page, row);
                fivemRows.Add(new KeyValuePair<string, Row>(name, row));
            }

            Add(page, new SectionTitle(L.T("Restaurar", "Restore")) { Tag = "head" });
            foreach (var a in Catalog.Actions.Where(a => a.Id == "fivem-restore")) Add(page, ActionRow(a));
            var toggles = new List<KeyValuePair<Tweak, Toggle>>();
            foreach (var t in Catalog.Tweaks.Where(t => t.Id == "fivem-remoteregistry")) Add(page, TweakRow(t, toggles));

            Add(page, new SectionTitle(L.T("Optimizaciones para FiveM", "FiveM optimizations")) { Tag = "head" });
            var ids = new[] { "game-gpu-fivem", "game-fso-fivem", "modojuego", "mmcss", "dxwindowed", "gamedvr", "hags", "sticky", "throttling", "smoothplan", "usbsuspend", "pcie" };
            foreach (var id in ids)
            {
                var t = Catalog.Tweaks.FirstOrDefault(x => x.Id == id);
                if (t != null) Add(page, TweakRow(t, toggles));
            }
            foreach (var group in Catalog.Actions.Where(a => a.Card == "fivem" && a.Id != "fivem-restore").GroupBy(a => a.Group))
            {
                Add(page, new SectionTitle(group.Key) { Tag = "head" });
                foreach (var a in group) Add(page, ActionRow(a));
            }
            Action refresh = () => { LoadStatuses(toggles); LoadServiceStatus(); };
            refreshers["fivem"] = refresh;
            refresh();
        }

        void LoadServiceStatus()
        {
            var rows = fivemRows.ToList();
            Interlocked.Increment(ref pendingStatus);
            ThreadPool.QueueUserWorkItem(delegate
            {
                int ok = 0, total = 0;
                foreach (var kv in rows)
                {
                    var info = Engine.ReadService(kv.Key);
                    bool critical = Engine.FiveMServices.Contains(kv.Key);
                    if (critical) { total++; if (info.Running) ok++; }
                    var row = kv.Value;
                    if (IsDisposed) return;
                    BeginInvoke(new Action(() =>
                    {
                        row.Title = info.Display.Length > 0 ? info.Display + "  (" + info.Name + ")" : info.Name;
                        row.Desc = info.Exists ? L.T("Estado: ", "Status: ") + info.Status + L.T("  ·  Inicio: ", "  ·  Startup: ") + info.Start : L.T("No existe en este Windows", "Not present in this Windows");
                        var pill = (StatusPill)row.Right;
                        pill.Set(info.Running ? L.T("ACTIVO", "RUNNING") : info.Disabled ? L.T("DESACTIVADO", "DISABLED") : info.Exists ? L.T("DETENIDO", "STOPPED") : L.T("NO EXISTE", "MISSING"), info.Running || (!critical && !info.Disabled));
                        row.Invalidate();
                    }));
                }
                var summary = ok + L.T(" de ", " of ") + total + L.T(" servicios del PC check en ejecución.", " PC check services running.");
                if (!IsDisposed) BeginInvoke(new Action(() => Log(summary + (ok < total ? L.T(" Pulsa «Restaurar para FiveM».", " Click \"Restore for FiveM\".") : L.T(" Todo listo para el PC check.", " Ready for the PC check.")))));
                Interlocked.Decrement(ref pendingStatus);
            });
        }

        void RestoreFiveM()
        {
            var action = Catalog.Actions.First(a => a.Id == "fivem-restore");
            var b = Backup.Load();
            var conflicts = Catalog.Tweaks.Where(t => t.FiveMConflict && b.ContainsKey(t.Id)).ToList();
            var text = L.T("Se encienden los servicios del PC check con el inicio que trae Windows y se ponen BAM, Prefetch y el Asistente de compatibilidad por defecto.",
                "Turns the PC check services back on with Windows' default startup and resets BAM, Prefetch and the Compatibility Assistant.");
            if (conflicts.Count > 0) text += L.T(" También se deshacen ", " It also undoes ") + conflicts.Count + L.T(" ajuste(s) de SmoothTwk que los apagaban: ", " SmoothTwk tweak(s) that turned them off: ") + string.Join(", ", conflicts.Select(t => t.Name)) + ".";
            if (!ConfirmDialog.Ask(this, L.T("Restaurar para FiveM", "Restore for FiveM"), text, L.T("Restaurar", "Restore"))) return;
            RunJob(L.T("Restaurar para FiveM", "Restore for FiveM"), () =>
            {
                foreach (var t in conflicts) Log(Engine.Revert(t, Log));
                int code = Engine.RunScript(action.Script, Log);
                return (Engine.Dry ? L.T("[simulación] ", "[simulation] ") : "") + (code == 0 ? L.T("Listo: servicios por defecto. Reinicia si BAM estaba apagado.", "Done: services at defaults. Restart if BAM was off.") : L.T("Terminó con avisos: revisa el registro.", "Finished with warnings: check the log."));
            }, () => { if (refreshers.ContainsKey("fivem")) refreshers["fivem"](); });
        }

        static IEnumerable<string> Tags(Tweak t)
        {
            if (t.Rec && Engine.DeviceMatches(t)) yield return "REC";
            if (t.OneShot) yield return L.T("SIN DESHACER", "NO UNDO");
            else if (t.Warn.Length > 0 && t.Card == "yanagi") yield return L.T("RIESGO", "RISK");
            if (t.FiveMConflict) yield return L.T("ROMPE FIVEM", "BREAKS FIVEM");
        }

        // ------------------------------------------------------------ textos del boton ?
        static readonly Dictionary<string, string> PowerNames = new Dictionary<string, string>
        {
            { "be337238-0d82-4146-a960-4f3749d470c7", L.T("Modo turbo del procesador", "Processor boost mode") },
            { "0cc5b647-c1df-4637-891a-dec35c318583", L.T("Núcleos mínimos despiertos (estacionamiento)", "Minimum unparked cores") },
            { "ee12f906-d277-404b-b6da-e5fa1a576df5", L.T("Ahorro de energía de PCIe", "PCIe power saving") },
            { "48e6b7a6-50f5-4782-a5d4-53bb8f07e226", L.T("Suspensión selectiva de USB", "USB selective suspend") },
            { "12bbebe6-58d6-4636-95bb-3217ef867c1a", L.T("Ahorro de energía del Wi-Fi", "Wi-Fi power saving") }
        };

        static string TweakHelp(Tweak t)
        {
            var sb = new System.Text.StringBuilder();
            sb.Append(t.Help.Length > 0 ? t.Help : t.Desc);
            var lines = new List<string>();
            foreach (var r in t.Registry)
            {
                var name = r.Name.Length == 0 ? L.T("(predeterminado)", "(default)") : r.Name;
                lines.Add(r.Value == null ? L.T("Registro: borra «", "Registry: deletes \"") + name + L.T("» en ", "\" in ") + r.Path : L.T("Registro: ", "Registry: ") + r.Path + " → " + name + " = " + (r.Value.Length == 0 ? L.T("(vacío)", "(empty)") : r.Value));
            }
            foreach (var s in t.Services)
                lines.Add(L.T("Servicio «", "Service \"") + s[0] + L.T("»: inicio ", "\": startup ") + (s[1] == "Disabled" ? L.T("desactivado", "disabled") : s[1] == "Manual" ? "manual" : s[1] == "AutomaticDelayedStart" ? L.T("automático retrasado", "automatic (delayed)") : L.T("automático", "automatic")));
            foreach (var task in t.Tasks) lines.Add(L.T("Tarea programada desactivada: ", "Scheduled task disabled: ") + task);
            foreach (var p in t.Power)
            {
                string label;
                if (!PowerNames.TryGetValue(p.Setting, out label)) label = L.T("Ajuste de energía ", "Power setting ") + p.Setting;
                if (p.Ac.HasValue) lines.Add(L.T("Energía: ", "Power: ") + label + " = " + p.Ac + L.T(" (enchufado)", " (plugged in)"));
                if (p.Dc.HasValue) lines.Add(L.T("Energía: ", "Power: ") + label + " = " + p.Dc + L.T(" (con batería)", " (on battery)"));
            }
            switch (t.Special)
            {
                case "plan-high": lines.Add(L.T("Activa el plan de energía Alto rendimiento. Al deshacer vuelve a tu plan anterior.", "Turns on the High performance power plan. Undo goes back to your previous plan.")); break;
                case "plan-smoothtwk": lines.Add(L.T("Importa el plan «SmoothTwk Power Plan» (powercfg /import) y lo activa. Al deshacer vuelve a tu plan anterior y lo borra.", "Imports the \"SmoothTwk Power Plan\" (powercfg /import) and turns it on. Undo goes back to your previous plan and deletes it.")); break;
                case "plan-ultimate": lines.Add(L.T("Crea una copia del plan Máximo rendimiento y la activa. Al deshacer vuelve a tu plan y borra la copia.", "Creates a copy of the Ultimate Performance plan and turns it on. Undo goes back to your plan and deletes the copy.")); break;
                case "overlay-best": lines.Add(L.T("Pone el modo de energía de Windows en «Mejor rendimiento» (powercfg /overlaysetactive).", "Sets the Windows power mode to \"Best performance\" (powercfg /overlaysetactive).")); break;
                case "hibernate-off": lines.Add(L.T("Ejecuta: ", "Runs: ") + "powercfg /hibernate off"); break;
                case "memcomp-off": lines.Add(L.T("Ejecuta: ", "Runs: ") + "Disable-MMAgent -MemoryCompression"); break;
                case "dx-windowed": lines.Add(L.T("Registro: HKCU\\Software\\Microsoft\\DirectX\\UserGpuPreferences → agrega SwapEffectUpgradeEnable=1 sin borrar tus otras opciones gráficas.", "Registry: HKCU\\Software\\Microsoft\\DirectX\\UserGpuPreferences → adds SwapEffectUpgradeEnable=1 without removing your other graphics options.")); break;
            }
            if (t.ApplyScript.Length > 0) lines.Add(L.T("Ejecuta un script de PowerShell", "Runs a PowerShell script") + (t.Download ? L.T(" que descarga archivos de internet", " that downloads files from the internet") : "") + ".");
            if (lines.Count > 0)
            {
                sb.Append(L.T("\n\nQué cambia exactamente:", "\n\nWhat it changes exactly:"));
                foreach (var l in lines.Take(14)) sb.Append("\n• " + l);
                if (lines.Count > 14) sb.Append("\n• … " + L.T("y ", "and ") + (lines.Count - 14) + L.T(" cambios más.", " more changes."));
            }
            sb.Append(L.T("\n\n¿Se puede deshacer? ", "\n\nCan it be undone? "));
            if (t.OneShot) sb.Append(L.T("No. Esta acción no tiene vuelta atrás.", "No. This action can't be reversed."));
            else
            {
                sb.Append(L.T("Sí: apaga el interruptor o usa «Deshacer todo» en Inicio. SmoothTwk guarda el valor que tenías antes.", "Yes: turn the switch off or use \"Undo everything\" on Home. SmoothTwk saves your previous value."));
                if (t.ApplyScript.Length > 0 && t.UndoScript.Length == 0) sb.Append(L.T(" La parte del script no se revierte sola.", " The script part is not reversed automatically."));
            }
            sb.Append(L.T("\nNecesita reiniciar: ", "\nNeeds a restart: ") + (t.Reboot ? L.T("sí.", "yes.") : "no."));
            sb.Append(L.T("\nPensado para: ", "\nMeant for: ") + (t.Device == "desktop" ? L.T("PC de escritorio.", "desktop PCs.") : t.Device == "laptop" ? "laptops." : L.T("cualquier equipo.", "any PC.")));
            if (t.Rec) sb.Append(L.T(" Recomendado.", " Recommended."));
            return sb.ToString();
        }

        static string ActionHelp(ActionItem a)
        {
            var sb = new System.Text.StringBuilder(a.Help.Length > 0 ? a.Help : a.Desc);
            var script = a.Script.Replace("\r", "").Split('\n').Select(l => l.Trim()).Where(l => l.Length > 0).ToList();
            if (script.Count > 0)
            {
                sb.Append(L.T("\n\nComandos que ejecuta:", "\n\nCommands it runs:"));
                foreach (var l in script.Take(10)) sb.Append("\n  " + (l.Length > 110 ? l.Substring(0, 110) + "…" : l));
                if (script.Count > 10) sb.Append("\n  … (" + (script.Count - 10) + L.T(" líneas más)", " more lines)"));
            }
            if (a.Download) sb.Append(L.T("\n\nDescarga archivos de internet.", "\n\nDownloads files from the internet."));
            return sb.ToString();
        }

        Row ActionRow(ActionItem a)
        {
            var row = new Row(a.Name, "", Btn(a.Button, () => RunAction(a), 100, a.Id == "restore-point"), null, ActionHelp(a));
            if (a.Id == "restore-point") row.Tags.Add("REC");
            return row;
        }

        void LoadStatuses(List<KeyValuePair<Tweak, Toggle>> toggles)
        {
            foreach (var kv in toggles) kv.Value.State = "busy";
            Interlocked.Increment(ref pendingStatus);
            ThreadPool.QueueUserWorkItem(delegate
            {
                foreach (var kv in toggles)
                {
                    var st = Engine.Status(kv.Key);
                    var tg = kv.Value;
                    if (IsDisposed) return;
                    BeginInvoke(new Action(() => tg.State = st));
                }
                Interlocked.Decrement(ref pendingStatus);
            });
        }

        void OnToggle(Tweak t, Toggle tg)
        {
            if (tg.State == "busy") return;
            // Bloqueado: explica por qué en vez de no hacer nada.
            if (tg.State == "na") { HelpDialog.Open(this, t.Name, Engine.GuardReason(t) ?? L.T("No disponible en este equipo.", "Not available on this PC.")); return; }
            // "partial" (aplicado a medias) se ve apagado: pulsarlo lo aplica completo.
            bool undo = tg.State == "on";
            if (!undo)
            {
                if (!Engine.DeviceMatches(t) && !ConfirmDialog.Ask(this, t.Name, L.T("Este ajuste está pensado para ", "This tweak is meant for ") + (t.Device == "desktop" ? L.T("escritorio", "desktops") : "laptops") + L.T(". ¿Aplicarlo igual?", ". Apply anyway?"), L.T("Aplicar", "Apply"))) return;
                if (t.Warn.Length > 0 && !ConfirmDialog.Ask(this, t.Name, t.Warn, L.T("Aplicar", "Apply"))) return;
            }
            var prev = tg.State;
            if (!TryBusy()) return;
            tg.State = "busy";
            ThreadPool.QueueUserWorkItem(delegate
            {
                string msg, st = prev;
                try { msg = undo ? Engine.Revert(t, Log) : Engine.Apply(t, Log); st = Engine.Status(t); }
                catch (Exception ex) { msg = "Error: " + ex.Message; }
                Done(msg, () => tg.State = st);
            });
        }

        bool TryBusy()
        {
            if (!CanChange()) return false;
            if (Interlocked.CompareExchange(ref busy, 1, 0) != 0) { Log(L.T("Espera a que termine la tarea actual.", "Wait for the current task to finish.")); return false; }
            return true;
        }

        // Sin permisos de administrador se puede mirar todo, pero no cambiar nada.
        bool CanChange()
        {
            if (!Engine.Limited) return true;
            if (ConfirmDialog.Ask(this, L.T("Se necesitan permisos", "Admin rights needed"), L.T("Para cambiar ajustes, SmoothTwk tiene que abrirse como administrador. Pulsa Reabrir y luego Sí en el aviso de Windows.", "To change settings, SmoothTwk must run as administrator. Click Reopen, then Yes in the Windows prompt."), L.T("Reabrir", "Reopen"))) Reopen();
            return false;
        }

        void Reopen()
        {
            if (Program.Elevate()) { Close(); return; }
            Log(Program.LastElevationError + L.T(" Sigues en modo solo lectura.", " Still read only."));
        }

        void Done(string msg, Action ui)
        {
            if (IsDisposed) return;
            BeginInvoke(new Action(() =>
            {
                if (ui != null) ui();
                Log(msg);
                Interlocked.Exchange(ref busy, 0);
                topBar.Invalidate();
            }));
        }

        void RunJob(string label, Func<string> work, Action ui)
        {
            if (!TryBusy()) return;
            Log(label + "…");
            ThreadPool.QueueUserWorkItem(delegate
            {
                string msg;
                try { msg = work(); } catch (Exception ex) { msg = L.T("Error: ", "Error: ") + ex.Message; }
                Done(msg, ui);
            });
        }

        void RunOneShot(Tweak t)
        {
            if (!ConfirmDialog.Ask(this, t.Name, (t.Warn.Length > 0 ? t.Warn : L.T("Esta acción no se puede deshacer.", "This action can't be undone.")) + (t.Download ? L.T(" Descarga archivos de internet.", " Downloads files from the internet.") : ""), L.T("Ejecutar", "Run"))) return;
            RunJob(t.Name, () => Engine.Apply(t, Log), null);
        }

        void RunAction(ActionItem a)
        {
            if (a.Id == "fivem-restore") { RestoreFiveM(); return; }
            var text = a.Warn.Length > 0 ? a.Warn : a.Desc;
            if (a.Download) text += L.T(" Descarga archivos de internet.", " Downloads files from the internet.");
            if (!ConfirmDialog.Ask(this, a.Name, text, a.Button)) return;
            RunJob(a.Name, () =>
            {
                int code = Engine.RunScript(a.Script, Log);
                return (Engine.Dry ? L.T("[simulación] ", "[simulation] ") : "") + (code == 0 ? L.T("Terminado: ", "Done: ") : L.T("Terminó con error (", "Finished with error (") + code + "): ") + a.Name;
            }, null);
        }

        void ApplyRecommended(string card)
        {
            var list = Catalog.Tweaks.Where(t => t.Card == card && t.Rec && Engine.DeviceMatches(t) && Engine.GuardReason(t) == null).ToList();
            RunBatch(L.T("Aplicar recomendados", "Apply recommended"), list, false, card);
        }

        void RevertCard(string card)
        {
            var b = Backup.Load();
            var list = Catalog.Tweaks.Where(t => t.Card == card && b.ContainsKey(t.Id)).ToList();
            if (list.Count == 0) { Log(L.T("No hay nada que deshacer en esta página.", "Nothing to undo on this page.")); return; }
            if (!ConfirmDialog.Ask(this, L.T("Deshacer todo aquí", "Undo this page"), L.T("Se restauran ", "Restores ") + list.Count + L.T(" ajustes a sus valores anteriores.", " tweaks to their previous values."), L.T("Deshacer", "Undo"))) return;
            RunBatch(L.T("Deshacer", "Undo"), list, true, card);
        }

        void ApplyPreset(string name)
        {
            List<string> ids;
            if (!Catalog.Presets.TryGetValue(name, out ids)) return;
            var list = Catalog.Tweaks.Where(t => ids.Contains(t.Id)).ToList();
            var label = name == "Minimal" ? L.T("Ajustes extra: básico", "Extra tweaks: basic") : L.T("Ajustes extra: estándar", "Extra tweaks: standard");
            if (!ConfirmDialog.Ask(this, label, L.T("Se aplican ", "Applies ") + list.Count + L.T(" ajustes: ", " tweaks: ") + string.Join(", ", list.Select(t => t.Name)) + ".", L.T("Aplicar", "Apply"))) return;
            RunBatch(label, list, false, "kiri");
        }

        void RunBatch(string label, List<Tweak> list, bool undo, string card)
        {
            RunJob(label, () =>
            {
                int n = 0;
                foreach (var t in list)
                {
                    if (!undo && Engine.Status(t) == "on") continue;
                    Log((undo ? Engine.Revert(t, Log) : Engine.Apply(t, Log)));
                    n++;
                }
                return label + ": " + n + L.T(" ajuste(s).", " tweak(s).");
            }, () => { if (card != null && refreshers.ContainsKey(card)) refreshers[card](); });
        }

        void Yaku()
        {
            var list = Catalog.Tweaks.Where(t => t.Rec && Engine.DeviceMatches(t) && Engine.GuardReason(t) == null && !t.OneShot).ToList();
            if (!ConfirmDialog.Ask(this, L.T("Aplicar recomendados", "Apply recommended"), L.T("Se aplican hasta ", "Applies up to ") + list.Count + L.T(" ajustes recomendados para ", " recommended tweaks for ") + (Engine.IsLaptop ? "laptop" : L.T("escritorio", "desktop")) + L.T(". Antes se intenta crear un punto de restauración.", ". A restore point is created first."), L.T("Aplicar", "Apply"))) return;
            RunJob(L.T("Aplicar recomendados", "Apply recommended"), () =>
            {
                Engine.RestorePoint(Log);
                int n = 0;
                foreach (var t in list)
                {
                    if (Engine.Status(t) == "on") continue;
                    Log(Engine.Apply(t, Log));
                    n++;
                }
                return L.T("Recomendados listos: ", "Recommended done: ") + n + L.T(" ajuste(s) aplicados.", " tweak(s) applied.");
            }, InvalidateCards);
        }

        void Momiji()
        {
            var b = Backup.Load();
            var list = Catalog.Tweaks.Where(t => b.ContainsKey(t.Id)).ToList();
            if (list.Count == 0) { Log(L.T("No hay ajustes de SmoothTwk que deshacer.", "No SmoothTwk tweaks to undo.")); return; }
            if (!ConfirmDialog.Ask(this, L.T("Deshacer todo", "Undo everything"), L.T("Se restauran ", "Restores ") + list.Count + L.T(" ajustes a como estaban antes de SmoothTwk.", " tweaks to how they were before SmoothTwk."), L.T("Deshacer", "Undo"))) return;
            RunJob(L.T("Deshacer todo", "Undo everything"), () =>
            {
                foreach (var t in list) Log(Engine.Revert(t, Log));
                return L.T("Listo: ", "Done: ") + list.Count + L.T(" ajuste(s) restaurados.", " tweak(s) restored.");
            }, InvalidateCards);
        }

        void InvalidateCards()
        {
            if (current != null && refreshers.ContainsKey(current)) refreshers[current]();
        }

        // ------------------------------------------------------------ apps y DNS
        readonly List<KeyValuePair<AppItem, Row>> appRows = new List<KeyValuePair<AppItem, Row>>();

        void BuildApps(Panel page)
        {
            foreach (var group in Catalog.Actions.Where(a => a.Card == "botan").GroupBy(a => a.Group))
            {
                Add(page, new SectionTitle(group.Key) { Tag = "head" });
                foreach (var a in group) Add(page, ActionRow(a));
            }
            Add(page, new SectionTitle("Apps (" + Catalog.Apps.Count + ")") { Tag = "head" });
            var search = new SearchBox { Height = 40, Tag = "head" };
            Add(page, search);
            foreach (var app in Catalog.Apps)
            {
                var check = new Check();
                var a = app;
                check.CheckedChanged += delegate { a.Selected = check.Checked; };
                var row = new Row(app.Name, "", null, check, app.Desc + L.T("\n\nCategoría: ", "\n\nCategory: ") + app.Category + L.T("\nPaquete de WinGet: ", "\nWinGet package: ") + app.Winget
                    + L.T("\n\nMárcala y pulsa «Instalar» arriba. Se descarga desde la fuente oficial de WinGet (Microsoft) y se instala en silencio, aceptando los términos del paquete. «Desinstalar» la quita igual.",
                          "\n\nTick it and click Install at the top. It downloads from the official WinGet source (Microsoft) and installs silently, accepting the package terms. Uninstall removes it the same way."));
                row.Tags.Add(app.Category.ToUpperInvariant());
                Add(page, row);
                appRows.Add(new KeyValuePair<AppItem, Row>(app, row));
            }
            search.Box.TextChanged += delegate
            {
                var q = search.Box.Text.Trim();
                page.SuspendLayout();
                foreach (var kv in appRows)
                {
                    bool show = q.Length == 0 || kv.Key.Name.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0 || kv.Key.Category.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0;
                    kv.Value.Visible = show;
                    kv.Value.Tag = show ? "row" : "hidden";
                }
                Stack(page);
                page.ResumeLayout();
            };
        }

        class SearchBox : Ctl
        {
            public readonly TextBox Box = new TextBox();
            public SearchBox()
            {
                Box.BorderStyle = BorderStyle.None; Box.BackColor = Theme.Panel; Box.ForeColor = Theme.Text; Box.Font = Theme.F(10f);
                Controls.Add(Box);
            }
            protected override void OnLayout(LayoutEventArgs e) { Box.SetBounds(46, 11, Width - 60, 20); base.OnLayout(e); }
            protected override void OnPaint(PaintEventArgs e)
            {
                var r = new Rectangle(0, 2, Width - 1, Height - 6);
                using (var b = new SolidBrush(Theme.Panel)) e.Graphics.FillRectangle(b, r);
                using (var pen = new Pen(Theme.Line)) e.Graphics.DrawRectangle(pen, r);
                using (var pen = new Pen(Theme.Dim, 1.5f)) { e.Graphics.DrawEllipse(pen, 16, 12, 11, 11); e.Graphics.DrawLine(pen, 26, 22, 31, 27); }
                if (Box.Text.Length == 0 && !Box.Focused) Theme.Draw(e.Graphics, L.T("Buscar app o categoría…", "Search app or category…"), Theme.F(10f), Theme.Faint, new Rectangle(46, 11, Width - 60, 20), TextFormatFlags.Left);
            }
        }

        void RunApps(string mode)
        {
            var selected = Catalog.Apps.Where(a => a.Selected).ToList();
            if (mode != "upgrade" && selected.Count == 0) { Log(L.T("Marca al menos una app.", "Tick at least one app.")); return; }
            var verb = mode == "install" ? L.T("Instalar", "Install") : mode == "uninstall" ? L.T("Desinstalar", "Uninstall") : L.T("Actualizar", "Update");
            var text = mode == "upgrade" ? L.T("WinGet actualiza todas las apps que tengan versión nueva.", "WinGet updates every app with a newer version.") : verb + ": " + string.Join(", ", selected.Select(a => a.Name)) + ".";
            if (!ConfirmDialog.Ask(this, verb + L.T(" con WinGet", " with WinGet"), text + L.T(" Descarga desde internet y acepta los acuerdos de cada paquete.", " Downloads from the internet and accepts each package's agreements."), verb)) return;
            const string filter = " 2>&1 | Out-String -Stream | Where-Object { $_ -match '[A-Za-z0-9]' -and $_ -notmatch '^\\s*[-\\\\|/█▒ ]+\\s*$' }";
            const string flags = " --silent --accept-package-agreements --accept-source-agreements --disable-interactivity";
            RunJob(verb, () =>
            {
                if (mode == "upgrade")
                {
                    Engine.RunScript("winget upgrade --all --include-unknown" + flags + filter, Log);
                    return L.T("Actualización terminada.", "Update finished.");
                }
                int ok = 0;
                foreach (var a in selected)
                {
                    Log(verb + ": " + a.Name);
                    var cmd = mode == "install"
                        ? "winget install --id '" + a.Winget + "' --exact --source winget" + flags
                        : "winget uninstall --id '" + a.Winget + "' --exact --silent --accept-source-agreements --disable-interactivity";
                    if (Engine.RunScript(cmd + filter + "\r\nexit $LASTEXITCODE", Log) == 0) ok++;
                }
                return verb + ": " + ok + L.T(" de ", " of ") + selected.Count + L.T(" terminaron bien.", " succeeded.");
            }, null);
        }

        // ------------------------------------------------------------ limpieza
        List<KeyValuePair<CleanTarget, Row>> cleanRows;

        void BuildClean(Panel page)
        {
            cleanRows = new List<KeyValuePair<CleanTarget, Row>>();
            Add(page, new SectionTitle(L.T("Qué se limpia", "What gets cleaned")) { Tag = "head" });
            foreach (var t in Cleaner.Targets())
            {
                var check = new Check { Checked = t.On };
                var target = t;
                check.CheckedChanged += delegate { target.On = check.Checked; };
                var row = new Row(t.Name, "", null, check, t.Recycle
                    ? L.T("Vacía la Papelera de reciclaje de todas las unidades. Lo que haya ahí se borra para siempre. Está apagada por defecto para que no la borres sin querer.", "Empties the Recycle Bin on every drive. Whatever is there is deleted for good. Off by default so you don't empty it by accident.")
                    : t.Desc + L.T("\n\nSon archivos que Windows o los programas vuelven a crear cuando los necesitan, así que borrarlos es seguro. Si un programa está abierto y usa alguno, ese archivo se salta.\n\nCarpetas que se vacían:\n", "\n\nWindows and apps recreate these files when needed, so deleting them is safe. Files in use by an open program are skipped.\n\nFolders that get emptied:\n") + string.Join("\n", t.Paths.Select(p => "• " + p)));
                row.Tags.Add(t.Recycle ? L.T("SIN DESHACER", "NO UNDO") : "…");
                Add(page, row);
                cleanRows.Add(new KeyValuePair<CleanTarget, Row>(t, row));
            }
            MeasureClean();
        }

        void MeasureClean()
        {
            if (cleanRows == null) return;
            var rows = cleanRows;
            Interlocked.Increment(ref pendingStatus);
            ThreadPool.QueueUserWorkItem(delegate
            {
                foreach (var kv in rows)
                {
                    if (!kv.Key.Recycle) Cleaner.Measure(kv.Key);
                    var row = kv.Value; var t = kv.Key;
                    if (IsDisposed) return;
                    BeginInvoke(new Action(() => { if (!t.Recycle) { row.Tags.Clear(); row.Tags.Add(Cleaner.Format(t.Size)); } row.Invalidate(); }));
                }
                Interlocked.Decrement(ref pendingStatus);
            });
        }

        void RunClean()
        {
            var chosen = cleanRows.Where(kv => kv.Key.On).Select(kv => kv.Key).ToList();
            if (chosen.Count == 0) { Log(L.T("No hay nada marcado.", "Nothing is ticked.")); return; }
            if (!ConfirmDialog.Ask(this, L.T("Limpiar", "Clean"), L.T("Se borran ", "Deletes ") + chosen.Count + L.T(" grupo(s) de archivos. No se puede deshacer. Cierra los navegadores para liberar más.", " group(s) of files. Can't be undone. Close your browsers to free more."), L.T("Limpiar", "Clean"))) return;
            RunJob(L.T("Limpieza", "Cleanup"), () =>
            {
                long freed = chosen.Sum(t => Cleaner.Run(t));
                return (Engine.Dry ? L.T("[simulación] ", "[simulation] ") : "") + L.T("Liberado: ", "Freed: ") + Cleaner.Format(freed);
            }, MeasureClean);
        }

        // ------------------------------------------------------------ inicio de Windows
        void BuildStartup(Panel page)
        {
            Add(page, new SectionTitle(L.T("Arrancan con Windows", "Start with Windows")) { Tag = "head" });
            foreach (var item in Startup.Items())
            {
                var tg = new Toggle { State = item.Enabled ? "on" : "off" };
                var it = item;
                tg.Click += delegate
                {
                    if (!CanChange()) return;
                    try
                    {
                        Startup.Set(it, !it.Enabled);
                        if (!Engine.Dry) tg.State = it.Enabled ? "on" : "off";
                        Log((Engine.Dry ? L.T("[simulación] ", "[simulation] ") : "") + (it.Enabled ? L.T("Activado: ", "Enabled: ") : L.T("Desactivado: ", "Disabled: ")) + it.Name);
                    }
                    catch (Exception ex) { Log(L.T("No se pudo cambiar: ", "Could not change: ") + ex.Message); }
                };
                var row = new Row(item.Name, "", tg, null, L.T("«" + item.Name + "» se abre solo cada vez que inicias sesión en Windows. Apagarlo hace que el PC arranque más rápido y libera memoria; el programa sigue instalado y puedes abrirlo a mano cuando quieras.\n\nEs lo mismo que desactivarlo en el Administrador de tareas > Aplicaciones de arranque: no borra nada y se puede volver a encender aquí.\n\nComando que ejecuta Windows:\n",
                    "\"" + item.Name + "\" opens by itself every time you sign in to Windows. Turning it off makes the PC start faster and frees memory; the program stays installed and you can open it yourself any time.\n\nSame as disabling it in Task Manager > Startup apps: nothing is deleted and you can turn it back on here.\n\nCommand Windows runs:\n") + item.Command);
                row.Tags.Add(item.Scope.ToUpperInvariant());
                Add(page, row);
            }
        }

        // ------------------------------------------------------------ capturas para pruebas
        public int Shoot(string dir)
        {
            Directory.CreateDirectory(dir);
            StartPosition = FormStartPosition.Manual;
            Location = new Point(-5000, 0);
            ShowInTaskbar = false;
            Show();
            int i = 0;
            foreach (var key in pagesDef.Select(p => p[0]).Concat(new[] { "fivem", "game-valorant", "game-minecraft" }))
            {
                ShowPage(key);
                var until = DateTime.Now.AddSeconds(40);
                do { Application.DoEvents(); Thread.Sleep(50); } while (pendingStatus > 0 && DateTime.Now < until);
                for (int k = 0; k < 10; k++) { Application.DoEvents(); Thread.Sleep(30); }
                using (var bmp = new Bitmap(Width, Height))
                {
                    DrawToBitmap(bmp, new Rectangle(0, 0, Width, Height));
                    bmp.Save(Path.Combine(dir, (++i).ToString("00") + "-" + key + ".png"));
                }
            }
            var sample = Catalog.Tweaks.FirstOrDefault(t => t.Id == "boost") ?? Catalog.Tweaks[0];
            using (var d = new HelpDialog(sample.Name, TweakHelp(sample)))
            {
                d.StartPosition = FormStartPosition.Manual;
                d.Location = new Point(-5000, 0);
                d.Show();
                for (int k = 0; k < 10; k++) { Application.DoEvents(); Thread.Sleep(30); }
                using (var bmp = new Bitmap(d.Width, d.Height))
                {
                    d.DrawToBitmap(bmp, new Rectangle(0, 0, d.Width, d.Height));
                    bmp.Save(Path.Combine(dir, (++i).ToString("00") + "-ayuda.png"));
                }
                d.Close();
            }
            File.WriteAllText(Path.Combine(dir, "log.txt"), log.Text);
            Close();
            return 0;
        }
    }
}
