// Ventana principal de Hanafuda Opti.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace Hanafuda
{
    public class MainForm : Form
    {
        const int TopH = 40, SideW = 214, HeadH = 76, LogH = 104;
        readonly string[][] pagesDef =
        {
            new[] { "inicio", "Inicio", "Estado del PC y acciones rápidas" },
            new[] { "matsu", "Rendimiento", "Respuesta del sistema y del disco" },
            new[] { "kiku", "Energía y CPU", "Planes de energía y procesador" },
            new[] { "fuji", "Juegos", "Prioridad y latencia para jugar" },
            new[] { "ume", "Privacidad", "Telemetría y rastreo" },
            new[] { "sakura", "Limpiar Windows", "Anuncios, Copilot y apps extra" },
            new[] { "kiri", "Ajustes de WinUtil", "Opciones de Chris Titus" },
            new[] { "botan", "Apps y DNS", "Instalar programas y cambiar DNS" },
            new[] { "ayame", "Liberar espacio", "Temporales y caché" },
            new[] { "hagi", "Arranque", "Programas al encender el PC" },
            new[] { "susuki", "Mantenimiento", "Reparar, actualizar y funciones" },
            new[] { "yanagi", "Seguridad avanzada", "Ajustes con riesgo" }
        };
        readonly Dictionary<string, NavButton> navs = new Dictionary<string, NavButton>();
        readonly Dictionary<string, Panel> pages = new Dictionary<string, Panel>();
        readonly Dictionary<string, Action> refreshers = new Dictionary<string, Action>();
        readonly Panel body = new Panel();
        readonly Ctl header = new Ctl();
        readonly Ctl topBar = new Ctl();
        readonly TextBox log = new TextBox();
        readonly List<Control> headerButtons = new List<Control>();
        string current, headTitle = "", headSub = "";
        int busy, pendingStatus;
        HudTile cpu, ram, disk, gpu, sys, power;
        readonly System.Windows.Forms.Timer timer = new System.Windows.Forms.Timer { Interval = 1000 };

        [DllImport("user32.dll")] static extern bool ReleaseCapture();
        [DllImport("user32.dll")] static extern IntPtr SendMessage(IntPtr h, int msg, int w, int l);
        [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr h, int attr, ref int value, int size);

        public MainForm()
        {
            Text = "Hanafuda Opti";
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
                int titleRoom = (headerButtons.Count > 0 ? headerButtons.Min(b => b.Left) : header.Width) - 40;
                Theme.Draw(e.Graphics, headTitle, Theme.F(titleRoom < 420 ? 15f : 20f, FontStyle.Bold), Theme.Text, new Rectangle(26, 12, titleRoom, 36), TextFormatFlags.Left | TextFormatFlags.EndEllipsis);
                Theme.Draw(e.Graphics, headSub, Theme.F(9f), Theme.Dim, new Rectangle(28, 48, 520, 18), TextFormatFlags.Left | TextFormatFlags.EndEllipsis);
                using (var pen = new Pen(Theme.Line)) e.Graphics.DrawLine(pen, 0, HeadH - 1, header.Width, HeadH - 1);
            };
            Controls.Add(header);

            body.SetBounds(SideW, TopH + HeadH, ClientSize.Width - SideW, ClientSize.Height - TopH - HeadH - LogH);
            body.BackColor = Theme.Bg;
            Controls.Add(body);

            log.Multiline = true; log.ReadOnly = true; log.ScrollBars = ScrollBars.Vertical; log.BorderStyle = BorderStyle.None;
            log.BackColor = Theme.Panel; log.ForeColor = Theme.Dim; log.Font = Theme.F(8.75f, FontStyle.Regular, "Consolas");
            log.SetBounds(SideW + 14, ClientSize.Height - LogH + 10, ClientSize.Width - SideW - 28, LogH - 20);
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

            Log(Engine.Dry ? "Modo simulación: nada se cambia en el sistema." : "Listo. Cada ajuste guarda el valor anterior para poder deshacerlo.");
            if (Engine.Limited)
            {
                if (Program.LastElevationError.Length > 0) Log(Program.LastElevationError);
                Log("Abierto sin permisos de administrador: puedes ver todo. Para cambiar algo, pulsa «Abrir como administrador» y luego Sí.");
            }
            Log("Equipo detectado: " + (Engine.IsLaptop ? "laptop" : "escritorio") + " · " + Engine.CpuName);
            timer.Tick += (s, e) => UpdateHud();
            timer.Start();
            ShowPage("inicio");
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
                Theme.Card(g, new Rectangle(14, 10, 14, 20));
                Theme.Draw(g, "HANAFUDA OPTI", Theme.F(9f, FontStyle.Bold), Theme.Text, new Rectangle(36, 0, 200, TopH), TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
                var mode = Engine.Dry ? "MODO SIMULACIÓN" : Engine.Limited ? "SOLO LECTURA" : "ADMINISTRADOR";
                if (Engine.Limited) mode = "SOLO LECTURA";
                if (Engine.RebootPending) mode = "REINICIO PENDIENTE  ·  " + mode;
                Theme.Draw(g, mode, Theme.F(7.5f, FontStyle.Bold), Engine.RebootPending || Engine.Limited ? Theme.Text : Theme.Dim, new Rectangle(0, 0, ClientSize.Width - (Engine.Limited ? 330 : 110), TopH), TextFormatFlags.Right | TextFormatFlags.VerticalCenter);
                using (var pen = new Pen(Theme.Line)) g.DrawLine(pen, 0, TopH - 1, ClientSize.Width, TopH - 1);
            };
            var min = new WindowButton("min") { Location = new Point(ClientSize.Width - 92, 0) };
            var close = new WindowButton("close") { Location = new Point(ClientSize.Width - 46, 0) };
            min.Click += delegate { WindowState = FormWindowState.Minimized; };
            close.Click += delegate { Close(); };
            topBar.Controls.Add(min);
            topBar.Controls.Add(close);
            if (Engine.Limited)
            {
                var elevate = new FlatButton("Abrir como administrador", 200) { Primary = true, Height = 26, Location = new Point(ClientSize.Width - 314, 7) };
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
            var side = new Ctl();
            side.SetBounds(0, TopH, SideW, ClientSize.Height - TopH);
            side.BackColor = Theme.Bg;
            side.Paint += (s, e) =>
            {
                var g = e.Graphics;
                Theme.Card(g, new Rectangle(20, 18, 26, 38));
                Theme.Draw(g, "HANAFUDA", Theme.F(15f, FontStyle.Bold), Theme.Text, new Rectangle(54, 14, 160, 28), TextFormatFlags.Left);
                Theme.Draw(g, "O P T I", Theme.F(8f, FontStyle.Bold), Theme.Dim, new Rectangle(56, 40, 160, 16), TextFormatFlags.Left);
                using (var pen = new Pen(Theme.Line)) g.DrawLine(pen, SideW - 1, 0, SideW - 1, side.Height);
            };
            int y = 74;
            foreach (var p in pagesDef)
            {
                var key = p[0];
                var nav = new NavButton(p[1], p[2]) { Location = new Point(6, y), Width = SideW - 12 };
                nav.Click += delegate { ShowPage(key); };
                navs[key] = nav;
                side.Controls.Add(nav);
                y += 46;
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
            foreach (var n in navs) n.Value.Selected = n.Key == key;
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
                y += c.Height + (c is SectionTitle ? 2 : 0);
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
                default: BuildCard(key, page); break;
            }
            Stack(page);
        }

        void SetHeaderFor(string key)
        {
            var def = pagesDef.First(p => p[0] == key);
            var title = def[1];
            switch (key)
            {
                case "inicio":
                    SetHeader(title, Engine.IsLaptop ? "Laptop detectada: los recomendados cuidan batería y temperatura." : "Escritorio detectado: los recomendados priorizan rendimiento.");
                    break;
                case "botan":
                    SetHeader(title, "Apps del catálogo de WinUtil instaladas con WinGet.",
                        Btn("Instalar", () => RunApps("install"), 110, true), Btn("Desinstalar", () => RunApps("uninstall"), 120), Btn("Actualizar todo", () => RunApps("upgrade"), 140));
                    break;
                case "ayame":
                    SetHeader(title, "Solo temporales y caché. Contraseñas, cookies e historial no se tocan.", Btn("Medir", MeasureClean, 100), Btn("Limpiar", RunClean, 110, true));
                    break;
                case "hagi":
                    SetHeader(title, "Igual que en el Administrador de tareas: desactivar no borra nada.", Btn("Recargar", () => { RebuildPage("hagi"); }, 110));
                    break;
                case "kiri":
                    SetHeader(title, "Opciones de WinUtil de Chris Titus dentro de Hanafuda.",
                        Btn("Preset mínimo", () => ApplyPreset("Minimal"), 130), Btn("Preset estándar", () => ApplyPreset("Standard"), 140, true), Btn("Deshacer carta", () => RevertCard(key), 130));
                    break;
                case "susuki":
                    SetHeader(title, "Reparaciones, Windows Update, funciones de Windows y paneles.");
                    break;
                default:
                    var rec = Catalog.Tweaks.Any(t => t.Card == key && t.Rec && Engine.DeviceMatches(t));
                    var buttons = new List<FlatButton>();
                    if (rec) buttons.Add(Btn("Aplicar recomendados", () => ApplyRecommended(key), 170, true));
                    buttons.Add(Btn("Deshacer carta", () => RevertCard(key), 130));
                    SetHeader(title, key == "yanagi" ? "Estos ajustes reducen la seguridad. Cada uno pide confirmación." : "Interruptor blanco = activo. Vuelve a pulsarlo para deshacer.", buttons.ToArray());
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
            cpu = new HudTile("Procesador"); ram = new HudTile("Memoria"); disk = new HudTile("Disco del sistema");
            gpu = new HudTile("Gráficos"); sys = new HudTile("Windows"); power = new HudTile("Energía");
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
                Btn("Aplicar recomendados", Yaku, 210, true),
                Btn("Deshacer todo", Momiji, 160),
                Btn("Punto de restauración", () => RunJob("Punto de restauración", () => Engine.RestorePoint(Log), null), 190),
                Btn("Reiniciar Explorer", () => RunJob("Reiniciar Explorer", () => { Engine.RunScript("Invoke-WinUtilExplorerUpdate -action restart", Log); return "Explorer reiniciado."; }, null), 160)
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
            var info = new Ctl { Tag = "fixed", Location = new Point(22, y + 58), Size = new Size(RowWidth, 120) };
            int tweaks = Catalog.Tweaks.Count, wu = Catalog.Tweaks.Count(t => t.Source == "WinUtil") + Catalog.Actions.Count(a => a.Source == "WinUtil");
            info.Paint += (s, e) =>
            {
                var lines = new[]
                {
                    tweaks + " ajustes  ·  " + Catalog.Actions.Count + " acciones  ·  " + Catalog.Apps.Count + " apps",
                    wu + " opciones vienen de WinUtil de Chris Titus y corren dentro de Hanafuda.",
                    "«Aplicar recomendados» solo aplica lo pensado para " + (Engine.IsLaptop ? "laptop" : "escritorio") + " y antes intenta crear un punto de restauración.",
                    "«Deshacer todo» devuelve cada valor que Hanafuda cambió a como estaba."
                };
                for (int i = 0; i < lines.Length; i++)
                    Theme.Draw(e.Graphics, lines[i], i == 0 ? Theme.F(10f, FontStyle.Bold) : Theme.F(9f), i == 0 ? Theme.Text : Theme.Dim, new Rectangle(0, i * 24, info.Width, 22), TextFormatFlags.Left);
            };
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
            ram.Value = pct.ToString("0") + " %"; ram.Sub = used.ToString("0.0") + " de " + total.ToString("0") + " GB en uso"; ram.Push((float)pct);
            try
            {
                var d = new DriveInfo(Path.GetPathRoot(Environment.SystemDirectory));
                float usedPct = 100f * (d.TotalSize - d.TotalFreeSpace) / d.TotalSize;
                disk.Value = Cleaner.Format(d.TotalFreeSpace) + " libres"; disk.Sub = d.Name + "  ·  " + Cleaner.Format(d.TotalSize) + " en total"; disk.Bar = usedPct; disk.Invalidate();
            }
            catch { }
            var up = Metrics.Uptime();
            sys.Value = (int)up.TotalDays > 0 ? (int)up.TotalDays + " d " + up.Hours + " h" : up.Hours + " h " + up.Minutes + " min";
            sys.Invalidate();
            var ps = SystemInformation.PowerStatus;
            if (Engine.IsLaptop)
            {
                power.Value = (ps.BatteryLifePercent * 100).ToString("0") + " %";
                power.Sub = ps.PowerLineStatus == PowerLineStatus.Online ? "Enchufada" : "Con batería";
                power.Bar = ps.BatteryLifePercent * 100;
            }
            else { power.Value = "Escritorio"; power.Sub = "Corriente directa"; power.Bar = -1; }
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
                foreach (var t in group)
                {
                    Control right;
                    if (t.OneShot)
                    {
                        var tw = t;
                        right = Btn("Ejecutar", () => RunOneShot(tw), 100);
                    }
                    else
                    {
                        var tg = new Toggle();
                        var tw = t;
                        tg.Click += delegate { OnToggle(tw, tg); };
                        toggles.Add(new KeyValuePair<Tweak, Toggle>(t, tg));
                        right = tg;
                    }
                    var row = new Row(t.Name, t.Desc, right);
                    row.Tags.AddRange(Tags(t));
                    Add(page, row);
                }
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

        static IEnumerable<string> Tags(Tweak t)
        {
            if (t.Rec && Engine.DeviceMatches(t)) yield return "REC";
            if (t.Device == "desktop") yield return "ESCRITORIO";
            if (t.Device == "laptop") yield return "LAPTOP";
            if (t.OneShot) yield return "SIN DESHACER";
            else if (t.Warn.Length > 0 && t.Card == "yanagi") yield return "RIESGO";
            if (t.Reboot) yield return "REINICIO";
            if (t.Source == "WinUtil") yield return "WINUTIL";
            if (t.Download) yield return "DESCARGA";
        }

        Row ActionRow(ActionItem a)
        {
            var row = new Row(a.Name, a.Desc, Btn(a.Button, () => RunAction(a), 100));
            if (a.Source == "WinUtil") row.Tags.Add("WINUTIL");
            if (a.Download) row.Tags.Add("DESCARGA");
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
            if (tg.State == "na") { Log(Engine.GuardReason(t) ?? "No disponible en este equipo."); return; }
            bool undo = tg.State == "on" || (tg.State == "partial" && Backup.Has(t.Id));
            if (!undo)
            {
                if (!Engine.DeviceMatches(t) && !ConfirmDialog.Ask(this, t.Name, "Este ajuste está pensado para " + (t.Device == "desktop" ? "escritorio" : "laptop") + ". ¿Aplicarlo igual?", "Aplicar")) return;
                if (t.Warn.Length > 0 && !ConfirmDialog.Ask(this, t.Name, t.Warn, "Aplicar")) return;
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
            if (Interlocked.CompareExchange(ref busy, 1, 0) != 0) { Log("Espera a que termine la tarea actual."); return false; }
            return true;
        }

        // Sin permisos de administrador se puede mirar todo, pero no cambiar nada.
        bool CanChange()
        {
            if (!Engine.Limited) return true;
            if (ConfirmDialog.Ask(this, "Se necesitan permisos", "Para cambiar ajustes, Hanafuda tiene que abrirse como administrador. Pulsa Reabrir y luego Sí en el aviso de Windows.", "Reabrir")) Reopen();
            return false;
        }

        void Reopen()
        {
            if (Program.Elevate()) { Close(); return; }
            Log(Program.LastElevationError + " Sigues en modo solo lectura.");
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
                try { msg = work(); } catch (Exception ex) { msg = "Error: " + ex.Message; }
                Done(msg, ui);
            });
        }

        void RunOneShot(Tweak t)
        {
            if (!ConfirmDialog.Ask(this, t.Name, (t.Warn.Length > 0 ? t.Warn : "Esta acción no se puede deshacer.") + (t.Download ? " Descarga archivos de internet." : ""), "Ejecutar")) return;
            RunJob(t.Name, () => Engine.Apply(t, Log), null);
        }

        void RunAction(ActionItem a)
        {
            var text = a.Warn.Length > 0 ? a.Warn : a.Desc;
            if (a.Download) text += " Descarga archivos de internet.";
            if (!ConfirmDialog.Ask(this, a.Name, text, a.Button)) return;
            RunJob(a.Name, () =>
            {
                int code = Engine.RunScript(a.Script, Log);
                return (Engine.Dry ? "[simulación] " : "") + (code == 0 ? "Terminado: " : "Terminó con error (" + code + "): ") + a.Name;
            }, null);
        }

        void ApplyRecommended(string card)
        {
            var list = Catalog.Tweaks.Where(t => t.Card == card && t.Rec && Engine.DeviceMatches(t) && Engine.GuardReason(t) == null).ToList();
            RunBatch("Aplicar recomendados", list, false, card);
        }

        void RevertCard(string card)
        {
            var b = Backup.Load();
            var list = Catalog.Tweaks.Where(t => t.Card == card && b.ContainsKey(t.Id)).ToList();
            if (list.Count == 0) { Log("No hay ajustes de esta carta que deshacer."); return; }
            if (!ConfirmDialog.Ask(this, "Deshacer carta", "Se restauran " + list.Count + " ajustes a sus valores anteriores.", "Deshacer")) return;
            RunBatch("Deshacer carta", list, true, card);
        }

        void ApplyPreset(string name)
        {
            List<string> ids;
            if (!Catalog.Presets.TryGetValue(name, out ids)) return;
            var list = Catalog.Tweaks.Where(t => ids.Contains(t.Id)).ToList();
            if (!ConfirmDialog.Ask(this, "Preset " + (name == "Minimal" ? "mínimo" : "estándar") + " de WinUtil", "Se aplican " + list.Count + " ajustes: " + string.Join(", ", list.Select(t => t.Name)) + ".", "Aplicar")) return;
            RunBatch("Preset de WinUtil", list, false, "kiri");
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
                return label + ": " + n + " ajuste(s).";
            }, () => { if (card != null && refreshers.ContainsKey(card)) refreshers[card](); });
        }

        void Yaku()
        {
            var list = Catalog.Tweaks.Where(t => t.Rec && Engine.DeviceMatches(t) && Engine.GuardReason(t) == null && !t.OneShot).ToList();
            if (!ConfirmDialog.Ask(this, "Aplicar recomendados", "Se aplican hasta " + list.Count + " ajustes recomendados para " + (Engine.IsLaptop ? "laptop" : "escritorio") + ". Antes se intenta crear un punto de restauración.", "Aplicar")) return;
            RunJob("Aplicar recomendados", () =>
            {
                Engine.RestorePoint(Log);
                int n = 0;
                foreach (var t in list)
                {
                    if (Engine.Status(t) == "on") continue;
                    Log(Engine.Apply(t, Log));
                    n++;
                }
                return "Recomendados listos: " + n + " ajuste(s) aplicados.";
            }, InvalidateCards);
        }

        void Momiji()
        {
            var b = Backup.Load();
            var list = Catalog.Tweaks.Where(t => b.ContainsKey(t.Id)).ToList();
            if (list.Count == 0) { Log("No hay ajustes de Hanafuda que deshacer."); return; }
            if (!ConfirmDialog.Ask(this, "Deshacer todo", "Se restauran " + list.Count + " ajustes a como estaban antes de Hanafuda.", "Deshacer")) return;
            RunJob("Deshacer todo", () =>
            {
                foreach (var t in list) Log(Engine.Revert(t, Log));
                return "Listo: " + list.Count + " ajuste(s) restaurados.";
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
                var row = new Row(app.Name, app.Desc, null, check) { Height = 50 };
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
                if (Box.Text.Length == 0 && !Box.Focused) Theme.Draw(e.Graphics, "Buscar app o categoría…", Theme.F(10f), Theme.Faint, new Rectangle(46, 11, Width - 60, 20), TextFormatFlags.Left);
            }
        }

        void RunApps(string mode)
        {
            var selected = Catalog.Apps.Where(a => a.Selected).ToList();
            if (mode != "upgrade" && selected.Count == 0) { Log("Marca al menos una app."); return; }
            var verb = mode == "install" ? "Instalar" : mode == "uninstall" ? "Desinstalar" : "Actualizar";
            var text = mode == "upgrade" ? "WinGet actualiza todas las apps que tengan versión nueva." : verb + ": " + string.Join(", ", selected.Select(a => a.Name)) + ".";
            if (!ConfirmDialog.Ask(this, verb + " con WinGet", text + " Descarga desde internet y acepta los acuerdos de cada paquete.", verb)) return;
            const string filter = " 2>&1 | Out-String -Stream | Where-Object { $_ -match '[A-Za-z0-9]' -and $_ -notmatch '^\\s*[-\\\\|/█▒ ]+\\s*$' }";
            const string flags = " --silent --accept-package-agreements --accept-source-agreements --disable-interactivity";
            RunJob(verb, () =>
            {
                if (mode == "upgrade")
                {
                    Engine.RunScript("winget upgrade --all --include-unknown" + flags + filter, Log);
                    return "Actualización terminada.";
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
                return verb + ": " + ok + " de " + selected.Count + " terminaron bien.";
            }, null);
        }

        // ------------------------------------------------------------ limpieza
        List<KeyValuePair<CleanTarget, Row>> cleanRows;

        void BuildClean(Panel page)
        {
            cleanRows = new List<KeyValuePair<CleanTarget, Row>>();
            Add(page, new SectionTitle("Qué se limpia") { Tag = "head" });
            foreach (var t in Cleaner.Targets())
            {
                var check = new Check { Checked = t.On };
                var target = t;
                check.CheckedChanged += delegate { target.On = check.Checked; };
                var row = new Row(t.Name, t.Desc, null, check);
                row.Tags.Add(t.Recycle ? "SIN DESHACER" : "…");
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
            if (chosen.Count == 0) { Log("No hay nada marcado."); return; }
            if (!ConfirmDialog.Ask(this, "Limpiar", "Se borran " + chosen.Count + " grupo(s) de archivos. No se puede deshacer. Cierra los navegadores para liberar más.", "Limpiar")) return;
            RunJob("Limpieza", () =>
            {
                long freed = chosen.Sum(t => Cleaner.Run(t));
                return (Engine.Dry ? "[simulación] " : "") + "Liberado: " + Cleaner.Format(freed);
            }, MeasureClean);
        }

        // ------------------------------------------------------------ inicio de Windows
        void BuildStartup(Panel page)
        {
            Add(page, new SectionTitle("Arrancan con Windows") { Tag = "head" });
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
                        Log((Engine.Dry ? "[simulación] " : "") + (it.Enabled ? "Activado: " : "Desactivado: ") + it.Name);
                    }
                    catch (Exception ex) { Log("No se pudo cambiar: " + ex.Message); }
                };
                var row = new Row(item.Name, item.Command, tg);
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
            foreach (var p in pagesDef)
            {
                ShowPage(p[0]);
                var until = DateTime.Now.AddSeconds(40);
                do { Application.DoEvents(); Thread.Sleep(50); } while (pendingStatus > 0 && DateTime.Now < until);
                for (int k = 0; k < 10; k++) { Application.DoEvents(); Thread.Sleep(30); }
                using (var bmp = new Bitmap(Width, Height))
                {
                    DrawToBitmap(bmp, new Rectangle(0, 0, Width, Height));
                    bmp.Save(Path.Combine(dir, (++i).ToString("00") + "-" + p[0] + ".png"));
                }
            }
            File.WriteAllText(Path.Combine(dir, "log.txt"), log.Text);
            Close();
            return 0;
        }
    }
}
