// Motor de SmoothTwk: catalogo, registro, servicios, tareas, energia, scripts y respaldo.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;
using Microsoft.Win32;

namespace SmoothTwk
{
    public class RegEntry { public string Path, Name, Kind, Value; }
    public class PowerEntry { public string Sub, Setting; public int? Ac, Dc; }

    public class Tweak
    {
        public string Id, Card, Group, Name, Desc, Help = "", Game = "", Device = "both", Warn = "", Special = "", Guard = "", Source = "SmoothTwk", ApplyScript = "", UndoScript = "";
        public bool Rec, Reboot, OneShot, Download, FiveMConflict;
        public List<RegEntry> Registry = new List<RegEntry>();
        public List<string[]> Services = new List<string[]>();
        public List<string> Tasks = new List<string>();
        public List<PowerEntry> Power = new List<PowerEntry>();
    }

    public class ActionItem { public string Id, Card, Group, Name, Desc, Help = "", Script, Warn = "", Button = "Ejecutar", Source = "SmoothTwk"; public bool Download; }
    public class AppItem { public string Id, Name, Category, Desc, Winget; public bool Selected; }
    public class GuideItem { public string Group, Name, Value, Why; }
    public class GameContent
    {
        public string Id, Name, AntiCheat = "", Tagline = "";
        public List<string> Cautions = new List<string>(), Shared = new List<string>();
        public List<GuideItem> Guide = new List<GuideItem>();
    }

    public static class Json
    {
        public static readonly JavaScriptSerializer Serializer = new JavaScriptSerializer { MaxJsonLength = int.MaxValue, RecursionLimit = 200 };
        public static string S(Dictionary<string, object> d, string k)
        {
            object v;
            if (d != null && d.TryGetValue(k, out v) && v != null) return Convert.ToString(v, CultureInfo.InvariantCulture);
            return null;
        }
        public static bool B(Dictionary<string, object> d, string k)
        {
            object v;
            return d != null && d.TryGetValue(k, out v) && v is bool && (bool)v;
        }
        public static int? I(Dictionary<string, object> d, string k)
        {
            object v;
            if (d != null && d.TryGetValue(k, out v) && v != null) return Convert.ToInt32(v, CultureInfo.InvariantCulture);
            return null;
        }
        public static IEnumerable<object> A(Dictionary<string, object> d, string k)
        {
            object v;
            if (d == null || !d.TryGetValue(k, out v) || v == null) return new object[0];
            var arr = v as object[];
            if (arr != null) return arr;
            var list = v as System.Collections.ArrayList;
            if (list != null) return list.Cast<object>();
            return new[] { v };
        }
        public static IEnumerable<Dictionary<string, object>> D(Dictionary<string, object> d, string k)
        {
            return A(d, k).OfType<Dictionary<string, object>>();
        }
    }

    public static class Catalog
    {
        public static List<Tweak> Tweaks = new List<Tweak>();
        public static List<ActionItem> Actions = new List<ActionItem>();
        public static List<AppItem> Apps = new List<AppItem>();
        public static List<GameContent> Games = new List<GameContent>();
        public static Dictionary<string, List<string>> Presets = new Dictionary<string, List<string>>();
        public static string Lib = "";

        public static string Resource(string name)
        {
            using (var s = Assembly.GetExecutingAssembly().GetManifestResourceStream(name))
            {
                if (s == null) throw new InvalidOperationException("Falta el recurso " + name);
                using (var r = new StreamReader(s, Encoding.UTF8)) return r.ReadToEnd();
            }
        }

        public static void Load()
        {
            Tweaks = new List<Tweak>(); Actions = new List<ActionItem>(); Apps = new List<AppItem>(); Games = new List<GameContent>(); Presets = new Dictionary<string, List<string>>();
            Lib = Resource("lib.ps1");
            var root = (Dictionary<string, object>)Json.Serializer.DeserializeObject(Resource("catalog.json"));
            foreach (var t in Json.D(root, "tweaks"))
            {
                var tw = new Tweak
                {
                    Id = Json.S(t, "id"), Card = Json.S(t, "card"), Group = Json.S(t, "group") ?? "", Name = Json.S(t, "name"), Desc = Json.S(t, "desc") ?? "",
                    Rec = Json.B(t, "rec"), Device = Json.S(t, "device") ?? "both", Reboot = Json.B(t, "reboot"), Warn = Json.S(t, "warn") ?? "",
                    OneShot = Json.B(t, "oneshot"), Special = Json.S(t, "special") ?? "", Guard = Json.S(t, "guard") ?? "", Source = Json.S(t, "source") ?? "SmoothTwk",
                    Download = Json.B(t, "download"), ApplyScript = Json.S(t, "apply") ?? "", UndoScript = Json.S(t, "undo") ?? "", Help = Json.S(t, "help") ?? "", Game = Json.S(t, "game") ?? ""
                };
                foreach (var r in Json.D(t, "registry"))
                    tw.Registry.Add(new RegEntry { Path = Json.S(r, "path"), Name = Json.S(r, "name") ?? "", Kind = Json.S(r, "type"), Value = Json.S(r, "value") });
                foreach (var s in Json.D(t, "services")) tw.Services.Add(new[] { Json.S(s, "name"), Json.S(s, "start") });
                foreach (var s in Json.A(t, "tasks")) tw.Tasks.Add(Convert.ToString(s));
                foreach (var p in Json.D(t, "power"))
                    tw.Power.Add(new PowerEntry { Sub = Json.S(p, "sub"), Setting = Json.S(p, "setting"), Ac = Json.I(p, "ac"), Dc = Json.I(p, "dc") });
                Tweaks.Add(tw);
            }
            foreach (var a in Json.D(root, "actions"))
                Actions.Add(new ActionItem
                {
                    Id = Json.S(a, "id"), Card = Json.S(a, "card"), Group = Json.S(a, "group"), Name = Json.S(a, "name"), Desc = Json.S(a, "desc") ?? "",
                    Script = Json.S(a, "script") ?? "", Warn = Json.S(a, "warn") ?? "", Button = Json.S(a, "button") ?? "Ejecutar",
                    Download = Json.B(a, "download"), Source = Json.S(a, "source") ?? "SmoothTwk", Help = Json.S(a, "help") ?? ""
                });
            foreach (var a in Json.D(root, "apps"))
                Apps.Add(new AppItem { Id = Json.S(a, "id"), Name = Json.S(a, "name"), Category = Json.S(a, "category"), Desc = Json.S(a, "desc") ?? "", Winget = Json.S(a, "winget") });
            foreach (var g in Json.D(root, "games"))
            {
                var gc = new GameContent { Id = Json.S(g, "id"), Name = Json.S(g, "name"), AntiCheat = Json.S(g, "anticheat") ?? "", Tagline = Json.S(g, "tagline") ?? "" };
                gc.Cautions.AddRange(Json.A(g, "cautions").Select(x => Convert.ToString(x)));
                gc.Shared.AddRange(Json.A(g, "shared").Select(x => Convert.ToString(x)));
                foreach (var i in Json.D(g, "guide"))
                    gc.Guide.Add(new GuideItem { Group = Json.S(i, "group"), Name = Json.S(i, "name"), Value = Json.S(i, "value"), Why = Json.S(i, "why") ?? "" });
                Games.Add(gc);
            }
            // Ajustes que apagan algo de lo que revisa el PC check de los servidores de FiveM.
            var critical = new HashSet<string>(Engine.FiveMServices, StringComparer.OrdinalIgnoreCase);
            foreach (var t in Tweaks)
                t.FiveMConflict = t.Card != "fivem" && (t.Services.Any(s => critical.Contains(s[0]) && s[1] == "Disabled")
                    || t.Registry.Any(r => r.Name == "EnablePrefetcher" || r.Name == "DisablePCA" || r.Path.EndsWith("\\Services\\bam", StringComparison.OrdinalIgnoreCase)));
            object pre;
            if (root.TryGetValue("presets", out pre) && pre is Dictionary<string, object>)
                foreach (var kv in (Dictionary<string, object>)pre)
                    Presets[kv.Key] = Json.A((Dictionary<string, object>)pre, kv.Key).Select(x => Convert.ToString(x)).ToList();
            L.ApplyToCatalog();
        }
    }

    public static class Backup
    {
        public static string FilePath = Path.Combine(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "SmoothTwk"), "backup-exe.json");
        static readonly object Gate = new object();

        public static Dictionary<string, object> Load()
        {
            lock (Gate)
            {
                if (!File.Exists(FilePath)) return new Dictionary<string, object>();
                var o = Json.Serializer.DeserializeObject(File.ReadAllText(FilePath, Encoding.UTF8)) as Dictionary<string, object>;
                return o ?? new Dictionary<string, object>();
            }
        }

        public static void Save(Dictionary<string, object> data)
        {
            if (Engine.Dry) return;
            lock (Gate)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
                var tmp = FilePath + ".tmp";
                File.WriteAllText(tmp, Json.Serializer.Serialize(data), new UTF8Encoding(false));
                if (File.Exists(FilePath)) File.Delete(FilePath);
                File.Move(tmp, FilePath);
            }
        }

        public static bool Has(string id) { return Load().ContainsKey(id); }

        // Nombres anteriores del programa, del más nuevo al más viejo.
        public static readonly string[] OldNames = { "ZeroLag", "HanafudaOpti" };

        // Trae el respaldo guardado con un nombre anterior para que «Deshacer todo» siga funcionando.
        public static void MigrateFromOldName()
        {
            try
            {
                if (File.Exists(FilePath) || Engine.Dry) return;
                foreach (var name in OldNames)
                {
                    var old = Path.Combine(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), name), "backup-exe.json");
                    if (!File.Exists(old)) continue;
                    Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
                    File.Copy(old, FilePath);
                    return;
                }
            }
            catch { }
        }
    }

    public class RegState { public bool KeyExisted, Exists; public string Kind, Value; }

    public static class Engine
    {
        public static bool Dry;
        public static bool Limited;
        public static Action<string> Log = delegate { };
        public static bool RebootPending;
        public const string HighPerf = "8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c";
        public const string Balanced = "381b4222-f694-41f0-9685-ff5bb260df2e";
        public const string Ultimate = "e9a42b02-d5df-448d-aa00-03f14749eb61";
        public const string OverlayBest = "ded574b5-45a0-4f42-8737-46345c09c238";
        public const string SmoothPlan = "8070b076-188f-428d-a9d6-e1a2f99fad5d";   // GUID que trae SmoothTwk.pow
        const string Guid = "[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}";

        // ------------------------------------------------------------ equipo
        static bool? laptop;
        public static bool IsLaptop
        {
            get
            {
                if (!laptop.HasValue) laptop = System.Windows.Forms.SystemInformation.PowerStatus.BatteryChargeStatus != System.Windows.Forms.BatteryChargeStatus.NoSystemBattery;
                return laptop.Value;
            }
        }

        public static string CpuName
        {
            get { var s = ReadReg("HKLM\\HARDWARE\\DESCRIPTION\\System\\CentralProcessor\\0", "ProcessorNameString"); return s.Exists ? s.Value.Trim() : "CPU"; }
        }

        [StructLayout(LayoutKind.Sequential)]
        public class MemoryStatus
        {
            public uint Length = (uint)Marshal.SizeOf(typeof(MemoryStatus));
            public uint Load;
            public ulong TotalPhys, AvailPhys, TotalPageFile, AvailPageFile, TotalVirtual, AvailVirtual, AvailExtendedVirtual;
        }
        [DllImport("kernel32.dll")] [return: MarshalAs(UnmanagedType.Bool)] public static extern bool GlobalMemoryStatusEx([In, Out] MemoryStatus buffer);

        public static double RamGB
        {
            get { var m = new MemoryStatus(); GlobalMemoryStatusEx(m); return m.TotalPhys / 1073741824.0; }
        }

        public static bool DeviceMatches(Tweak t)
        {
            if (t.Device == "desktop") return !IsLaptop;
            if (t.Device == "laptop") return IsLaptop;
            return true;
        }

        // ------------------------------------------------------------ FiveM
        public static readonly string[] FiveMServices = { "PcaSvc", "DPS", "DiagTrack", "SysMain", "EventLog", "Appinfo", "BFE", "bam" };
        public static readonly string[] FiveMExtras = { "CDPSvc", "DusmSvc", "SSDPSRV", "UmRdpService", "RemoteRegistry" };
        static bool? fivemGuard;

        // Protege el PC check: impide aplicar ajustes que apagan esos servicios. Encendida por defecto.
        public static bool FiveMGuard
        {
            get
            {
                if (!fivemGuard.HasValue)
                {
                    var s = ReadReg("HKCU\\Software\\SmoothTwk", "FiveMGuard");
                    foreach (var name in Backup.OldNames) { if (s.Exists) break; s = ReadReg("HKCU\\Software\\" + name, "FiveMGuard"); }   // nombres anteriores
                    fivemGuard = !s.Exists || s.Value != "0";
                }
                return fivemGuard.Value;
            }
            set
            {
                fivemGuard = value;
                if (!Dry) WriteReg("HKCU\\Software\\SmoothTwk", "FiveMGuard", "DWord", value ? "1" : "0");
            }
        }

        public class SvcInfo { public string Name, Display = "", Start = "", Status = ""; public bool Exists, Running, Disabled; }

        public static SvcInfo ReadService(string name)
        {
            var info = new SvcInfo { Name = name };
            var start = ReadReg("HKLM\\SYSTEM\\CurrentControlSet\\Services\\" + name, "Start");
            var delayed = ReadReg("HKLM\\SYSTEM\\CurrentControlSet\\Services\\" + name, "DelayedAutostart");
            if (start.Exists)
            {
                int v = ToDword(start.Value);
                info.Disabled = v == 4;
                info.Start = v == 0 ? L.T("Arranque", "Boot") : v == 1 ? L.T("Sistema", "System") : v == 2 ? (delayed.Exists && delayed.Value == "1" ? L.T("Automático (retrasado)", "Automatic (delayed)") : L.T("Automático", "Automatic")) : v == 3 ? "Manual" : L.T("Desactivado", "Disabled");
            }
            try
            {
                using (var sc = new System.ServiceProcess.ServiceController(name))
                {
                    info.Display = sc.DisplayName;
                    var st = sc.Status;
                    info.Exists = true;
                    info.Running = st == System.ServiceProcess.ServiceControllerStatus.Running;
                    info.Status = info.Running ? L.T("En ejecución", "Running") : st == System.ServiceProcess.ServiceControllerStatus.Stopped ? L.T("Detenido", "Stopped") : st.ToString();
                }
            }
            catch (InvalidOperationException) { info.Exists = start.Exists; info.Status = start.Exists ? L.T("Sin acceso", "No access") : L.T("No existe", "Missing"); }
            return info;
        }

        public static string GuardReason(Tweak t)
        {
            if (t.Game.Length > 0 && !GameLibrary.Find(t.Game).Installed)
            {
                var def = GameLibrary.Def(t.Game);
                return L.T("No encontré " + (def != null ? def.Name : t.Game) + " instalado en esta PC.", (def != null ? def.Name : t.Game) + " is not installed on this PC.");
            }
            if (t.FiveMConflict && FiveMGuard) return L.T("Bloqueado por la protección de FiveM: este ajuste apaga algo que los servidores revisan en el PC check. Apaga la protección en el apartado FiveM si no juegas FiveM.",
                "Blocked by FiveM protection: this tweak turns off something servers check in the PC check. Turn the protection off in the FiveM page if you don't play FiveM.");
            if (t.Guard == "x3d" && CpuName.IndexOf("X3D", StringComparison.OrdinalIgnoreCase) >= 0) return L.T("Bloqueado en Ryzen X3D: baja FPS en esos procesadores.", "Blocked on Ryzen X3D: it lowers FPS on those CPUs.");
            if (t.Guard == "ram32" && RamGB < 31) return L.T("Necesita 32 GB de RAM o más.", "Needs 32 GB of RAM or more.");
            return null;
        }

        // ------------------------------------------------------------ registro
        static RegistryKey Root(string hive)
        {
            switch (hive)
            {
                case "HKCU": return RegistryKey.OpenBaseKey(RegistryHive.CurrentUser, RegistryView.Registry64);
                case "HKLM": return RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
                case "HKU": return RegistryKey.OpenBaseKey(RegistryHive.Users, RegistryView.Registry64);
            }
            throw new ArgumentException("Colmena no soportada: " + hive);
        }

        static void Split(string path, out RegistryKey root, out string sub)
        {
            int i = path.IndexOf('\\');
            root = Root(path.Substring(0, i));
            sub = path.Substring(i + 1);
        }

        public static RegState ReadReg(string path, string name)
        {
            RegistryKey root; string sub;
            Split(path, out root, out sub);
            using (root)
            using (var k = root.OpenSubKey(sub))
            {
                if (k == null) return new RegState();
                if (!k.GetValueNames().Contains(name, StringComparer.OrdinalIgnoreCase)) return new RegState { KeyExisted = true };
                var kind = k.GetValueKind(name);
                var v = k.GetValue(name, null, RegistryValueOptions.DoNotExpandEnvironmentNames);
                return new RegState { KeyExisted = true, Exists = true, Kind = kind.ToString(), Value = ValueToString(kind, v) };
            }
        }

        static string ValueToString(RegistryValueKind kind, object v)
        {
            if (v == null) return "";
            switch (kind)
            {
                case RegistryValueKind.DWord: return ((int)v).ToString(CultureInfo.InvariantCulture);
                case RegistryValueKind.QWord: return ((long)v).ToString(CultureInfo.InvariantCulture);
                case RegistryValueKind.MultiString: return string.Join("\0", (string[])v);
                case RegistryValueKind.Binary: return Convert.ToBase64String((byte[])v);
            }
            return Convert.ToString(v, CultureInfo.InvariantCulture);
        }

        public static int ToDword(string s)
        {
            long l = long.Parse(s.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture);
            return unchecked((int)l);
        }

        public static void WriteReg(string path, string name, string kind, string value)
        {
            if (Dry) { Log("[simulación] registro " + path + " [" + name + "] = " + value); return; }
            RegistryKey root; string sub;
            Split(path, out root, out sub);
            using (root)
            using (var k = root.CreateSubKey(sub))
            {
                var vk = (RegistryValueKind)Enum.Parse(typeof(RegistryValueKind), kind, true);
                switch (vk)
                {
                    case RegistryValueKind.DWord: k.SetValue(name, ToDword(value), vk); break;
                    case RegistryValueKind.QWord: k.SetValue(name, long.Parse(value, CultureInfo.InvariantCulture), vk); break;
                    case RegistryValueKind.MultiString: k.SetValue(name, value.Split('\0'), vk); break;
                    case RegistryValueKind.Binary: k.SetValue(name, Convert.FromBase64String(value), vk); break;
                    default: k.SetValue(name, value ?? "", vk); break;
                }
            }
        }

        public static void DeleteReg(string path, string name)
        {
            if (Dry) { Log("[simulación] borrar " + path + " [" + name + "]"); return; }
            RegistryKey root; string sub;
            Split(path, out root, out sub);
            using (root)
            using (var k = root.OpenSubKey(sub, true))
                if (k != null) k.DeleteValue(name, false);
        }

        public static void DeleteKeyIfEmpty(string path)
        {
            if (Dry) return;
            RegistryKey root; string sub;
            Split(path, out root, out sub);
            using (root)
            {
                bool empty;
                using (var k = root.OpenSubKey(sub))
                {
                    if (k == null) return;
                    empty = k.SubKeyCount == 0 && k.ValueCount == 0;
                }
                if (empty) root.DeleteSubKey(sub, false);
            }
        }

        public static bool KeyExists(string path)
        {
            RegistryKey root; string sub;
            Split(path, out root, out sub);
            using (root)
            using (var k = root.OpenSubKey(sub)) return k != null;
        }

        // ------------------------------------------------------------ procesos
        public static int Run(string file, string args, out string output, int timeoutMs = 600000)
        {
            var psi = new ProcessStartInfo(file, args)
            {
                UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true
            };
            using (var p = Process.Start(psi))
            {
                var err = p.StandardError.ReadToEndAsync();
                output = p.StandardOutput.ReadToEnd();
                if (!p.WaitForExit(timeoutMs)) { try { p.Kill(); } catch { } output += "\n(tiempo agotado)"; return -1; }
                output += err.Result;
                return p.ExitCode;
            }
        }

        public static void RunCmd(string file, string args)
        {
            if (Dry) { Log("[simulación] " + file + " " + args); return; }
            string o;
            int code = Run(file, args, out o);
            if (code != 0) throw new InvalidOperationException(file + " " + args + " falló (" + code + "): " + o.Trim());
        }

        public static int RunScript(string script, Action<string> log)
        {
            if (string.IsNullOrWhiteSpace(script)) return 0;
            if (Dry)
            {
                var first = script.Trim().Split('\n')[0].Trim();
                log("[simulación] script: " + (first.Length > 90 ? first.Substring(0, 90) + "…" : first));
                return 0;
            }
            var file = Path.Combine(Path.GetTempPath(), "smoothtwk-" + System.Guid.NewGuid().ToString("N") + ".ps1");
            File.WriteAllText(file, Catalog.Lib + "\r\n" + script + "\r\n", new UTF8Encoding(true));
            try
            {
                var psi = new ProcessStartInfo("powershell.exe", "-NoProfile -ExecutionPolicy Bypass -File \"" + file + "\"")
                {
                    UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true,
                    StandardOutputEncoding = Encoding.UTF8, StandardErrorEncoding = Encoding.UTF8
                };
                using (var p = new Process { StartInfo = psi })
                {
                    p.OutputDataReceived += (s, e) => { if (!string.IsNullOrWhiteSpace(e.Data)) log(e.Data); };
                    p.ErrorDataReceived += (s, e) => { if (!string.IsNullOrWhiteSpace(e.Data)) log("! " + e.Data); };
                    p.Start();
                    p.BeginOutputReadLine();
                    p.BeginErrorReadLine();
                    p.WaitForExit();
                    return p.ExitCode;
                }
            }
            finally { try { File.Delete(file); } catch { } }
        }

        // ------------------------------------------------------------ tareas programadas
        public static bool? TaskEnabled(string task)
        {
            string o;
            if (Run("schtasks.exe", "/Query /TN \"" + task + "\" /XML", out o, 30000) != 0) return null;
            int a = o.IndexOf("<Settings>", StringComparison.Ordinal), b = o.IndexOf("</Settings>", StringComparison.Ordinal);
            var settings = a >= 0 && b > a ? o.Substring(a, b - a) : o;
            return settings.IndexOf("<Enabled>false</Enabled>", StringComparison.OrdinalIgnoreCase) < 0;
        }

        public static void SetTask(string task, bool enable)
        {
            RunCmd("schtasks.exe", "/Change /TN \"" + task + "\" " + (enable ? "/Enable" : "/Disable"));
        }

        // ------------------------------------------------------------ energia
        public static string ActivePlan()
        {
            string o;
            Run("powercfg.exe", "/getactivescheme", out o, 30000);
            var m = Regex.Match(o, Guid);
            return m.Success ? m.Value.ToLowerInvariant() : "";
        }

        public static bool ParsePowerQuery(string output, out int ac, out int dc)
        {
            var hex = Regex.Matches(output, @":\s*0x([0-9a-fA-F]+)\s*$", RegexOptions.Multiline);
            ac = dc = 0;
            if (hex.Count < 2) return false;
            ac = Convert.ToInt32(hex[hex.Count - 2].Groups[1].Value, 16);
            dc = Convert.ToInt32(hex[hex.Count - 1].Groups[1].Value, 16);
            return true;
        }

        public static bool QueryPower(PowerEntry p, out int ac, out int dc)
        {
            string o;
            Run("powercfg.exe", "/qh SCHEME_CURRENT " + p.Sub + " " + p.Setting, out o, 30000);
            return ParsePowerQuery(o, out ac, out dc);
        }

        static void SetPower(PowerEntry p, int? ac, int? dc)
        {
            if (ac.HasValue) RunCmd("powercfg.exe", "/setacvalueindex SCHEME_CURRENT " + p.Sub + " " + p.Setting + " " + ac.Value);
            if (dc.HasValue) RunCmd("powercfg.exe", "/setdcvalueindex SCHEME_CURRENT " + p.Sub + " " + p.Setting + " " + dc.Value);
            RunCmd("powercfg.exe", "/setactive SCHEME_CURRENT");
        }

        // ------------------------------------------------------------ entradas de un ajuste
        public static List<RegEntry> Entries(Tweak t)
        {
            var list = new List<RegEntry>(t.Registry);
            foreach (var s in t.Services)
            {
                var path = "HKLM\\SYSTEM\\CurrentControlSet\\Services\\" + s[0];
                if (!KeyExists(path)) continue;
                string start = s[1] == "Disabled" ? "4" : s[1] == "Manual" ? "3" : "2";
                list.Add(new RegEntry { Path = path, Name = "Start", Kind = "DWord", Value = start });
                if (s[1] == "AutomaticDelayedStart") list.Add(new RegEntry { Path = path, Name = "DelayedAutostart", Kind = "DWord", Value = "1" });
            }
            if (t.Special == "game-gpu" || t.Special == "game-fso") list.AddRange(GameLibrary.ExeEntries(t.Special, t.Game));
            if (t.Special == "dx-windowed")
            {
                const string path = "HKCU\\Software\\Microsoft\\DirectX\\UserGpuPreferences";
                var cur = ReadReg(path, "DirectXUserGlobalSettings");
                var tokens = (cur.Exists ? cur.Value : "").Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries)
                    .Where(x => !x.StartsWith("SwapEffectUpgradeEnable=", StringComparison.OrdinalIgnoreCase)).ToList();
                tokens.Add("SwapEffectUpgradeEnable=1");
                list.Add(new RegEntry { Path = path, Name = "DirectXUserGlobalSettings", Kind = "String", Value = string.Join(";", tokens) + ";" });
            }
            return list;
        }

        static bool EntryApplied(RegEntry e)
        {
            var s = ReadReg(e.Path, e.Name);
            if (e.Value == null) return !s.Exists;
            if (!s.Exists) return false;
            if (string.Equals(e.Kind, "DWord", StringComparison.OrdinalIgnoreCase))
                return s.Kind == "DWord" && ToDword(s.Value) == ToDword(e.Value);
            return string.Equals(s.Value, e.Value, StringComparison.Ordinal);
        }

        // on, off, partial, na
        public static string Status(Tweak t)
        {
            try
            {
                // Bloqueado solo si no se aplico antes: lo ya aplicado se puede seguir deshaciendo.
                if (GuardReason(t) != null && !Backup.Has(t.Id)) return "na";
                if (t.OneShot) return "off";
                switch (t.Special)
                {
                    case "plan-high": return ActivePlan() == HighPerf ? "on" : "off";
                    case "plan-smoothtwk": return ActivePlan() == SmoothPlan ? "on" : "off";
                    case "hibernate-off":
                        var h = ReadReg("HKLM\\SYSTEM\\CurrentControlSet\\Control\\Power", "HibernateEnabled");
                        return h.Exists && h.Value == "0" ? "on" : "off";
                    case "overlay-best":
                        var ov = ReadReg("HKLM\\SYSTEM\\CurrentControlSet\\Control\\Power\\User\\PowerSchemes", "ActiveOverlayAcPowerScheme");
                        return ov.Exists && string.Equals(ov.Value, OverlayBest, StringComparison.OrdinalIgnoreCase) ? "on" : "off";
                    case "plan-ultimate":
                        var rec = Backup.Load();
                        object r;
                        if (!rec.TryGetValue(t.Id, out r)) return "off";
                        var custom = Json.S(r as Dictionary<string, object>, "custom") ?? "";
                        var parts = custom.Split('|');
                        return parts.Length == 2 && ActivePlan() == parts[1] ? "on" : "partial";
                    case "memcomp-off": return Backup.Has(t.Id) ? "on" : "off";
                }
                int total = 0, hits = 0;
                foreach (var e in Entries(t)) { total++; if (EntryApplied(e)) hits++; }
                foreach (var task in t.Tasks)
                {
                    var en = TaskEnabled(task);
                    if (!en.HasValue) continue;
                    total++;
                    if (!en.Value) hits++;
                }
                foreach (var p in t.Power)
                {
                    int ac, dc;
                    if (!QueryPower(p, out ac, out dc)) continue;
                    total++;
                    if ((!p.Ac.HasValue || p.Ac.Value == ac) && (!p.Dc.HasValue || p.Dc.Value == dc)) hits++;
                }
                if (total == 0)
                {
                    if (!string.IsNullOrEmpty(t.ApplyScript)) return Backup.Has(t.Id) ? "on" : "off";
                    return "na";
                }
                if (hits == 0) return "off";
                return hits == total ? "on" : "partial";
            }
            catch (Exception ex)
            {
                Log(L.T("No se pudo leer ", "Could not read ") + t.Name + ": " + ex.Message);
                return "off";
            }
        }

        // ------------------------------------------------------------ aplicar y deshacer
        public static string Apply(Tweak t, Action<string> log)
        {
            if (GuardReason(t) != null) return GuardReason(t);
            if (t.OneShot)
            {
                int code = RunScript(t.ApplyScript, log);
                return code == 0 ? L.T("Hecho: ", "Done: ") + t.Name : L.T("Terminó con error (", "Finished with error (") + code + "): " + t.Name;
            }
            int fails = 0;
            var entries = Entries(t);
            var backup = Backup.Load();
            if (!backup.ContainsKey(t.Id))
            {
                var saved = new List<object>();
                foreach (var e in entries)
                {
                    var s = ReadReg(e.Path, e.Name);
                    saved.Add(new Dictionary<string, object> { { "path", e.Path }, { "name", e.Name }, { "keyExisted", s.KeyExisted }, { "exists", s.Exists }, { "kind", s.Kind }, { "value", s.Value } });
                }
                var tasks = new List<object>();
                foreach (var task in t.Tasks) { var en = TaskEnabled(task); if (en.HasValue && en.Value) tasks.Add(task); }
                var power = new List<object>();
                foreach (var p in t.Power)
                {
                    int ac, dc;
                    if (QueryPower(p, out ac, out dc)) power.Add(new Dictionary<string, object> { { "sub", p.Sub }, { "setting", p.Setting }, { "ac", ac }, { "dc", dc } });
                }
                string custom = "";
                if (t.Special == "plan-high" || t.Special == "plan-ultimate" || t.Special == "plan-smoothtwk") custom = ActivePlan();
                if (t.Special == "overlay-best")
                {
                    var ov = ReadReg("HKLM\\SYSTEM\\CurrentControlSet\\Control\\Power\\User\\PowerSchemes", "ActiveOverlayAcPowerScheme");
                    custom = ov.Exists ? ov.Value : "00000000-0000-0000-0000-000000000000";
                }
                backup[t.Id] = new Dictionary<string, object> { { "entries", saved }, { "tasks", tasks }, { "power", power }, { "custom", custom }, { "when", DateTime.Now.ToString("s") } };
                Backup.Save(backup);
            }
            foreach (var e in entries)
            {
                try { if (e.Value == null) DeleteReg(e.Path, e.Name); else WriteReg(e.Path, e.Name, e.Kind, e.Value); }
                catch (Exception ex) { fails++; log("! " + e.Path + " [" + e.Name + "]: " + ex.Message); }
            }
            foreach (var task in t.Tasks)
            {
                try { if (TaskEnabled(task).HasValue) SetTask(task, false); }
                catch (Exception ex) { fails++; log("! " + ex.Message); }
            }
            foreach (var p in t.Power)
            {
                try { SetPower(p, p.Ac, p.Dc); }
                catch (Exception ex) { fails++; log("! " + ex.Message); }
            }
            try { ApplySpecial(t, log); }
            catch (Exception ex) { fails++; log("! " + ex.Message); }
            if (!string.IsNullOrEmpty(t.ApplyScript) && RunScript(t.ApplyScript, log) != 0) fails++;
            if (t.Reboot) RebootPending = true;
            return Result(L.T("Aplicado", "Applied"), t, fails);
        }

        static void ApplySpecial(Tweak t, Action<string> log)
        {
            switch (t.Special)
            {
                case "plan-high": RunCmd("powercfg.exe", "/setactive " + HighPerf); break;
                case "plan-smoothtwk":
                    if (Dry) { Log("[simulación] powercfg /import SmoothTwk.pow " + SmoothPlan); break; }
                    string list;
                    Run("powercfg.exe", "/list", out list, 30000);
                    if (list.IndexOf(SmoothPlan, StringComparison.OrdinalIgnoreCase) < 0)
                    {
                        var pow = Path.Combine(Path.GetTempPath(), "SmoothTwk-" + System.Guid.NewGuid().ToString("N") + ".pow");
                        using (var src = Assembly.GetExecutingAssembly().GetManifestResourceStream("SmoothTwk.pow"))
                        using (var dst = File.Create(pow)) src.CopyTo(dst);
                        try { RunCmd("powercfg.exe", "/import \"" + pow + "\" " + SmoothPlan); }
                        finally { try { File.Delete(pow); } catch { } }
                    }
                    RunCmd("powercfg.exe", "/setactive " + SmoothPlan);
                    break;
                case "hibernate-off": RunCmd("powercfg.exe", "/hibernate off"); break;
                case "memcomp-off": RunScript("Disable-MMAgent -MemoryCompression", log); break;
                case "overlay-best":
                    if (!Dry && ActivePlan() != Balanced) throw new InvalidOperationException(L.T("Activa primero el plan Equilibrado; este modo solo existe ahí.", "Switch to the Balanced plan first; this mode only exists there."));
                    RunCmd("powercfg.exe", "/overlaysetactive " + OverlayBest);
                    break;
                case "plan-ultimate":
                    if (Dry) { Log("[simulación] powercfg /duplicatescheme " + Ultimate); break; }
                    string o;
                    Run("powercfg.exe", "/duplicatescheme " + Ultimate, out o, 30000);
                    var m = Regex.Match(o, Guid);
                    if (!m.Success) throw new InvalidOperationException("Windows no creó el plan Máximo rendimiento.");
                    var guid = m.Value.ToLowerInvariant();
                    RunCmd("powercfg.exe", "/setactive " + guid);
                    var b = Backup.Load();
                    var rec = b[t.Id] as Dictionary<string, object>;
                    rec["custom"] = Json.S(rec, "custom") + "|" + guid;
                    Backup.Save(b);
                    break;
            }
        }

        public static string Revert(Tweak t, Action<string> log)
        {
            var backup = Backup.Load();
            object raw;
            if (!backup.TryGetValue(t.Id, out raw)) return L.T("Ya estaba así antes de SmoothTwk: no hay valor anterior que restaurar.", "It was already like this before SmoothTwk: nothing to restore.");
            var rec = (Dictionary<string, object>)raw;
            int fails = 0;
            var saved = Json.D(rec, "entries").ToList();
            for (int i = saved.Count - 1; i >= 0; i--)
            {
                var e = saved[i];
                try
                {
                    if (Json.B(e, "exists")) WriteReg(Json.S(e, "path"), Json.S(e, "name") ?? "", Json.S(e, "kind"), Json.S(e, "value") ?? "");
                    else
                    {
                        DeleteReg(Json.S(e, "path"), Json.S(e, "name") ?? "");
                        if (!Json.B(e, "keyExisted")) DeleteKeyIfEmpty(Json.S(e, "path"));
                    }
                }
                catch (Exception ex) { fails++; log("! " + ex.Message); }
            }
            foreach (var task in Json.A(rec, "tasks"))
            {
                try { SetTask(Convert.ToString(task), true); } catch (Exception ex) { fails++; log("! " + ex.Message); }
            }
            foreach (var p in Json.D(rec, "power"))
            {
                try { SetPower(new PowerEntry { Sub = Json.S(p, "sub"), Setting = Json.S(p, "setting") }, Json.I(p, "ac"), Json.I(p, "dc")); }
                catch (Exception ex) { fails++; log("! " + ex.Message); }
            }
            var custom = Json.S(rec, "custom") ?? "";
            try
            {
                switch (t.Special)
                {
                    case "plan-high": if (custom.Length > 0) RunCmd("powercfg.exe", "/setactive " + custom); break;
                    case "plan-smoothtwk":
                        RunCmd("powercfg.exe", "/setactive " + (custom.Length > 0 && custom != SmoothPlan ? custom : Balanced));
                        RunCmd("powercfg.exe", "/delete " + SmoothPlan);
                        break;
                    case "hibernate-off": RunCmd("powercfg.exe", "/hibernate on"); break;
                    case "memcomp-off": RunScript("Enable-MMAgent -MemoryCompression", log); break;
                    case "overlay-best": RunCmd("powercfg.exe", "/overlaysetactive " + (custom.Length > 0 ? custom : "00000000-0000-0000-0000-000000000000")); break;
                    case "plan-ultimate":
                        var parts = custom.Split('|');
                        if (parts[0].Length > 0) RunCmd("powercfg.exe", "/setactive " + parts[0]);
                        if (parts.Length == 2) RunCmd("powercfg.exe", "/delete " + parts[1]);
                        break;
                }
            }
            catch (Exception ex) { fails++; log("! " + ex.Message); }
            if (!string.IsNullOrEmpty(t.UndoScript) && RunScript(t.UndoScript, log) != 0) fails++;
            backup.Remove(t.Id);
            Backup.Save(backup);
            if (t.Reboot) RebootPending = true;
            return Result(L.T("Deshecho", "Undone"), t, fails);
        }

        static string Result(string verb, Tweak t, int fails)
        {
            var text = verb + ": " + t.Name;
            if (Dry) return L.T("[simulación] ", "[simulation] ") + text;
            if (fails > 0) return text + ". " + fails + L.T(" cambio(s) bloqueado(s) por Windows.", " change(s) blocked by Windows.");
            return text;
        }

        public static string RestorePoint(Action<string> log)
        {
            var action = Catalog.Actions.FirstOrDefault(a => a.Id == "restore-point");
            if (action == null) return "Falta la acción de punto de restauración en el catálogo.";
            int code = RunScript(action.Script, log);
            return Dry ? L.T("[simulación] Punto de restauración", "[simulation] Restore point") : code == 0 ? L.T("Punto de restauración listo.", "Restore point created.") : L.T("No se pudo crear el punto de restauración.", "Could not create the restore point.");
        }
    }
}
