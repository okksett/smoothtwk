// Entrada de HanafudaOpti.exe
//   (sin argumentos)       abre la ventana y pide administrador
//   --dry                  simulacion: no cambia nada
//   --shot <carpeta>       guarda una captura de cada pagina (implica --dry)
//   --selftest <archivo>   pruebas internas; solo escribe en HKCU\Software\HanafudaOptiTest
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Principal;
using System.Text;
using System.Windows.Forms;

namespace Hanafuda
{
    static class Program
    {
        static string Arg(List<string> args, string name)
        {
            int i = args.IndexOf(name);
            return i >= 0 && i + 1 < args.Count ? args[i + 1] : null;
        }

        static bool IsAdmin()
        {
            return new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator);
        }

        [STAThread]
        static int Main(string[] argv)
        {
            var args = argv.ToList();
            Engine.Dry = args.Contains("--dry");
            var self = Arg(args, "--selftest");
            var shot = Arg(args, "--shot");
            if (self != null) return SelfTest.Run(self);
            try { Catalog.Load(); }
            catch (Exception ex) { MessageBox.Show("No se pudo cargar el catálogo: " + ex.Message, "Hanafuda Opti"); return 2; }
            if (shot != null) Engine.Dry = true;
            if (!Engine.Dry && !IsAdmin())
            {
                try { Process.Start(new ProcessStartInfo(Application.ExecutablePath, string.Join(" ", argv.Select(a => "\"" + a + "\""))) { Verb = "runas", UseShellExecute = true }); }
                catch (Win32Exception) { MessageBox.Show("Hanafuda Opti necesita permisos de administrador para cambiar ajustes del sistema.", "Hanafuda Opti"); }
                return 0;
            }
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            var form = new MainForm();
            if (shot != null) return form.Shoot(shot);
            Application.Run(form);
            return 0;
        }
    }

    static class SelfTest
    {
        static readonly StringBuilder Out = new StringBuilder();
        static int fails;
        static void Check(string name, bool ok) { Out.AppendLine((ok ? "ok    " : "FALLO ") + name); if (!ok) fails++; }

        public static int Run(string file)
        {
            var key = "HKCU\\Software\\HanafudaOptiTest";
            Backup.FilePath = Path.Combine(Path.GetTempPath(), "hanafuda-selftest-" + Guid.NewGuid().ToString("N") + ".json");
            try { Body(key); }
            catch (Exception ex) { Check("excepción: " + ex, false); }
            finally
            {
                try { Microsoft.Win32.RegistryKey.OpenBaseKey(Microsoft.Win32.RegistryHive.CurrentUser, Microsoft.Win32.RegistryView.Registry64).DeleteSubKeyTree("Software\\HanafudaOptiTest", false); } catch { }
                try { File.Delete(Backup.FilePath); } catch { }
            }
            Check("clave de prueba eliminada", !Engine.KeyExists(key));
            Out.AppendLine("--- fallos: " + fails);
            File.WriteAllText(file, Out.ToString(), new UTF8Encoding(false));
            return fails;
        }

        static void Body(string key)
        {
            Catalog.Load();
            Check("catálogo: " + Catalog.Tweaks.Count + " ajustes, " + Catalog.Actions.Count + " acciones, " + Catalog.Apps.Count + " apps", Catalog.Tweaks.Count > 80 && Catalog.Actions.Count > 40 && Catalog.Apps.Count > 200);
            var ids = Catalog.Tweaks.Select(t => t.Id).Concat(Catalog.Actions.Select(a => a.Id)).ToList();
            Check("ids únicos", ids.Distinct().Count() == ids.Count);
            var kinds = new[] { "DWord", "String", "QWord", "ExpandString", "Binary", "MultiString" };
            var badReg = Catalog.Tweaks.SelectMany(t => t.Registry.Select(r => new { t, r }))
                .Where(x => !(x.r.Path.StartsWith("HKCU\\") || x.r.Path.StartsWith("HKLM\\") || x.r.Path.StartsWith("HKU\\")) || !kinds.Contains(x.r.Kind)
                    || (x.r.Kind == "DWord" && x.r.Value != null && !IsNumber(x.r.Value))).ToList();
            foreach (var b in badReg.Take(5)) Out.AppendLine("  mal: " + b.t.Id + " " + b.r.Path + " " + b.r.Kind + " " + b.r.Value);
            Check("entradas de registro válidas", badReg.Count == 0);
            Check("servicios válidos", Catalog.Tweaks.SelectMany(t => t.Services).All(s => new[] { "Disabled", "Manual", "Automatic", "AutomaticDelayedStart" }.Contains(s[1])));
            Check("cartas conocidas", Catalog.Tweaks.All(t => new[] { "matsu", "kiku", "fuji", "ume", "sakura", "kiri", "yanagi" }.Contains(t.Card)));
            Check("WinUtil integrado (" + Catalog.Tweaks.Count(t => t.Source == "WinUtil") + " ajustes)", Catalog.Tweaks.Count(t => t.Source == "WinUtil") > 60);
            Check("sin ajuste de Defender en el exe", Catalog.Tweaks.All(t => t.Id != "defender"));
            Check("presets de WinUtil", Catalog.Presets.ContainsKey("Standard") && Catalog.Presets["Standard"].Count > 5);
            Check("ningún script abre WinUtil", Catalog.Tweaks.All(t => (t.ApplyScript + t.UndoScript).IndexOf("christitus.com/win", StringComparison.OrdinalIgnoreCase) < 0)
                && Catalog.Actions.All(a => a.Script.IndexOf("christitus.com/win", StringComparison.OrdinalIgnoreCase) < 0));

            int ac, dc;
            Check("lee powercfg en inglés", Engine.ParsePowerQuery("    Current AC Power Setting Index: 0x00000002\r\n    Current DC Power Setting Index: 0x00000001\r\n", out ac, out dc) && ac == 2 && dc == 1);
            Check("lee powercfg en español", Engine.ParsePowerQuery("    Valor mínimo posible: 0x00000000\r\n    Índice de configuración de corriente alterna actual: 0x00000064\r\n    Índice de configuración de corriente continua actual: 0x00000005\r\n", out ac, out dc) && ac == 100 && dc == 5);
            Check("DWORD 0xFFFFFFFF", Engine.ToDword("4294967295") == -1);

            var fake = new Tweak { Id = "fake", Name = "Fake", Card = "matsu" };
            fake.Registry.Add(new RegEntry { Path = key + "\\Existing", Name = "Keep", Kind = "DWord", Value = "4294967295" });
            fake.Registry.Add(new RegEntry { Path = key + "\\Existing", Name = "Text", Kind = "String", Value = "despues" });
            fake.Registry.Add(new RegEntry { Path = key + "\\Existing", Name = "Gone", Kind = "DWord", Value = null });
            fake.Registry.Add(new RegEntry { Path = key + "\\Creada\\Sub", Name = "", Kind = "String", Value = "" });
            Engine.WriteReg(key + "\\Existing", "Keep", "DWord", "7");
            Engine.WriteReg(key + "\\Existing", "Text", "String", "antes");
            Engine.WriteReg(key + "\\Existing", "Gone", "DWord", "5");
            Check("estado inicial off", Engine.Status(fake) == "off");
            var log = new List<string>();
            Engine.Apply(fake, log.Add);
            Check("aplicado: on", Engine.Status(fake) == "on");
            Check("aplicado: valor borrado", !Engine.ReadReg(key + "\\Existing", "Gone").Exists);
            Check("aplicado: 0xFFFFFFFF", Engine.ReadReg(key + "\\Existing", "Keep").Value == "-1");
            Check("respaldo guardado", Backup.Has("fake"));
            Engine.Apply(fake, log.Add);
            Check("deshacer", Engine.Revert(fake, log.Add).StartsWith("Deshecho"));
            Check("restaura DWORD", Engine.ReadReg(key + "\\Existing", "Keep").Value == "7");
            Check("restaura texto", Engine.ReadReg(key + "\\Existing", "Text").Value == "antes");
            Check("restaura valor borrado", Engine.ReadReg(key + "\\Existing", "Gone").Value == "5");
            Check("quita clave creada", !Engine.KeyExists(key + "\\Creada\\Sub"));
            Check("sin respaldo: mensaje", Engine.Revert(fake, log.Add).StartsWith("Ya estaba"));

            var sw = Stopwatch.StartNew();
            var states = Catalog.Tweaks.Select(t => Engine.Status(t)).ToList();
            Check("estado legible de " + states.Count + " ajustes en " + sw.Elapsed.TotalSeconds.ToString("0.0") + " s (on " + states.Count(s => s == "on") + ")", states.All(s => s == "on" || s == "off" || s == "partial" || s == "na"));
            Check("tarea programada legible", Engine.TaskEnabled("\\Microsoft\\Windows\\Defrag\\ScheduledDefrag").HasValue);
            Check("tarea inexistente = null", !Engine.TaskEnabled("\\Hanafuda\\NoExiste").HasValue);
            Check("plan activo legible", Engine.ActivePlan().Length == 36);

            var outLines = new List<string>();
            Engine.RunScript("Write-Output 'hola ñandú'; Write-Output $sync.winutildir; Invoke-WinUtilExplorerUpdate -action refresh; Write-Output 'fin'", outLines.Add);
            Check("script con preámbulo y acentos", outLines.Contains("hola ñandú") && outLines.Contains("fin") && !outLines.Any(l => l.StartsWith("!")));
            if (!outLines.Contains("fin")) foreach (var l in outLines.Take(6)) Out.AppendLine("  " + l);

            var scripts = new List<KeyValuePair<string, string>>();
            foreach (var t in Catalog.Tweaks) { if (t.ApplyScript.Length > 0) scripts.Add(new KeyValuePair<string, string>(t.Id + ".apply", t.ApplyScript)); if (t.UndoScript.Length > 0) scripts.Add(new KeyValuePair<string, string>(t.Id + ".undo", t.UndoScript)); }
            foreach (var a in Catalog.Actions) scripts.Add(new KeyValuePair<string, string>(a.Id, a.Script));
            var dir = Path.Combine(Path.GetTempPath(), "hanafuda-parse-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            try
            {
                File.WriteAllText(Path.Combine(dir, "_lib.ps1"), Catalog.Lib, new UTF8Encoding(true));
                foreach (var s in scripts) File.WriteAllText(Path.Combine(dir, s.Key + ".ps1"), s.Value, new UTF8Encoding(true));
                var parseLines = new List<string>();
                Engine.RunScript("Get-ChildItem -LiteralPath '" + dir + "' -Filter *.ps1 | ForEach-Object { $e = $null; [void][System.Management.Automation.Language.Parser]::ParseFile($_.FullName, [ref]$null, [ref]$e); if ($e) { 'MAL ' + $_.Name + ': ' + $e[0].Message } }; 'parseados'", parseLines.Add);
                foreach (var l in parseLines.Where(l => l.StartsWith("MAL")).Take(8)) Out.AppendLine("  " + l);
                Check("sintaxis de " + (scripts.Count + 1) + " scripts", parseLines.Contains("parseados") && !parseLines.Any(l => l.StartsWith("MAL")));
            }
            finally { try { Directory.Delete(dir, true); } catch { } }

            var targets = Cleaner.Targets();
            Check("objetivos de limpieza: " + targets.Count, targets.Count == 9);
            Check("rutas de limpieza seguras", targets.SelectMany(t => t.Paths).All(Cleaner.SafePath));
            Check("nunca limpia datos personales", targets.SelectMany(t => t.Paths).All(p => !System.Text.RegularExpressions.Regex.IsMatch(p, "Login Data|Cookies|History|Local Storage|Bookmarks")));
            foreach (var p in new[] { Environment.GetEnvironmentVariable("WINDIR"), Environment.GetEnvironmentVariable("USERPROFILE"), "C:\\", "", "relativa\\x" })
                Check("ruta bloqueada: '" + p + "'", !Cleaner.SafePath(p));
            Check("programas al iniciar: " + Startup.Items().Count, Startup.Items() != null);
            Check("GPU: " + Metrics.Gpu(), Metrics.Gpu().Length > 0);
            Check("Windows: " + Metrics.WindowsName(), Metrics.WindowsName().StartsWith("Windows"));
            Check("equipo: " + (Engine.IsLaptop ? "laptop" : "escritorio") + ", " + Engine.CpuName + ", " + Engine.RamGB.ToString("0") + " GB", Engine.RamGB > 1);
        }

        static bool IsNumber(string s) { long l; return long.TryParse(s, out l); }
    }
}
